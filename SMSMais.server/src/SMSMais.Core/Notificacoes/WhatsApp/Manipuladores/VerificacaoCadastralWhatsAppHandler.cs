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
/// (lição do incidente de 26/08: vazamento, latência e validação fraca). O desafio
/// <c>validacao_cadastro</c> abre um <see cref="VerificacaoCadastralEstado"/>; daqui em diante:
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
    ILogger<VerificacaoCadastralWhatsAppHandler> logger) : IManipuladorMensagemWhatsApp
{
    private const string PrefixoSim = "vcad_sim:";
    private const string PrefixoNao = "vcad_nao:";
    private const int MaxTentativasErradas = 2;
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
        // Botões/quick-replies de OUTROS domínios nunca são resposta do interrogatório.
        if (!string.IsNullOrEmpty(ctx.BotaoPayload) || !string.IsNullOrEmpty(ctx.InterativoReplyId)) return;
        if (string.IsNullOrWhiteSpace(ctx.Texto)) return;

        var telefone = ctx.Conversa.TelefoneCanonical;
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
            case EtapaVerificacaoCadastral.AguardandoCpf:
                await TratarEtapaCpfAsync(ctx, estado, ct);
                break;
            case EtapaVerificacaoCadastral.AguardandoNascimento:
                await TratarEtapaNascimentoAsync(ctx, estado, ct);
                break;
            case EtapaVerificacaoCadastral.AguardandoNome:
                await TratarEtapaNomeTextoAsync(ctx, estado, ct);
                break;
        }
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
            estado.TentativasErradas++;
            if (estado.TentativasErradas <= MaxTentativasErradas)
                await ResponderAsync(ctx,
                    "Esses dígitos não conferem com o cadastro. Confira com calma e envie novamente os "
                    + "*4 primeiros dígitos do CPF* do paciente.", ct);
            else if (estado.TentativasErradas == MaxTentativasErradas + 1)
                await ResponderAsync(ctx,
                    "Não foi possível confirmar os dados por aqui. Para sua segurança, procure o posto de "
                    + "saúde onde o paciente é atendido para atualizar o cadastro e retirar a guia.", ct);
            // depois disso, silencia (continua aceitando uma resposta correta, sem dar pistas).
            return;
        }

        estado.CpfDigitosInformados = digitos;
        estado.TentativasErradas = 0;
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
                await ResponderAsync(ctx,
                    "Não consegui concluir a confirmação por aqui. Procure o posto de saúde onde o "
                    + "paciente é atendido para retirar a guia com segurança.", ct);
                return;
            }
            estado.TentativasErradas++;
            if (estado.TentativasErradas <= MaxTentativasErradas)
                await ResponderAsync(ctx,
                    "Esse mês/ano não confere com o cadastro. Confira e envie novamente o "
                    + "*mês e o ano de nascimento* do paciente.", ct);
            else if (estado.TentativasErradas == MaxTentativasErradas + 1)
                await ResponderAsync(ctx,
                    "Não foi possível confirmar os dados por aqui. Para sua segurança, procure o posto de "
                    + "saúde onde o paciente é atendido para atualizar o cadastro e retirar a guia.", ct);
            return;
        }

        var escolhido = casam[0];
        estado.PacienteId = escolhido.Paciente.Id;
        estado.ComunicacaoPacienteId = escolhido.ComunicacaoId;
        estado.TentativasErradas = 0;
        estado.Etapa = EtapaVerificacaoCadastral.AguardandoNome;

        await whatsApp.EnviarInterativoBotoesAsync(
            estado.TelefoneCanonical,
            $"Estamos quase lá! Confirme: o paciente é *{escolhido.Paciente.NomeCompleto?.Trim()}*?",
            [
                new BotaoInterativoWhatsApp($"{PrefixoSim}{estado.Id}", "Sim"),
                new BotaoInterativoWhatsApp($"{PrefixoNao}{estado.Id}", "Não"),
            ],
            pacienteId: ctx.PacienteId, ct: ct);
    }

    // ---------- etapa 3: confirmação do nome ----------

    private async Task TratarBotaoNomeAsync(ManipuladorContexto ctx, Guid estadoId, bool confirmou, CancellationToken ct)
    {
        var estado = await db.VerificacoesCadastraisEstado
            .FirstOrDefaultAsync(e => e.Id == estadoId, ct);
        if (estado is null || estado.Etapa != EtapaVerificacaoCadastral.AguardandoNome) return;
        if (estado.ExpiraEm <= DateTime.UtcNow) { db.VerificacoesCadastraisEstado.Remove(estado); return; }

        ctx.Consumido = true;
        if (confirmou) await ConcluirComSucessoAsync(ctx, estado, ct);
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
            await ConcluirComSucessoAsync(ctx, estado, ct);
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

    // ---------- conclusões ----------

    private async Task ConcluirComSucessoAsync(ManipuladorContexto ctx, VerificacaoCadastralEstado estado, CancellationToken ct)
    {
        var pacienteId = estado.PacienteId!.Value;
        var paciente = await ObterPacienteAsync(pacienteId, ct);
        var telefone = estado.TelefoneCanonical;

        // Carimbo de telefone verificado (melhor esforço; conflito = número confirmado de outro CPF).
        if (!string.IsNullOrWhiteSpace(paciente?.Cpf))
        {
            try { await telefones.MarcarValidadoAsync(paciente!.Cpf, telefone, "verificacao-cadastral", null, ct); }
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
            n.Status = StatusComunicacao.Pendente;
            n.ProximaTentativaEm = agora;
            n.MotivoFalha = null;
            n.IgnorarVerificacaoTelefone = true;
            n.Tentativas = 0;
        }

        db.VerificacoesCadastraisEstado.Remove(estado);
        logger.LogInformation(
            "Verificação cadastral concluída para o paciente {Paciente} (…{Fone4}); {Qtd} comunicação(ões) liberada(s).",
            pacienteId, Ultimos4(telefone), retidas.Count);

        var nome = PrimeiroNome(paciente?.NomeCompleto);
        await ResponderAsync(ctx,
            $"Perfeito{(nome is null ? "" : $", {nome}")}! Cadastro confirmado. Já estou enviando as "
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
        db.PendenciasCadastro.Add(new PendenciaCadastro
        {
            Id = Guid.CreateVersion7(),
            ConversaId = ctx.Conversa.Id,
            TelefoneCanonical = estado.TelefoneCanonical,
            PacienteId = estado.PacienteId,
            Tipo = TipoPendenciaCadastro.NumeroErrado,
            Vinculo = VinculoContato.NaoInformado,
            Observacao = "Verificação cadastral: a pessoa negou ser o paciente na confirmação do nome.",
            Status = StatusPendenciaCadastro.Aberta,
            CriadoEm = DateTime.UtcNow,
        });
        db.VerificacoesCadastraisEstado.Remove(estado);

        await ResponderAsync(ctx,
            "Sem problemas, obrigado por avisar! Este número não será usado para essa pessoa. A equipe "
            + "de cadastro vai revisar o contato.", ct);
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
        whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical, texto, pacienteId: ctx.PacienteId, ct: ct);

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
