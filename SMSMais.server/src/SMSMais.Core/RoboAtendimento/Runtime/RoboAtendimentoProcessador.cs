using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Alertas;
using SMSMais.Core.Conversas;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>Processa UMA tarefa da fila do robô (chamado pelo worker, um escopo por tarefa).</summary>
public interface IRoboAtendimentoProcessador
{
    Task ProcessarAsync(Guid tarefaId, CancellationToken ct);
}

public sealed class RoboAtendimentoProcessador(
    SmsMaisDbContext db,
    IRoboClassificador classificador,
    IRoboAtendimentoMotor motor,
    IWhatsAppCliente whats,
    IConversaNotificador notificador,
    IAlertaPlataforma alerta,
    PendenciasCadastro.IContatoNegadoService contatosNegados,
    ILogger<RoboAtendimentoProcessador> logger) : IRoboAtendimentoProcessador
{
    private const int MaxTentativas = 3;
    private const int HistoricoTurnos = 12;

    public async Task ProcessarAsync(Guid tarefaId, CancellationToken ct)
    {
        var tarefa = await db.RoboTarefas.FirstOrDefaultAsync(t => t.Id == tarefaId, ct);
        if (tarefa is null || tarefa.Status != StatusRoboTarefa.Pendente) return;

        try
        {
            await ProcessarInternoAsync(tarefa, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao processar tarefa {Tarefa} do robô.", tarefaId);

            // Foi assim que o crédito acabou sem ninguém saber: cada tarefa só virava este Warning.
            // Falha de CONTA da IA (crédito/chave) já sai pelo FalhaContaIaHandler, com o texto
            // certo — aqui fica o resto (modelo fora, erro de ferramenta, banco…).
            if (!FalhaContaIa.EhFalhaDeConta(ex.Message))
            {
                alerta.Reportar(new EventoAlerta(
                    AlertaCatalogo.RoboFalha,
                    "O robô não conseguiu responder",
                    $"Tarefa {tarefaId} (tentativa {tarefa.Tentativas + 1} de {MaxTentativas}).\n\n"
                    + $"{ex.GetType().Name}: {ex.Message}"));
            }

            await ReagendarOuFalharAsync(tarefa, ex.Message, ct);
        }
    }

    private async Task ProcessarInternoAsync(RoboAtendimentoTarefa tarefa, CancellationToken ct)
    {
        var cfg = await db.RoboConfiguracoes.FirstOrDefaultAsync(ct);
        if (cfg is null || !cfg.Ativo)
        {
            await FinalizarAsync(tarefa, StatusRoboTarefa.Concluida, "Robô desligado.", ct);
            return;
        }

        var conversa = await db.Conversas.FirstOrDefaultAsync(c => c.Id == tarefa.ConversaId, ct);
        var mensagem = await db.MensagensWhatsApp.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == tarefa.MensagemWhatsAppId, ct);
        if (conversa is null || mensagem is null)
        {
            await FinalizarAsync(tarefa, StatusRoboTarefa.Falha, "Conversa/mensagem ausente.", ct);
            return;
        }

        // Bloqueio forte por conversa (operador parou o robô) — vence tudo, inclusive a virada de horário.
        if (conversa.RoboBloqueado)
        {
            await FinalizarAsync(tarefa, StatusRoboTarefa.HandOff, "Robô bloqueado nesta conversa.", ct);
            return;
        }

        var ancora = TravaHumano.AncoraEfetiva(conversa.JanelaAbertaEm, mensagem.OcorridoEm, conversa.RoboRearmadoEm);
        // Fora do expediente dos atendentes o robô assume mesmo com humano na sessão — MAS recua se
        // um atendente agiu recentemente (corte = agora−recência). Dentro do expediente, a trava
        // humano-por-janela vale a janela toda (corte = âncora).
        var foraExpediente = TravaHumano.ForaDoExpedienteHumano(
            cfg.HoraAtendimentoHumanoInicio, cfg.HoraAtendimentoHumanoFim, DateTime.UtcNow,
            cfg.DiasSemanaAtendimentoHumano);
        var corteHumano = TravaHumano.CorteHumano(ancora, foraExpediente, DateTime.UtcNow);

        // Trava humano (1ª checagem).
        if (await HumanoNaJanelaAsync(conversa.Id, corteHumano, ct))
        {
            await FinalizarAsync(tarefa, StatusRoboTarefa.HandOff, "Humano atuou (dentro do corte).", ct);
            return;
        }

        var texto = mensagem.Conteudo ?? string.Empty;

        // AUTO-RESPOSTA de outro WhatsApp Business ("X agradece seu contato. Como podemos ajudar?",
        // "responderemos assim que possível"): não é gente. Responder dispara a auto-resposta do
        // outro lado e vira laço bot-a-bot até o teto de interações — na amostra real de 14 dias há
        // 17 destas, de comércios e consultórios. A proteção tem de ser AQUI, antes do modelo:
        // na simulação, o Haiku só não caiu no laço por conta própria, desobedecendo a regra que
        // manda responder com pergunta.
        if (PareceAutoResposta(texto))
        {
            await FinalizarAsync(tarefa, StatusRoboTarefa.HandOff,
                "Auto-resposta de outro sistema detectada — não respondido.", ct);
            return;
        }

        // Classificação por ESTADO tem prioridade: se há um desafio cadastral pendente, a resposta
        // (dígitos do CPF / botões) é do assunto "Verificação cadastral". O desafio é buscado por
        // PACIENTE quando a conversa está resolvida, MAS também por TELEFONE — o caso comum é o
        // número NÃO estar amarrado a ninguém (é o motivo do desafio), então conversa.PacienteId é nulo.
        var aguardandoCadastral =
            (conversa.PacienteId is { } pid && await AguardandoVerificacaoCadastralAsync(pid, ct))
            || await AguardandoVerificacaoCadastralPorTelefoneAsync(conversa.TelefoneCanonical, ct);
        var assunto = await classificador.ResolverAsync(
            aguardandoCadastral ? RoboAssuntosPadrao.VerificacaoCadastralId : null, texto, ct);

        var maxInteracoes = assunto?.MaxInteracoesSemResolver ?? 20;
        if (conversa.RoboInteracoesNaJanela >= maxInteracoes)
        {
            // Bateu o teto de interações. Antes isso era silêncio ABSOLUTO: o cidadão seguia
            // escrevendo ("Ta aí?") e ninguém respondia. Agora ele é avisado UMA vez — e a
            // `MensagemHandOff`, que existia na tela e nunca era enviada por ninguém, finalmente
            // serve para alguma coisa. O "uma vez" sai de graça do próprio contador: só o turno que
            // cruza o limite avisa; os seguintes já entram com o contador maior e ficam quietos.
            if (conversa.RoboInteracoesNaJanela == maxInteracoes)
            {
                // Fora do expediente não há atendente para prometer — aí a orientação certa é
                // voltar no horário. `MensagemForaHorario` também estava na tela sem uso nenhum.
                var configurada = foraExpediente ? cfg.MensagemForaHorario : cfg.MensagemHandOff;
                var aviso = !string.IsNullOrWhiteSpace(configurada)
                    ? configurada!.Trim()
                    : foraExpediente
                        ? "No momento estamos fora do horário de atendimento. Retorne o contato em "
                          + "horário comercial que a nossa equipe segue com você por aqui."
                        : "A partir daqui um atendente da nossa equipe continua com você por aqui.";
                try
                {
                    await EnviarComoRoboAsync(conversa, SanitizarWhatsApp(aviso), cfg.NomeExibicao, ct);
                    conversa.RoboInteracoesNaJanela += 1; // fecha o assunto: daqui pra frente, silêncio
                    conversa.AtualizadoEm = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Falha ao avisar o cidadão do hand-off na conversa {Conversa}.", conversa.Id);
                }
            }
            await FinalizarAsync(tarefa, StatusRoboTarefa.HandOff, "Limite de interações atingido.", ct);
            return;
        }

        // "Dentro do horário" para o robô = há ATENDENTE HUMANO disponível agora. Fora do expediente
        // humano não há para quem encaminhar — o robô não pode oferecer atendente e deve orientar a
        // voltar no horário.
        var dentroHorario = !foraExpediente && (assunto is null || RoboPrompt.DentroDoHorario(assunto));
        var urlApp = await db.Instituicoes.AsNoTracking().Select(i => i.UrlApp).FirstOrDefaultAsync(ct);
        // Última resposta permitida: o robô já prepara a pessoa para a passagem, em vez de sumir.
        var pertoDoLimite = conversa.RoboInteracoesNaJanela == maxInteracoes - 1;
        // Base SEMPRE + os habilitados no assunto. Sem assunto, o robô ficava sem ferramenta
        // nenhuma e "verificava" identidade no vazio (ver ComandoRoboCatalogo.Base).
        var comandos = MontarComandos(assunto);
        // LGPD: se o dono deste número já disse que NÃO conhece o paciente amarrado à conversa,
        // o robô não trata a conversa como sendo dele — nada daquele cadastro pode ser revelado
        // aqui. Os comandos caem na busca por telefone, que também filtra os negados.
        var pacienteDaConversa = conversa.PacienteId is { } pidConversa
            && await contatosNegados.BloqueadoAsync(conversa.TelefoneCanonical, pidConversa, ct)
                ? null
                : conversa.PacienteId;

        var entrada = new EntradaMotorRobo(
            ChaveSessao: conversa.Id.ToString(),
            ConversaId: conversa.Id,
            PacienteId: pacienteDaConversa,
            AssuntoId: assunto?.Id,
            Modelo: string.IsNullOrWhiteSpace(assunto?.Modelo) ? cfg.ModeloPadrao : assunto!.Modelo!,
            InstrucaoSistema: RoboPrompt.MontarInstrucao(cfg.PersonaGlobal, assunto, dentroHorario, urlApp, pertoDoLimite, comandos.Length > 0),
            ComandosHabilitados: comandos,
            Historico: await CarregarHistoricoAsync(conversa.Id, tarefa.MensagemWhatsAppId, ct),
            MensagemAtual: texto,
            DentroDoHorario: dentroHorario);

        var resposta = await motor.ResponderAsync(entrada, ct);

        // Trava humano (2ª checagem — TOCTOU: alguém pode ter assumido enquanto a IA pensava).
        if (await HumanoNaJanelaAsync(conversa.Id, corteHumano, ct))
        {
            await FinalizarAsync(tarefa, StatusRoboTarefa.HandOff, "Humano assumiu durante o processamento.", ct);
            return;
        }

        // Máquina determinística assumiu o telefone enquanto a IA pensava? Não responder por cima.
        if (await db.VerificacoesCadastraisEstado.AsNoTracking().AnyAsync(
                e => e.TelefoneCanonical == conversa.TelefoneCanonical && e.ExpiraEm > DateTime.UtcNow, ct))
        {
            await FinalizarAsync(tarefa, StatusRoboTarefa.HandOff, "Verificação cadastral determinística em andamento.", ct);
            return;
        }

        // Reivindica a tarefa (Processando) e COMMITA antes de enviar. Se o save final falhar por
        // corrida, ela não volta a Pendente e o worker não re-envia — evita mensagem duplicada.
        tarefa.Status = StatusRoboTarefa.Processando;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(resposta.Texto))
            await EnviarComoRoboAsync(conversa, SanitizarWhatsApp(resposta.Texto), cfg.NomeExibicao, ct);

        conversa.RoboAssuntoId = assunto?.Id ?? conversa.RoboAssuntoId;
        conversa.RoboInteracoesNaJanela += 1;
        conversa.AtualizadoEm = DateTime.UtcNow;

        // Hand-off por BAIXA CONFIANÇA. `LimiarConfianca` existia na entidade e na tela desde o
        // início, mas nunca era comparado — a proteção que se supunha ativa nunca funcionou.
        var limiar = assunto?.LimiarConfianca;
        var poucaConfianca = limiar is { } l && resposta.Confianca is { } c && c < l;
        if (poucaConfianca)
        {
            logger.LogInformation(
                "Robô com confiança {Confianca} abaixo do limiar {Limiar} na conversa {Conversa} — hand-off.",
                resposta.Confianca, limiar, conversa.Id);
        }

        tarefa.RoboAssuntoId = assunto?.Id;
        tarefa.ConfiancaUltima = resposta.Confianca;
        tarefa.TokensEntrada = resposta.TokensEntrada;
        tarefa.TokensSaida = resposta.TokensSaida;
        tarefa.CustoUsd = resposta.CustoUsd;
        tarefa.Status = resposta.HandOff || poucaConfianca ? StatusRoboTarefa.HandOff : StatusRoboTarefa.Concluida;
        tarefa.AtualizadoEm = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await notificador.MensagemEnviadaAsync(new ConversaEventoRealtime(
            conversa.Id, conversa.OperadorResponsavelId, conversa.UnidadeId,
            conversa.TelefoneCanonical, conversa.NomeContato, Truncar(resposta.Texto),
            conversa.NaoLidas, conversa.UltimaMensagemEm), ct);
    }

    /// <summary>Envia texto como robô SEM virar dono da conversa (não faz claim, não zera NaoLidas).</summary>
    private async Task EnviarComoRoboAsync(Data.Entities.Conversas.Conversa conversa, string texto, string nomeRobo, CancellationToken ct)
    {
        // O robô só fala porque o cidadão escreveu. O que ele NÃO pode é revelar dado de paciente
        // cujo contato foi negado — isso é barrado antes, na resolução do paciente da conversa.
        var envio = await whats.EnviarTextoAsync(conversa.TelefoneCanonical, texto, pacienteId: conversa.PacienteId,
            ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
        if (!envio.Ok)
            throw new InvalidOperationException($"Falha ao enviar mensagem do robô: {envio.Erro}");

        if (!string.IsNullOrEmpty(envio.WaMessageId))
        {
            var msg = db.MensagensWhatsApp.Local.FirstOrDefault(m => m.WaMessageId == envio.WaMessageId)
                ?? await db.MensagensWhatsApp.FirstOrDefaultAsync(m => m.WaMessageId == envio.WaMessageId, ct);
            if (msg is not null)
            {
                msg.ConversaId = conversa.Id;
                msg.TipoMensagem = TipoMensagem.Robo;
                msg.AutorNomeExibicao = nomeRobo;
                msg.AutorUsuarioId = null; // automação — não vira dono nem "atendente".
            }
        }

        var agora = DateTime.UtcNow;
        conversa.UltimaMensagemEm = agora;
        conversa.UltimaMensagemDirecao = DirecaoMensagem.Saida;
        conversa.UltimaMensagemPreview = Truncar(texto);
        // NÃO zera NaoLidas: o robô "ler" não é o operador ler.
    }

    private async Task<bool> HumanoNaJanelaAsync(Guid conversaId, DateTime ancora, CancellationToken ct)
    {
        var respondeu = await db.MensagensWhatsApp.AsNoTracking().AnyAsync(
            m => m.ConversaId == conversaId && m.Direcao == DirecaoMensagem.Saida
                && m.AutorUsuarioId != null && m.OcorridoEm >= ancora, ct);
        if (respondeu) return true;

        return await db.ConversaEventos.AsNoTracking().AnyAsync(
            e => e.ConversaId == conversaId && e.AtorUsuarioId != null && e.OcorridoEm >= ancora
                && (e.Tipo == TipoEventoConversa.Assumida
                    || e.Tipo == TipoEventoConversa.Transferida
                    || e.Tipo == TipoEventoConversa.EncaminhadaUnidade), ct);
    }

    private Task<bool> AguardandoVerificacaoCadastralAsync(Guid pacienteId, CancellationToken ct) =>
        db.ComunicacoesPaciente.AsNoTracking().AnyAsync(
            n => n.PacienteId == pacienteId
                && n.Status == StatusComunicacao.AguardandoVerificacaoCadastral
                && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento, ct);

    /// <summary>Há desafio cadastral pendente para ESTE telefone? Cobre o caso (comum) do número
    /// não amarrado a paciente. O telefone da comunicação é comparado em forma canônica.</summary>
    private async Task<bool> AguardandoVerificacaoCadastralPorTelefoneAsync(string telefoneCanonical, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(telefoneCanonical)) return false;
        var limite = DateTime.UtcNow.AddDays(-20); // desafio antigo não conta
        var fones = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(n => n.Status == StatusComunicacao.AguardandoVerificacaoCadastral
                && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                && n.Telefone != null && n.CriadoEm >= limite)
            .Select(n => n.Telefone!)
            .ToListAsync(ct);
        return fones.Any(t => TelefoneWhatsApp.Canonizar(t) == telefoneCanonical);
    }

    /// <summary>Ferramentas do turno: o conjunto base (sempre) mais o que o assunto habilitou.</summary>
    private static string[] MontarComandos(RoboAssunto? assunto)
    {
        var lista = new HashSet<ComandoRobo>(ComandoRoboCatalogo.Base);
        if (assunto is not null)
            foreach (var c in assunto.Comandos.Where(c => c.Habilitado)) lista.Add(c.Comando);
        return [.. lista.Select(c => c.ToString())];
    }

    private async Task<IReadOnlyList<MensagemHistoricoRobo>> CarregarHistoricoAsync(
        Guid conversaId, Guid excluirId, CancellationToken ct)
    {
        var recentes = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.ConversaId == conversaId && m.Id != excluirId
                && m.TipoMensagem != TipoMensagem.NotaInterna && m.Conteudo != null)
            .OrderByDescending(m => m.OcorridoEm)
            .Take(HistoricoTurnos)
            .Select(m => new { m.Direcao, m.TipoMensagem, m.AutorUsuarioId, m.Conteudo })
            .ToListAsync(ct);
        recentes.Reverse();

        return [.. recentes.Select(m => new MensagemHistoricoRobo(Papel(m.Direcao, m.TipoMensagem, m.AutorUsuarioId), m.Conteudo!))];
    }

    private static string Papel(DirecaoMensagem direcao, TipoMensagem? tipo, Guid? autor)
    {
        if (direcao == DirecaoMensagem.Entrada) return "cidadao";
        if (tipo == TipoMensagem.Robo) return "robo";
        if (autor != null) return "atendente";
        return "sistema";
    }

    /// <summary>Heurística de auto-resposta de WhatsApp Business (bot do outro lado). Padrões
    /// observados no tráfego real; exige mensagem com algum corpo para não pegar frase de gente.</summary>
    private static bool PareceAutoResposta(string texto)
    {
        if (texto.Length < 30) return false;
        var t = RoboClassificador.NormalizarTexto(texto);
        return t.Contains("agradece seu contato", StringComparison.Ordinal)
            || t.Contains("responderemos assim que possivel", StringComparison.Ordinal)
            || t.Contains("nao estamos disponiveis no momento", StringComparison.Ordinal)
            || t.Contains("mensagem automatica", StringComparison.Ordinal)
            || (t.Contains("fora do horario de atendimento", StringComparison.Ordinal)
                && t.Contains("deixe sua mensagem", StringComparison.Ordinal));
    }

    /// <summary>Converte negrito markdown (**x**) para o do WhatsApp (*x*) e colapsa asteriscos duplicados.</summary>
    private static string SanitizarWhatsApp(string texto) =>
        System.Text.RegularExpressions.Regex.Replace(texto, @"\*{2,}", "*");

    private async Task FinalizarAsync(RoboAtendimentoTarefa tarefa, StatusRoboTarefa status, string nota, CancellationToken ct)
    {
        tarefa.Status = status;
        tarefa.Erro = status is StatusRoboTarefa.Falha ? nota : tarefa.Erro;
        tarefa.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task ReagendarOuFalharAsync(RoboAtendimentoTarefa tarefa, string erro, CancellationToken ct)
    {
        // Recarrega o estado limpo (o SaveChanges pode ter falhado no meio).
        db.ChangeTracker.Clear();
        var t = await db.RoboTarefas.FirstOrDefaultAsync(x => x.Id == tarefa.Id, ct);
        if (t is null || t.Status != StatusRoboTarefa.Pendente) return;

        t.Tentativas += 1;
        t.Erro = Truncar(erro);
        t.AtualizadoEm = DateTime.UtcNow;
        if (t.Tentativas >= MaxTentativas)
        {
            t.Status = StatusRoboTarefa.Falha;
        }
        else
        {
            var minutos = Math.Pow(2, t.Tentativas) * 2; // 4, 8, 16 min
            t.ProximaTentativaEm = DateTime.UtcNow.AddMinutes(minutos);
        }
        await db.SaveChangesAsync(ct);
    }

    private static string Truncar(string? s) => s is null ? string.Empty : s.Length <= 200 ? s : s[..200];
}
