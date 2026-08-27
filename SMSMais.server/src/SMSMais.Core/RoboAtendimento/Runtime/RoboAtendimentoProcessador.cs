using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            cfg.HoraAtendimentoHumanoInicio, cfg.HoraAtendimentoHumanoFim, DateTime.UtcNow);
        var corteHumano = TravaHumano.CorteHumano(ancora, foraExpediente, DateTime.UtcNow);

        // Trava humano (1ª checagem).
        if (await HumanoNaJanelaAsync(conversa.Id, corteHumano, ct))
        {
            await FinalizarAsync(tarefa, StatusRoboTarefa.HandOff, "Humano atuou (dentro do corte).", ct);
            return;
        }

        var texto = mensagem.Conteudo ?? string.Empty;
        // Classificação por ESTADO tem prioridade: se há um desafio cadastral pendente, a resposta
        // (dígitos do CPF / botões) é do assunto "Verificação cadastral". O desafio é buscado por
        // PACIENTE quando a conversa está resolvida, MAS também por TELEFONE — o caso comum é o
        // número NÃO estar amarrado a ninguém (é o motivo do desafio), então conversa.PacienteId é nulo.
        var aguardandoCadastral =
            (conversa.PacienteId is { } pid && await AguardandoVerificacaoCadastralAsync(pid, ct))
            || await AguardandoVerificacaoCadastralPorTelefoneAsync(conversa.TelefoneCanonical, ct);
        Guid? assuntoId = aguardandoCadastral
            ? RoboAssuntosPadrao.VerificacaoCadastralId
            : await classificador.ClassificarAsync(texto, ct);
        RoboAssunto? assunto = assuntoId is { } id
            ? await db.RoboAssuntos.AsNoTracking()
                .Include(a => a.Treinos)
                .Include(a => a.Comandos)
                .FirstOrDefaultAsync(a => a.Id == id && a.Ativo && a.ExcluidoEm == null, ct)
            : null;

        var maxInteracoes = assunto?.MaxInteracoesSemResolver ?? 5;
        if (conversa.RoboInteracoesNaJanela >= maxInteracoes)
        {
            await FinalizarAsync(tarefa, StatusRoboTarefa.HandOff, "Limite de interações atingido.", ct);
            return;
        }

        // "Dentro do horário" para o robô = há ATENDENTE HUMANO disponível agora. Fora do expediente
        // humano não há para quem encaminhar — o robô não pode oferecer atendente e deve orientar a
        // voltar no horário.
        var dentroHorario = !foraExpediente && (assunto is null || DentroDoHorario(assunto));
        var urlApp = await db.Instituicoes.AsNoTracking().Select(i => i.UrlApp).FirstOrDefaultAsync(ct);
        var comandos = assunto is null
            ? Array.Empty<string>()
            : [.. assunto.Comandos.Where(c => c.Habilitado).Select(c => c.Comando.ToString())];
        var entrada = new EntradaMotorRobo(
            ChaveSessao: conversa.Id.ToString(),
            ConversaId: conversa.Id,
            PacienteId: conversa.PacienteId,
            AssuntoId: assunto?.Id,
            Modelo: string.IsNullOrWhiteSpace(assunto?.Modelo) ? cfg.ModeloPadrao : assunto!.Modelo!,
            InstrucaoSistema: MontarInstrucao(cfg.PersonaGlobal, assunto, dentroHorario, urlApp),
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

        tarefa.RoboAssuntoId = assunto?.Id;
        tarefa.ConfiancaUltima = resposta.Confianca;
        tarefa.TokensEntrada = resposta.TokensEntrada;
        tarefa.TokensSaida = resposta.TokensSaida;
        tarefa.CustoUsd = resposta.CustoUsd;
        tarefa.Status = resposta.HandOff ? StatusRoboTarefa.HandOff : StatusRoboTarefa.Concluida;
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
        var envio = await whats.EnviarTextoAsync(conversa.TelefoneCanonical, texto, pacienteId: conversa.PacienteId, ct: ct);
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

    /// <summary>Converte negrito markdown (**x**) para o do WhatsApp (*x*) e colapsa asteriscos duplicados.</summary>
    private static string SanitizarWhatsApp(string texto) =>
        System.Text.RegularExpressions.Regex.Replace(texto, @"\*{2,}", "*");

    private static string MontarInstrucao(string personaGlobal, RoboAssunto? assunto, bool dentroHorario, string? urlApp)
    {
        var sb = new StringBuilder();
        sb.AppendLine(personaGlobal.Trim());
        sb.AppendLine();
        if (assunto is null)
        {
            sb.AppendLine("Você não identificou um assunto específico para esta mensagem. Tente entender, "
                + "de forma cordial, do que a pessoa precisa; se não puder ajudar, encaminhe para um atendente humano.");
        }
        else
        {
            sb.AppendLine($"Assunto: {assunto.Nome}. {assunto.InstrucoesPersona.Trim()}");
            var regras = assunto.Treinos.Where(t => t.Ativo).OrderBy(t => t.Ordem).ToList();
            if (regras.Count > 0)
            {
                sb.AppendLine().AppendLine("Regras (siga cada uma):");
                foreach (var r in regras)
                {
                    var titulo = string.IsNullOrWhiteSpace(r.Titulo) ? string.Empty : $"{r.Titulo.Trim()}: ";
                    sb.AppendLine($"- {titulo}{r.Conteudo.Trim()}");
                }
            }
        }
        sb.AppendLine();
        sb.AppendLine(dentroHorario
            ? "Há atendente humano disponível no horário. NÃO ofereça encaminhar para um atendente por "
              + "conta própria: só encaminhe se a pessoa PEDIR um atendente humano ou se você realmente não "
              + "conseguir resolver — nunca de forma preventiva nem como fecho de cortesia."
            : "ESTAMOS FORA DO HORÁRIO DE ATENDIMENTO HUMANO: não há atendente disponível agora. NÃO ofereça "
              + "nem prometa encaminhar para um atendente. Ajude no que puder; se não resolver, oriente a pessoa "
              + "a procurar o atendimento humano dentro do horário.");
        sb.AppendLine();
        var app = string.IsNullOrWhiteSpace(urlApp) ? "o aplicativo do cidadão da prefeitura" : urlApp!.Trim();
        sb.AppendLine($"AO SE DESPEDIR, sempre oriente a pessoa: acesse {app} — lá ficam os exames, consultas e "
            + "atendimentos que ela já teve na rede municipal; peça para manter os dados sempre atualizados.");
        return sb.ToString();
    }

    private static bool DentroDoHorario(RoboAssunto a)
    {
        // Regra única de fuso: Brasília fixo (UTC-3).
        var agora = DateTime.UtcNow.AddHours(-3);

        if (a.DiasSemana is int mask)
        {
            var bit = (int)agora.DayOfWeek; // domingo = 0
            if ((mask & (1 << bit)) == 0) return false;
        }

        if (a.HorarioInicio is { } ini && a.HorarioFim is { } fim)
        {
            var hora = TimeOnly.FromDateTime(agora);
            if (ini <= fim) return hora >= ini && hora <= fim;
            return hora >= ini || hora <= fim; // vira a meia-noite
        }
        return true;
    }

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
