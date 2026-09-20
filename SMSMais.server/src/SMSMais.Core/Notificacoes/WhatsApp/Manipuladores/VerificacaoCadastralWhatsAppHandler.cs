using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Conversas;
using SMSMais.Core.Notificacoes.VerificacaoCadastral;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Telefones;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;

/// <summary>
/// Máquina de estados DETERMINÍSTICA da verificação cadastral no WhatsApp — SEM LLM no caminho
/// (lição do incidente de 26/08: vazamento, latência e validação fraca). A primeira mensagem
/// (<c>confirmacao_exame</c>/<c>confirmacao_consulta</c>) abre um
/// <see cref="VerificacaoCadastralEstado"/> em <c>AguardandoInteresse</c> — ela não pede nada.
/// Daqui em diante:
///
///   "Quero mais informações" (botão da primeira mensagem) → pede os 4 dígitos do CPF
///
///
///   "Não sou essa pessoa." (botão do template) → "Você conhece FULANO?" [Não conheço | Conheço]
///     → "Não conheço" marca o número como INVÁLIDO para aquele paciente (pendência de número
///       errado + carimbo no telefone do cadastro) e nada mais é enviado para ele sobre essa pessoa.
///
///   dígitos do CPF (≥4, sem pontuação) → mês/ano de nascimento (formatos tolerantes)
///     → confirmação do NOME (botões Sim/Não ou nome digitado)
///     → marca o telefone verificado PARA AQUELE paciente e ENVIA a comunicação PENDURADA
///       (a que o estado aponta — nada de procurar/deduzir), liberando também as demais
///       retidas do mesmo paciente. Multi-paciente no mesmo número: valida um por vez e
///       emenda o próximo desafio ao concluir.
///
/// Responde em ~1s (inline no webhook, molde do <see cref="ConfirmacaoAgendamentoWhatsAppHandler"/>).
/// "Prefiro falar com atendente" interrompe o interrogatório e deixa a conversa para a equipe.
/// Sem estado ativo (e sem desafio pendente), texto livre passa reto. Não chama SaveChanges.
/// </summary>
public sealed class VerificacaoCadastralWhatsAppHandler(
    SmsMaisDbContext db,
    IWhatsAppCliente whatsApp,
    IPacientesService pacientes,
    ITelefoneValidacaoService telefones,
    PendenciasCadastro.IPendenciaCadastroService pendencias,
    ILogger<VerificacaoCadastralWhatsAppHandler> logger) : IManipuladorMensagemWhatsApp
{
    private const string PrefixoSim = "vcad_sim:";
    private const string PrefixoNao = "vcad_nao:";
    private const string PrefixoTentarSim = "vcad_retry_sim:";
    private const string PrefixoTentarNao = "vcad_retry_nao:";
    private const string PrefixoNaoConheco = "vcad_naoconheco:";
    private const string PrefixoVinculoProprio = "vcad_vinc_proprio:";
    private const string PrefixoVinculoResponsavel = "vcad_vinc_resp:";
    private const string PrefixoVinculoParente = "vcad_vinc_parente:";
    private const string PrefixoConheco = "vcad_conheco:";

    /// <summary>Chances de acertar os dados antes de encerrar e orientar o posto. Cada ciclo
    /// (dígitos + nascimento) que não confere gasta uma — errar de digitação é comum.</summary>
    private const int MaxChances = 3;
    private const int MaxReorientacoes = 3; // mata loop com autoresponder/bot do outro lado
    private static readonly TimeSpan ValidadeEstado = TimeSpan.FromDays(7);
    private static readonly TimeSpan JanelaDesafios = TimeSpan.FromDays(20);

    public int Ordem => 120; // depois da confirmação Sim/Não (110), antes do robô (1000)

    public async Task TratarAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        if (ctx.Consumido) return;

        // Botões do próprio fluxo (confirmação de nome).
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoSim, out var idSim))
        {
            await TratarBotaoNomeAsync(ctx, idSim, confirmou: true, ct);
            return;
        }
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoNao, out var idNao))
        {
            await TratarBotaoNomeAsync(ctx, idNao, confirmou: false, ct);
            return;
        }
        // Botões do "quer tentar de novo?"
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoTentarSim, out var idRetrySim))
        {
            await TratarBotaoNovaTentativaAsync(ctx, idRetrySim, aceitou: true, ct);
            return;
        }
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoTentarNao, out var idRetryNao))
        {
            await TratarBotaoNovaTentativaAsync(ctx, idRetryNao, aceitou: false, ct);
            return;
        }
        // Vínculo declarado ("este número é seu ou você recebe pela pessoa?").
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoVinculoProprio, out var idVincProprio))
        {
            ctx.Consumido = true;
            await ConcluirComVinculoAsync(ctx, idVincProprio, VinculoContatoVerificado.Proprio, ct);
            return;
        }
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoVinculoResponsavel, out var idVincResp))
        {
            ctx.Consumido = true;
            await ConcluirComVinculoAsync(ctx, idVincResp, VinculoContatoVerificado.MaeOuPaiOuResponsavel, ct);
            return;
        }
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoVinculoParente, out var idVincParente))
        {
            ctx.Consumido = true;
            await ConcluirComVinculoAsync(ctx, idVincParente, VinculoContatoVerificado.OutroParenteOuCuidador, ct);
            return;
        }

        // "Você conhece FULANO?" — resposta à pergunta aberta por "Não sou essa pessoa."
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoNaoConheco, out var idNaoConheco))
        {
            ctx.Consumido = true;
            await MarcarNumeroInvalidoAsync(ctx, idNaoConheco, ct);
            return;
        }
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoConheco, out var idConheco))
        {
            ctx.Consumido = true;
            await TratarConhecePacienteAsync(ctx, idConheco, ct);
            return;
        }

        var telefone = ctx.Conversa.TelefoneCanonical;

        // Botões do TEMPLATE da primeira mensagem: a Meta devolve o texto do botão (o template não
        // leva payload próprio). Só valem se há conversa de identificação em aberto neste número.
        if (string.IsNullOrEmpty(ctx.InterativoReplyId))
        {
            if (InterpretadorRespostaCidadao.QuerMaisInformacoes(ctx.Texto))
            {
                if (await ComunicacaoDoDesafioAsync(ctx, telefone, ct) is { } alvoInteresse)
                {
                    ctx.Consumido = true;
                    await PedirCpfAsync(ctx, telefone, alvoInteresse, ct);
                }
                return;
            }
            if (InterpretadorRespostaCidadao.EhNaoSouEssaPessoa(ctx.Texto))
            {
                if (await ComunicacaoDoDesafioAsync(ctx, telefone, ct) is { } alvo)
                {
                    ctx.Consumido = true;
                    await PerguntarSeConhecePacienteAsync(ctx, alvo, ct);
                }
                return;
            }
            if (!string.IsNullOrEmpty(ctx.BotaoPayload) && InterpretadorRespostaCidadao.EhBotaoPrefiroAtendente(ctx.Texto))
            {
                if (await ComunicacaoDoDesafioAsync(ctx, telefone, ct) is not null)
                {
                    ctx.Consumido = true;
                    await ResponderAsync(ctx,
                        "Certo! Vou deixar sua conversa com a nossa equipe — um atendente te responde por aqui "
                        + "dentro do horário de atendimento.", ct);
                }
                return;
            }
        }

        // Botões/quick-replies de OUTROS domínios nunca são resposta do interrogatório.
        if (!string.IsNullOrEmpty(ctx.BotaoPayload) || !string.IsNullOrEmpty(ctx.InterativoReplyId)) return;
        if (string.IsNullOrWhiteSpace(ctx.Texto)) return;

        var estado = await db.VerificacoesCadastraisEstado
            .FirstOrDefaultAsync(e => e.TelefoneCanonical == telefone, ct);

        if (estado is not null && estado.ExpiraEm <= DateTime.UtcNow)
        {
            db.VerificacoesCadastraisEstado.Remove(estado);
            estado = null;
        }

        // BACKFILL: desafio já enviado (antes do estado existir — lote antigo, restart) e a pessoa
        // manda dígitos → cria o estado na hora. Sem dígitos e sem estado, passa reto.
        if (estado is null)
        {
            if (InterpretadorRespostaCidadao.ExtrairDigitosCpf(ctx.Texto) is null) return;
            var pendentes = await DesafiosPendentesDoTelefoneAsync(telefone, ct);
            if (pendentes.Count == 0) return;
            estado = new VerificacaoCadastralEstado
            {
                Id = Guid.CreateVersion7(),
                TelefoneCanonical = telefone,
                ComunicacaoPacienteId = pendentes[0].Id,
                Etapa = EtapaVerificacaoCadastral.AguardandoCpf,
                ExpiraEm = DateTime.UtcNow.Add(ValidadeEstado),
                CriadoEm = DateTime.UtcNow,
            };
            db.VerificacoesCadastraisEstado.Add(estado);
        }

        // Pedido de atendente interrompe o interrogatório (o estado fica; a pessoa pode voltar).
        if (InterpretadorRespostaCidadao.PedeAtendente(ctx.Texto))
        {
            ctx.Consumido = true;
            await ResponderAsync(ctx,
                "Certo! Vou deixar sua conversa com a nossa equipe — um atendente te responde por aqui "
                + "dentro do horário de atendimento.", ct);
            return;
        }

        switch (estado.Etapa)
        {
            case EtapaVerificacaoCadastral.AguardandoInteresse:
                // Quem manda os dígitos direto (sem tocar no botão) não é barrado por formalidade.
                if (InterpretadorRespostaCidadao.ExtrairDigitosCpf(ctx.Texto) is not null)
                {
                    estado.Etapa = EtapaVerificacaoCadastral.AguardandoCpf;
                    await TratarEtapaCpfAsync(ctx, estado, ct);
                }
                else
                {
                    await ReorientarAsync(ctx, estado,
                        "Para ver os dados do seu agendamento, toque em *Quero mais informações* na "
                        + "mensagem acima — ou responda com os *4 primeiros dígitos do CPF* do paciente.", ct);
                }
                break;
            case EtapaVerificacaoCadastral.AguardandoCpf:
                await TratarEtapaCpfAsync(ctx, estado, ct);
                break;
            case EtapaVerificacaoCadastral.AguardandoNascimento:
                await TratarEtapaNascimentoAsync(ctx, estado, ct);
                break;
            case EtapaVerificacaoCadastral.AguardandoNome:
                await TratarEtapaNomeTextoAsync(ctx, estado, ct);
                break;
            case EtapaVerificacaoCadastral.AguardandoVinculo:
                await TratarEtapaVinculoTextoAsync(ctx, estado, ct);
                break;
            case EtapaVerificacaoCadastral.AguardandoNovaTentativa:
                await TratarEtapaNovaTentativaTextoAsync(ctx, estado, ct);
                break;
            case EtapaVerificacaoCadastral.Esgotado:
                break; // chances esgotadas: silêncio — resolve-se no posto ou com um atendente
        }
    }

    // ---------- nova tentativa (após dados não conferirem) ----------

    private async Task TratarBotaoNovaTentativaAsync(
        ManipuladorContexto ctx, Guid estadoId, bool aceitou, CancellationToken ct)
    {
        var estado = await db.VerificacoesCadastraisEstado.FirstOrDefaultAsync(e => e.Id == estadoId, ct);
        if (estado is null || estado.Etapa != EtapaVerificacaoCadastral.AguardandoNovaTentativa) return;
        if (estado.ExpiraEm <= DateTime.UtcNow) { db.VerificacoesCadastraisEstado.Remove(estado); return; }

        ctx.Consumido = true;
        if (aceitou) await RecomecarCicloAsync(ctx, estado, ct);
        else await EncerrarOrientandoPostoAsync(ctx, estado, bloquear: false, ct);
    }

    /// <summary>Na espera do "quer tentar de novo?", aceitamos o Sim/Não — mas também os DÍGITOS
    /// direto: quem errou de digitação costuma só reenviar, e travar isso seria burocracia.</summary>
    private async Task TratarEtapaNovaTentativaTextoAsync(
        ManipuladorContexto ctx, VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        if (InterpretadorRespostaCidadao.ExtrairDigitosCpf(ctx.Texto) is not null)
        {
            // Já mandou os dígitos: recomeça o ciclo e processa esta mesma mensagem como a resposta.
            estado.Etapa = EtapaVerificacaoCadastral.AguardandoCpf;
            estado.PacienteId = null;
            estado.CpfDigitosInformados = null;
            await TratarEtapaCpfAsync(ctx, estado, ct);
            return;
        }
        if (InterpretadorRespostaCidadao.EhSim(ctx.Texto))
        {
            ctx.Consumido = true;
            await RecomecarCicloAsync(ctx, estado, ct);
            return;
        }
        if (InterpretadorRespostaCidadao.EhNao(ctx.Texto))
        {
            ctx.Consumido = true;
            await EncerrarOrientandoPostoAsync(ctx, estado, bloquear: false, ct);
            return;
        }
        await ReorientarAsync(ctx, estado,
            "Deseja tentar novamente? Responda *Sim* — ou envie de uma vez os *4 primeiros dígitos do "
            + "CPF* do paciente.", ct);
    }

    /// <summary>Zera o ciclo (volta a pedir os dígitos) preservando o contador de chances.</summary>
    private async Task RecomecarCicloAsync(
        ManipuladorContexto ctx, VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        estado.Etapa = EtapaVerificacaoCadastral.AguardandoCpf;
        estado.PacienteId = null;
        estado.CpfDigitosInformados = null;
        estado.Reorientacoes = 0;
        Tocar(estado);

        var nome = await PrimeiroNomeDoAlvoAsync(estado, ct);
        var restantes = Math.Max(0, MaxChances - estado.TentativasErradas);
        await ResponderAsync(ctx,
            $"Vamos lá! Envie os *4 primeiros dígitos do CPF* {(nome is null ? "do paciente" : $"de *{nome}*")}."
            + (restantes == 1 ? " Esta é a última tentativa." : string.Empty), ct);
    }

    /// <summary>Os dados não conferiram: avisa, EXPLICA em mensagem separada que CPF e nascimento são
    /// os do PACIENTE do agendamento (não os de quem escreve) e pergunta se quer tentar de novo.
    /// Esgotadas as chances, orienta o posto e encerra.</summary>
    private async Task FalharCicloAsync(
        ManipuladorContexto ctx, VerificacaoCadastralEstado estado, string oQueNaoConfere, CancellationToken ct)
    {
        estado.TentativasErradas++;
        Tocar(estado);
        await ResponderAsync(ctx, $"{oQueNaoConfere} não confere com o cadastro.", ct);

        if (estado.TentativasErradas >= MaxChances)
        {
            await EncerrarOrientandoPostoAsync(ctx, estado, bloquear: true, ct);
            return;
        }

        estado.Etapa = EtapaVerificacaoCadastral.AguardandoNovaTentativa;
        estado.PacienteId = null;
        estado.CpfDigitosInformados = null;
        estado.Reorientacoes = 0;

        // Mensagem SEPARADA: a confusão mais comum é o parente informar os próprios dados.
        var nome = await PrimeiroNomeDoAlvoAsync(estado, ct);
        var deQuem = nome is null ? "do paciente do agendamento" : $"de *{nome}*, o paciente do agendamento";
        await ResponderAsync(ctx,
            $"Atenção: o CPF e a data de nascimento precisam ser {deQuem} — não os de quem está "
            + "escrevendo. Se você é parente ou responsável, informe os dados do paciente.", ct);

        await whatsApp.EnviarInterativoBotoesAsync(
            estado.TelefoneCanonical,
            "Deseja tentar novamente?",
            [
                new BotaoInterativoWhatsApp($"{PrefixoTentarSim}{estado.Id}", "Sim, tentar de novo"),
                new BotaoInterativoWhatsApp($"{PrefixoTentarNao}{estado.Id}", "Não"),
            ],
            pacienteId: ctx.PacienteId, ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
    }

    /// <param name="bloquear">Chances esgotadas/ambiguidade: marca <c>Esgotado</c> e MANTÉM o estado —
    /// apagá-lo faria o reenvio de dígitos recriar o diálogo com o contador zerado (tentativas
    /// infinitas). Quando é só desistência ("Não") com chances de sobra, o diálogo fica aberto.</param>
    private async Task EncerrarOrientandoPostoAsync(
        ManipuladorContexto ctx, VerificacaoCadastralEstado estado, bool bloquear, CancellationToken ct)
    {
        estado.Etapa = bloquear
            ? EtapaVerificacaoCadastral.Esgotado
            : EtapaVerificacaoCadastral.AguardandoNovaTentativa;
        estado.PacienteId = null;
        estado.CpfDigitosInformados = null;
        Tocar(estado);

        logger.LogInformation(
            "Verificação cadastral encerrada sem sucesso (…{Fone4}) após {Qtd} tentativa(s); bloqueado={Bloq}.",
            Ultimos4(estado.TelefoneCanonical), estado.TentativasErradas, bloquear);
        await ResponderAsync(ctx,
            (bloquear ? "Não consegui confirmar os dados por aqui." : "Tudo bem.")
            + " Para sua segurança, não posso enviar as informações do agendamento sem essa confirmação. "
            + "Procure o posto de saúde onde o paciente é atendido para retirar a guia e atualizar o "
            + "cadastro.", ct);
    }

    /// <summary>Primeiro nome do paciente da comunicação PENDURADA (o mesmo que foi ao template).</summary>
    private async Task<string?> PrimeiroNomeDoAlvoAsync(VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        var pacienteId = estado.PacienteId ?? await db.ComunicacoesPaciente.AsNoTracking()
            .Where(n => n.Id == estado.ComunicacaoPacienteId)
            .Select(n => (Guid?)n.PacienteId)
            .FirstOrDefaultAsync(ct);
        if (pacienteId is not { } id) return null;
        return PrimeiroNome((await ObterPacienteAsync(id, ct))?.NomeCompleto);
    }

    // ---------- etapa 1: dígitos do CPF ----------

    private async Task TratarEtapaCpfAsync(ManipuladorContexto ctx, VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        var digitos = InterpretadorRespostaCidadao.ExtrairDigitosCpf(ctx.Texto);
        if (digitos is null)
        {
            await ReorientarAsync(ctx, estado,
                "Para sua segurança, preciso que você envie apenas os *4 primeiros dígitos do CPF* "
                + "do paciente para continuar.", ct);
            return;
        }

        ctx.Consumido = true;
        Tocar(estado);

        var candidatos = await CandidatosDoTelefoneAsync(estado, ct);
        var casam = candidatos.Where(c => InterpretadorRespostaCidadao.CpfPrefixoConfere(c.Paciente.Cpf, digitos)).ToList();

        if (casam.Count == 0)
        {
            await FalharCicloAsync(ctx, estado, "Esse início de CPF", ct);
            return;
        }

        estado.CpfDigitosInformados = digitos;
        estado.Etapa = EtapaVerificacaoCadastral.AguardandoNascimento;
        if (casam.Count == 1)
        {
            estado.PacienteId = casam[0].Paciente.Id;
            estado.ComunicacaoPacienteId = casam[0].ComunicacaoId;
        }
        // >1: nascimento desambigua; alvo fica em aberto.

        await ResponderAsync(ctx,
            "Obrigado! Agora, para concluir, me informe o *mês e o ano de nascimento* do paciente.", ct);
    }

    // ---------- etapa 2: mês/ano de nascimento ----------

    private async Task TratarEtapaNascimentoAsync(ManipuladorContexto ctx, VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        var resposta = InterpretadorRespostaCidadao.TentarLerNascimento(ctx.Texto);
        if (resposta is null)
        {
            await ReorientarAsync(ctx, estado,
                "Preciso do *mês e do ano de nascimento* do paciente para continuar.", ct);
            return;
        }

        ctx.Consumido = true;
        Tocar(estado);

        var candidatos = await CandidatosDoTelefoneAsync(estado, ct);
        if (estado.PacienteId is { } alvoId)
            candidatos = [.. candidatos.Where(c => c.Paciente.Id == alvoId)];
        else if (!string.IsNullOrEmpty(estado.CpfDigitosInformados))
            candidatos = [.. candidatos.Where(c =>
                InterpretadorRespostaCidadao.CpfPrefixoConfere(c.Paciente.Cpf, estado.CpfDigitosInformados))];

        var casam = candidatos.Where(c =>
            InterpretadorRespostaCidadao.NascimentoConfere(c.Paciente.DataNascimento, resposta)).ToList();

        if (casam.Count != 1)
        {
            if (casam.Count > 1)
            {
                // Mesmo prefixo de CPF + mesmo mês/ano: não dá para desambiguar com segurança.
                logger.LogWarning("Verificação cadastral ambígua ({Qtd} candidatos) no telefone …{Fone4}.",
                    casam.Count, Ultimos4(estado.TelefoneCanonical));
                await EncerrarOrientandoPostoAsync(ctx, estado, bloquear: true, ct);
                return;
            }
            await FalharCicloAsync(ctx, estado, "Esse mês/ano de nascimento", ct);
            return;
        }

        var escolhido = casam[0];
        estado.PacienteId = escolhido.Paciente.Id;
        estado.ComunicacaoPacienteId = escolhido.ComunicacaoId;
        estado.Etapa = EtapaVerificacaoCadastral.AguardandoNome;

        await whatsApp.EnviarInterativoBotoesAsync(
            estado.TelefoneCanonical,
            $"Estamos quase lá! Confirme: o paciente é *{escolhido.Paciente.NomeCompleto?.Trim()}*?",
            [
                new BotaoInterativoWhatsApp($"{PrefixoSim}{estado.Id}", "Sim"),
                new BotaoInterativoWhatsApp($"{PrefixoNao}{estado.Id}", "Não"),
            ],
            pacienteId: ctx.PacienteId, ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
    }

    // ---------- etapa 3: confirmação do nome ----------

    private async Task TratarBotaoNomeAsync(ManipuladorContexto ctx, Guid estadoId, bool confirmou, CancellationToken ct)
    {
        var estado = await db.VerificacoesCadastraisEstado
            .FirstOrDefaultAsync(e => e.Id == estadoId, ct);
        if (estado is null || estado.Etapa != EtapaVerificacaoCadastral.AguardandoNome) return;
        if (estado.ExpiraEm <= DateTime.UtcNow) { db.VerificacoesCadastraisEstado.Remove(estado); return; }

        ctx.Consumido = true;
        if (confirmou) await PerguntarVinculoAsync(ctx, estado, ct);
        else await ConcluirComoNumeroErradoAsync(ctx, estado, ct);
    }

    private async Task TratarEtapaNomeTextoAsync(ManipuladorContexto ctx, VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        if (estado.PacienteId is not { } pacienteId)
        {
            db.VerificacoesCadastraisEstado.Remove(estado); // estado inconsistente — recomeça no próximo contato
            return;
        }
        var paciente = await ObterPacienteAsync(pacienteId, ct);
        if (paciente is null) { db.VerificacoesCadastraisEstado.Remove(estado); return; }

        if (InterpretadorRespostaCidadao.EhSim(ctx.Texto)
            || InterpretadorRespostaCidadao.NomeConfere(paciente.NomeCompleto, ctx.Texto))
        {
            ctx.Consumido = true;
            await PerguntarVinculoAsync(ctx, estado, ct);
            return;
        }
        if (InterpretadorRespostaCidadao.EhNao(ctx.Texto))
        {
            ctx.Consumido = true;
            await ConcluirComoNumeroErradoAsync(ctx, estado, ct);
            return;
        }

        await ReorientarAsync(ctx, estado,
            $"Só falta confirmar: o paciente é *{paciente.NomeCompleto?.Trim()}*? Responda *Sim* ou *Não*.", ct);
    }

    // ---------- etapa 4: vínculo (LGPD) ----------

    /// <summary>
    /// Última pergunta: o número é do próprio paciente ou de quem recebe por ele? A resposta fica
    /// GRAVADA no cadastro — é o que permite o celular da mãe atender pelos filhos sem virar
    /// "número duplicado", e é a resposta de LGPD para "por que essa pessoa recebe o dado daquela".
    /// </summary>
    private async Task PerguntarVinculoAsync(
        ManipuladorContexto ctx, VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        estado.Etapa = EtapaVerificacaoCadastral.AguardandoVinculo;
        estado.Reorientacoes = 0;
        Tocar(estado);

        var nome = await PrimeiroNomeDoAlvoAsync(estado, ct);
        await whatsApp.EnviarInterativoBotoesAsync(
            estado.TelefoneCanonical,
            $"Confirmado! Só falta uma coisa: este WhatsApp é {(nome is null ? "do paciente" : $"de *{nome}*")} "
            + "ou você recebe as mensagens por ele(a)?",
            [
                new BotaoInterativoWhatsApp($"{PrefixoVinculoProprio}{estado.Id}", "Sou o paciente"),
                new BotaoInterativoWhatsApp($"{PrefixoVinculoResponsavel}{estado.Id}", "Sou responsável"),
                new BotaoInterativoWhatsApp($"{PrefixoVinculoParente}{estado.Id}", "Outro parente"),
            ],
            pacienteId: ctx.PacienteId, ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
    }

    private async Task ConcluirComVinculoAsync(
        ManipuladorContexto ctx, Guid estadoId, VinculoContatoVerificado vinculo, CancellationToken ct)
    {
        var estado = await db.VerificacoesCadastraisEstado.FirstOrDefaultAsync(e => e.Id == estadoId, ct);
        if (estado is null || estado.Etapa != EtapaVerificacaoCadastral.AguardandoVinculo) return;
        if (estado.ExpiraEm <= DateTime.UtcNow) { db.VerificacoesCadastraisEstado.Remove(estado); return; }
        await ConcluirComSucessoAsync(ctx, estado, vinculo, ct);
    }

    /// <summary>Quem responde em texto ("sou a mãe dele", "sou eu mesmo") não precisa tocar botão.</summary>
    private async Task TratarEtapaVinculoTextoAsync(
        ManipuladorContexto ctx, VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        if (InterpretadorRespostaCidadao.TentarLerVinculo(ctx.Texto) is { } vinculo)
        {
            ctx.Consumido = true;
            await ConcluirComSucessoAsync(ctx, estado, vinculo, ct);
            return;
        }
        await ReorientarAsync(ctx, estado,
            "Só para registrar: este WhatsApp é *do próprio paciente* ou você recebe as mensagens "
            + "*por ele(a)* (mãe, pai, responsável, parente)?", ct);
    }

    // ---------- conclusões ----------

    private async Task ConcluirComSucessoAsync(
        ManipuladorContexto ctx, VerificacaoCadastralEstado estado, VinculoContatoVerificado vinculo,
        CancellationToken ct)
    {
        var pacienteId = estado.PacienteId!.Value;
        var paciente = await ObterPacienteAsync(pacienteId, ct);
        var telefone = estado.TelefoneCanonical;

        // Mesma régua do robô e do login: verificação cadastral NÃO troca contato já provado. Se o
        // paciente tem outro número verificado (ex.: foi verificado depois que este desafio saiu), o
        // carimbo não acontece; a comunicação liberada abaixo sai para o número verificado, e trocar
        // o cadastro exige o posto ([ADR-0057]).
        var trocariaVerificado = !string.IsNullOrWhiteSpace(paciente?.TelefoneVerificado)
            && !Telefones.TelefoneValidacaoService.EhMesmoNumero(paciente!.TelefoneVerificado, telefone);
        if (trocariaVerificado)
            logger.LogInformation(
                "Verificação cadastral concluída em …{Fone4}, mas o paciente {Paciente} já tem outro "
                + "contato verificado: o número NÃO foi trocado.", Ultimos4(telefone), pacienteId);

        // Carimbo de telefone verificado (melhor esforço; conflito = número confirmado de outro CPF).
        if (!trocariaVerificado && !string.IsNullOrWhiteSpace(paciente?.Cpf))
        {
            try
            {
                await telefones.MarcarValidadoAsync(
                    paciente!.Cpf, telefone, "verificacao-cadastral", null, ct, vinculo);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao carimbar telefone verificado na verificação cadastral (paciente {Id}).", pacienteId);
            }
        }

        // Libera a comunicação PENDURADA + todas as retidas do MESMO paciente. IgnorarVerificacaoTelefone
        // garante o envio mesmo se o carimbo FHIR falhar (anti-loop de desafio).
        var agora = DateTime.UtcNow;
        var retidas = await db.ComunicacoesPaciente
            .Where(n => (n.Id == estado.ComunicacaoPacienteId
                    || (n.PacienteId == pacienteId && n.Status == StatusComunicacao.AguardandoVerificacaoCadastral))
                && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
            .ToListAsync(ct);
        foreach (var n in retidas)
        {
            if (n.Status is StatusComunicacao.Enviada or StatusComunicacao.Entregue or StatusComunicacao.Lida)
                continue; // flag anti-reenvio: o que já saiu não sai de novo
            if (n.Status == StatusComunicacao.SubstituidaPorAtendente)
                continue; // uma pessoa já entrou no circuito (menu Confirmações): o automático não volta
            n.Status = StatusComunicacao.Pendente;
            n.ProximaTentativaEm = agora;
            n.MotivoFalha = null;
            n.IgnorarVerificacaoTelefone = true;
            // O paciente acabou de se identificar e está esperando a resposta: sai mesmo fora do horário.
            n.IgnorarJanelaHorario = true;
            n.Tentativas = 0;
        }

        db.VerificacoesCadastraisEstado.Remove(estado);
        logger.LogInformation(
            "Verificação cadastral concluída para o paciente {Paciente} (…{Fone4}); {Qtd} comunicação(ões) liberada(s).",
            pacienteId, Ultimos4(telefone), retidas.Count);

        var nome = PrimeiroNome(paciente?.NomeCompleto);
        var tratamento = vinculo == VinculoContatoVerificado.Proprio && nome is not null ? $", {nome}" : "";
        await ResponderAsync(ctx,
            $"Perfeito{tratamento}! Cadastro confirmado. Já estou enviando as "
            + "informações do agendamento — chegam aqui em instantes.", ct);

        // Multi-paciente: há desafio pendente para OUTRA pessoa neste número? Emenda o próximo.
        var proximos = (await DesafiosPendentesDoTelefoneAsync(telefone, ct))
            .Where(p => p.PacienteId != pacienteId).ToList();
        if (proximos.Count > 0)
        {
            var proximo = proximos[0];
            db.VerificacoesCadastraisEstado.Add(new VerificacaoCadastralEstado
            {
                Id = Guid.CreateVersion7(),
                TelefoneCanonical = telefone,
                ComunicacaoPacienteId = proximo.Id,
                Etapa = EtapaVerificacaoCadastral.AguardandoCpf,
                ExpiraEm = DateTime.UtcNow.Add(ValidadeEstado),
                CriadoEm = DateTime.UtcNow,
            });
            var outroNome = PrimeiroNome((await ObterPacienteAsync(proximo.PacienteId, ct))?.NomeCompleto);
            await ResponderAsync(ctx,
                $"Temos também uma informação para *{outroNome ?? "outra pessoa"}* neste número. Para "
                + "recebê-la, envie os *4 primeiros dígitos do CPF* dele(a).", ct);
        }
    }

    private async Task ConcluirComoNumeroErradoAsync(ManipuladorContexto ctx, VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        db.VerificacoesCadastraisEstado.Remove(estado);
        if (estado.PacienteId is { } pacienteId)
            await RegistrarNumeroInvalidoAsync(ctx, estado.TelefoneCanonical, pacienteId,
                "Verificação cadastral: a pessoa negou ser o paciente na confirmação do nome.", ct);

        await ResponderAsync(ctx,
            "Sem problemas, obrigado por avisar! Este número não será usado para essa pessoa. A equipe "
            + "de cadastro vai revisar o contato.", ct);
    }

    // ---------- "Quero mais informações" ----------

    /// <summary>
    /// O toque em "Quero mais informações" é o que destrava o pedido do CPF. A primeira mensagem
    /// deliberadamente não pede nada: quando pedia, a pessoa não respondia — ia no outro botão.
    /// </summary>
    private async Task PedirCpfAsync(
        ManipuladorContexto ctx, string telefone, Data.Entities.ComunicacaoPaciente alvo, CancellationToken ct)
    {
        var estado = await db.VerificacoesCadastraisEstado
            .FirstOrDefaultAsync(e => e.TelefoneCanonical == telefone, ct);
        var agora = DateTime.UtcNow;
        if (estado is null)
        {
            estado = new VerificacaoCadastralEstado
            {
                Id = Guid.CreateVersion7(),
                TelefoneCanonical = telefone,
                ComunicacaoPacienteId = alvo.Id,
                Etapa = EtapaVerificacaoCadastral.AguardandoCpf,
                ExpiraEm = agora.Add(ValidadeEstado),
                CriadoEm = agora,
            };
            db.VerificacoesCadastraisEstado.Add(estado);
        }
        else if (estado.Etapa is EtapaVerificacaoCadastral.AguardandoInteresse
                 or EtapaVerificacaoCadastral.AguardandoCpf)
        {
            // Diálogo já adiantado (nascimento, nome) não volta para trás por um toque repetido.
            estado.ComunicacaoPacienteId = alvo.Id;
            estado.Etapa = EtapaVerificacaoCadastral.AguardandoCpf;
            estado.Reorientacoes = 0;
            estado.ExpiraEm = agora.Add(ValidadeEstado);
            Tocar(estado);
        }

        var nome = PrimeiroNome((await ObterPacienteAsync(alvo.PacienteId, ct))?.NomeCompleto);
        await ResponderAsync(ctx,
            "Claro! Para sua segurança, informe apenas os *4 primeiros números do CPF* "
            + $"{(nome is null ? "do paciente" : $"de *{nome}*")}.", ct);
    }

    // ---------- "Não sou essa pessoa." ----------

    /// <summary>A comunicação de que trata o toque: a mensagem respondida (contexto) → o estado do
    /// diálogo → o desafio pendente mais novo do número. Null = não há desafio para este número.</summary>
    private async Task<Data.Entities.ComunicacaoPaciente?> ComunicacaoDoDesafioAsync(
        ManipuladorContexto ctx, string telefone, CancellationToken ct)
    {
        if (ctx.Mensagem.ContextoWaMessageId is { } wamid)
        {
            var porContexto = await db.ComunicacoesPaciente
                .Where(n => n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                    && n.MensagemWhatsApp != null && n.MensagemWhatsApp.WaMessageId == wamid)
                .FirstOrDefaultAsync(ct);
            if (porContexto is not null) return porContexto;
        }

        var estado = await db.VerificacoesCadastraisEstado.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TelefoneCanonical == telefone && e.ExpiraEm > DateTime.UtcNow, ct);
        if (estado is not null)
            return await db.ComunicacoesPaciente.FirstOrDefaultAsync(n => n.Id == estado.ComunicacaoPacienteId, ct);

        var pendentes = await DesafiosPendentesDoTelefoneAsync(telefone, ct);
        return pendentes.Count == 0
            ? null
            : await db.ComunicacoesPaciente.FirstOrDefaultAsync(n => n.Id == pendentes[0].Id, ct);
    }

    /// <summary>Confirma ANTES de marcar: tocar no botão errado não pode inutilizar o número de
    /// um paciente. A pergunta usa o primeiro nome que o próprio template já mostrou.</summary>
    private async Task PerguntarSeConhecePacienteAsync(
        ManipuladorContexto ctx, Data.Entities.ComunicacaoPaciente alvo, CancellationToken ct)
    {
        var nome = PrimeiroNome((await ObterPacienteAsync(alvo.PacienteId, ct))?.NomeCompleto);
        var quem = nome is null ? "o paciente" : $"*{nome}*";
        await whatsApp.EnviarInterativoBotoesAsync(
            ctx.Conversa.TelefoneCanonical,
            $"Entendi. Você *conhece* {quem}, a pessoa desta mensagem?\n\n"
            + "Se for alguém da sua família, toque em *Conheço* e informe os dados dela.",
            [
                new BotaoInterativoWhatsApp($"{PrefixoNaoConheco}{alvo.Id}", "Não conheço"),
                new BotaoInterativoWhatsApp($"{PrefixoConheco}{alvo.Id}", "Conheço"),
            ],
            pacienteId: ctx.PacienteId, ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
    }

    private async Task TratarConhecePacienteAsync(ManipuladorContexto ctx, Guid comunicacaoId, CancellationToken ct)
    {
        var alvo = await db.ComunicacoesPaciente.AsNoTracking().FirstOrDefaultAsync(n => n.Id == comunicacaoId, ct);
        if (alvo is null) return;
        var nome = PrimeiroNome((await ObterPacienteAsync(alvo.PacienteId, ct))?.NomeCompleto);
        var deQuem = nome is null ? "do paciente" : $"de *{nome}*";
        await ResponderAsync(ctx,
            $"Tudo bem! Então envie os *4 primeiros dígitos do CPF* {deQuem} para continuar.", ct);
    }

    /// <summary>"Não conheço": o número passa a ser INVÁLIDO para esse paciente — pendência de número
    /// errado + carimbo no telefone do cadastro (o envio pula número carimbado) — e as confirmações
    /// dele presas neste número ficam retidas até a recepção corrigir o contato.</summary>
    private async Task MarcarNumeroInvalidoAsync(ManipuladorContexto ctx, Guid comunicacaoId, CancellationToken ct)
    {
        var alvo = await db.ComunicacoesPaciente.FirstOrDefaultAsync(n => n.Id == comunicacaoId, ct);
        if (alvo is null) return;
        var telefone = ctx.Conversa.TelefoneCanonical;

        await RegistrarNumeroInvalidoAsync(ctx, telefone, alvo.PacienteId,
            "Quem atende este número disse que NÃO CONHECE o paciente (botão \"Não sou essa pessoa.\" "
            + "da confirmação de agendamento).", ct);

        var estado = await db.VerificacoesCadastraisEstado.FirstOrDefaultAsync(e => e.TelefoneCanonical == telefone, ct);
        if (estado is not null && (estado.ComunicacaoPacienteId == alvo.Id || estado.PacienteId == alvo.PacienteId))
            db.VerificacoesCadastraisEstado.Remove(estado);

        logger.LogInformation("Número …{Fone4} marcado como inválido para o paciente {Paciente} (não conhece).",
            Ultimos4(telefone), alvo.PacienteId);
        await ResponderAsync(ctx,
            "Obrigado por avisar! 🙏 Este número *não vai mais receber* mensagens sobre essa pessoa. "
            + "Desculpe o incômodo.", ct);
    }

    private async Task RegistrarNumeroInvalidoAsync(
        ManipuladorContexto ctx, string telefone, Guid pacienteId, string observacao, CancellationToken ct)
    {
        try
        {
            await pendencias.RegistrarNumeroErradoAsync(
                ctx.Conversa.Id, telefone, pacienteId, VinculoContato.SemVinculo, observacao, criadoPor: null, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao registrar número inválido (…{Fone4}) do paciente {Paciente}.",
                Ultimos4(telefone), pacienteId);
        }

        // Tudo o que estava preso para esse paciente NESTE número fica retido com o motivo explícito.
        var retidas = await db.ComunicacoesPaciente
            .Where(n => n.PacienteId == pacienteId
                && (n.Status == StatusComunicacao.AguardandoVerificacaoCadastral || n.Status == StatusComunicacao.Pendente))
            .ToListAsync(ct);
        foreach (var n in retidas.Where(n => n.Telefone is not null && TelefoneWhatsApp.MesmoNumero(n.Telefone, telefone)))
        {
            n.Status = StatusComunicacao.AguardandoCorrecaoContato;
            n.MotivoFalha = "Número INVÁLIDO: quem atende disse que não conhece o paciente. "
                + "Atualize o telefone no cadastro para liberar o envio.";
            n.ProximaTentativaEm = null;
            n.AtualizadoEm = DateTime.UtcNow;
        }
    }

    // ---------- apoio ----------

    private sealed record Candidato(Guid ComunicacaoId, Guid PacienteId, PacienteDto Paciente);

    /// <summary>Comunicações com desafio pendente (status 8) deste telefone, mais novas primeiro.</summary>
    private async Task<List<(Guid Id, Guid PacienteId)>> DesafiosPendentesDoTelefoneAsync(string telefone, CancellationToken ct)
    {
        var limite = DateTime.UtcNow.Subtract(JanelaDesafios);
        var brutas = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(n => n.Status == StatusComunicacao.AguardandoVerificacaoCadastral
                && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                && n.Telefone != null && n.CriadoEm >= limite)
            .OrderByDescending(n => n.CriadoEm)
            .Select(n => new { n.Id, n.PacienteId, n.Telefone })
            .ToListAsync(ct);
        return [.. brutas
            .Where(n => TelefoneWhatsApp.Canonizar(n.Telefone!) == telefone)
            .Select(n => (n.Id, n.PacienteId))];
    }

    /// <summary>Candidatos à validação: pacientes dos desafios pendentes do telefone + o alvo do
    /// próprio estado (a comunicação pendurada pode já ter saído do status 8 — ex.: reparo).</summary>
    private async Task<List<Candidato>> CandidatosDoTelefoneAsync(VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        var pares = await DesafiosPendentesDoTelefoneAsync(estado.TelefoneCanonical, ct);
        var proprio = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(n => n.Id == estado.ComunicacaoPacienteId)
            .Select(n => new { n.Id, n.PacienteId })
            .FirstOrDefaultAsync(ct);
        if (proprio is not null && pares.All(p => p.Id != proprio.Id))
            pares.Insert(0, (proprio.Id, proprio.PacienteId));

        var lista = new List<Candidato>();
        foreach (var grupo in pares.GroupBy(p => p.PacienteId))
        {
            var paciente = await ObterPacienteAsync(grupo.Key, ct);
            if (paciente is not null)
                lista.Add(new Candidato(grupo.First().Id, grupo.Key, paciente));
        }
        return lista;
    }

    private async Task<PacienteDto?> ObterPacienteAsync(Guid id, CancellationToken ct)
    {
        try { return await pacientes.ObterPorIdAsync(id, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Verificação cadastral: paciente {Id} não pôde ser carregado.", id);
            return null;
        }
    }

    /// <summary>Texto não interpretável: re-orienta com teto (anti-loop com autoresponder).</summary>
    private async Task ReorientarAsync(ManipuladorContexto ctx, VerificacaoCadastralEstado estado, string texto, CancellationToken ct)
    {
        if (estado.Reorientacoes >= MaxReorientacoes) return; // silencia; segue aceitando resposta válida
        estado.Reorientacoes++;
        Tocar(estado);
        ctx.Consumido = true;
        await ResponderAsync(ctx, texto, ct);
    }

    private Task ResponderAsync(ManipuladorContexto ctx, string texto, CancellationToken ct) =>
        whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical, texto, pacienteId: ctx.PacienteId, ct: ct,
            origem: OrigemEnvioWhatsApp.Resposta);

    private static void Tocar(VerificacaoCadastralEstado estado) => estado.AtualizadoEm = DateTime.UtcNow;

    private static string? PrimeiroNome(string? nome)
    {
        var t = (nome ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        var p = t.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        return char.ToUpperInvariant(p[0]) + p[1..];
    }

    private static string Ultimos4(string s) => s.Length <= 4 ? s : s[^4..];

    private static bool TentarExtrairId(string? valor, string prefixo, out Guid id)
    {
        id = default;
        return valor is not null
            && valor.StartsWith(prefixo, StringComparison.Ordinal)
            && Guid.TryParse(valor[prefixo.Length..], out id);
    }
}
