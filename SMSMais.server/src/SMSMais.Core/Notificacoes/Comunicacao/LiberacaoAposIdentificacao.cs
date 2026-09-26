using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Tempo;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.Comunicacao;

/// <summary>
/// O que o paciente recebe depois de se identificar (máquina de verificação do WhatsApp ou
/// ferramenta <c>VerificarCadastro</c> do robô). As duas pontas usam ESTE liberador, para que a
/// frase dita à pessoa seja a do que aconteceu de fato.
///
/// <para>Por que existe: até 25/09/2026 a verificação liberava só a confirmação (finalidade 1) e
/// respondia "Já estou enviando… chegam em instantes" mesmo com ZERO liberadas. Como o LEMBRETE de
/// quem não respondeu (finalidade 4) também passa pelo desafio, a pessoa confirmava os dados e não
/// recebia nada — 878 casos entre 20 e 25/09.</para>
/// </summary>
public interface ILiberacaoAposIdentificacao
{
    /// <summary>
    /// Libera as comunicações retidas do paciente (a pendurada do diálogo + as que esperam
    /// identificação, confirmação ou lembrete), descartando ANTES as que não sairiam. NÃO salva —
    /// participa do <c>SaveChanges</c> do chamador.
    /// </summary>
    /// <param name="penduradaId">A comunicação a que o diálogo respondia (null no robô, que não
    /// tem diálogo próprio).</param>
    Task<LiberacaoResultado> LiberarAsync(Guid pacienteId, Guid? penduradaId, CancellationToken ct = default);
}

/// <summary>O que aconteceu na liberação — é o que escolhe a frase dita à pessoa.</summary>
public enum DesfechoLiberacao
{
    /// <summary>Pelo menos uma comunicação volta para a fila e sai em instantes.</summary>
    Liberou,
    /// <summary>Uma atendente já assumiu o agendamento (menu Confirmações): o automático não volta.</summary>
    AtendenteAssumiu,
    /// <summary>A solicitação foi cancelada ou excluída.</summary>
    AgendamentoCancelado,
    /// <summary>A data do agendamento já passou (ou está chegando agora).</summary>
    AgendamentoPassou,
    /// <summary>A comunicação já tinha saído antes.</summary>
    JaEnviado,
    /// <summary>Não havia nada retido para enviar (ou o que havia deixou de valer por outro motivo).</summary>
    NadaPendente,
}

/// <param name="Liberadas">Quantas comunicações voltaram para a fila.</param>
/// <param name="Desfecho">O desfecho que decide a frase (com zero liberadas, o motivo).</param>
/// <param name="DataAgendadaUtc">Data do agendamento que decidiu o desfecho (a frase do "já passou" a cita).</param>
/// <param name="TelefoneDoEnvio">Para <see cref="DesfechoLiberacao.JaEnviado"/>: o número que recebeu.</param>
/// <param name="Motivo">O motivo em uma frase curta — para o log e para o robô.</param>
/// <param name="LiberadasIds">As comunicações que voltaram para a fila — é por elas que o chamador
/// dispara o ENVIO IMEDIATO (a promessa "chegam em instantes" não pode esperar o worker).</param>
public sealed record LiberacaoResultado(
    int Liberadas, DesfechoLiberacao Desfecho, DateTime? DataAgendadaUtc, string? TelefoneDoEnvio, string Motivo,
    IReadOnlyList<Guid>? LiberadasIdsOuNulo = null)
{
    /// <summary>Nunca nula — o chamador itera sem se preocupar.</summary>
    public IReadOnlyList<Guid> LiberadasIds => LiberadasIdsOuNulo ?? [];
}

public sealed class LiberacaoAposIdentificacao(
    SmsMaisDbContext db,
    Confirmacoes.IConfirmacaoConfiguracaoService regras,
    ILogger<LiberacaoAposIdentificacao> logger) : ILiberacaoAposIdentificacao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Agendamento que começa daqui a menos que isto conta como passado: prometer "chegam em
    /// instantes" a quem já devia estar na unidade não ajuda ninguém.
    /// </summary>
    internal static readonly TimeSpan FolgaAntesDoAgendamento = TimeSpan.FromMinutes(15);

    /// <summary>Motivo gravado na linha que ficou coberta pela vencedora da mesma solicitação.</summary>
    internal const string MotivoCoberta = "Coberta pela confirmação liberada na identificação.";

    /// <summary>Quanto mais para cima, mais o motivo explica o "zero liberadas" à pessoa.</summary>
    private static readonly DesfechoLiberacao[] PrioridadeDoMotivo =
    [
        DesfechoLiberacao.AtendenteAssumiu,
        DesfechoLiberacao.AgendamentoCancelado,
        DesfechoLiberacao.AgendamentoPassou,
        DesfechoLiberacao.JaEnviado,
        DesfechoLiberacao.NadaPendente,
    ];

    private sealed record Motivo(DesfechoLiberacao Desfecho, DateTime? DataAgendada, string? Telefone);

    public async Task<LiberacaoResultado> LiberarAsync(Guid pacienteId, Guid? penduradaId, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var dia = FusoBrasilia.ParaExibicao(agora).ToString("dd/MM", PtBr);

        // Candidatas: a pendurada (e só se for DESTE paciente — o estado pode apontar para outro
        // de um número multi-paciente) + as retidas do mesmo paciente. Confirmação e lembrete: o
        // lembrete de quem não respondeu repete a primeira mensagem e fica retido do mesmo jeito.
        var candidatas = await db.ComunicacoesPaciente
            .Include(n => n.Solicitacao)
            .Where(n => n.PacienteId == pacienteId
                && (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                    || n.Finalidade == FinalidadeComunicacao.LembreteAgendamento)
                && (n.Id == penduradaId || n.Status == StatusComunicacao.AguardandoVerificacaoCadastral))
            .ToListAsync(ct);

        var somenteSisreg = candidatas.Count > 0 && (await regras.ObterAsync(ct)).SomenteSisreg;
        var solicitacoes = candidatas.Where(n => n.SolicitacaoId is not null)
            .Select(n => n.SolicitacaoId!.Value).Distinct().ToList();
        // Uma pessoa está com o agendamento (menu Confirmações): o automático não entra por cima.
        HashSet<Guid> comAtendente = solicitacoes.Count == 0
            ? []
            : (await db.AtendimentosConfirmacao.AsNoTracking()
                .Where(a => solicitacoes.Contains(a.SolicitacaoId) && a.EncerradoEm == null)
                .Select(a => a.SolicitacaoId)
                .ToListAsync(ct)).ToHashSet();

        var motivos = new Dictionary<Guid, Motivo>();
        var elegiveis = new List<ComunicacaoPaciente>();
        var naoLiberadas = 0;
        foreach (var n in candidatas)
        {
            if (n.Status is StatusComunicacao.Enviada or StatusComunicacao.Entregue or StatusComunicacao.Lida)
            {
                // Flag anti-reenvio: o que já saiu não sai de novo.
                motivos[n.Id] = new(DesfechoLiberacao.JaEnviado, n.Solicitacao?.DataAgendada, n.Telefone);
                continue;
            }
            if (n.Status == StatusComunicacao.SubstituidaPorAtendente)
            {
                // Uma pessoa já entrou no circuito: o automático não volta.
                motivos[n.Id] = new(DesfechoLiberacao.AtendenteAssumiu, n.Solicitacao?.DataAgendada, null);
                continue;
            }
            if (n.Status == StatusComunicacao.Dispensada)
            {
                // Já coberta por outra da mesma solicitação.
                motivos[n.Id] = new(DesfechoLiberacao.NadaPendente, n.Solicitacao?.DataAgendada, null);
                continue;
            }

            // A MESMA régua do envio, com folga: o que o envio vai barrar não é prometido.
            var impedimento = ComunicacaoPacienteService.MotivoQueImpedeEnvio(
                n.Finalidade, n.Solicitacao, somenteSisreg, agora.Add(FolgaAntesDoAgendamento));
            if (impedimento is not null)
            {
                Encerrar(n, StatusComunicacao.Falha,
                    $"Identificação concluída em {dia}; não liberada: {Minuscula(impedimento.Motivo)}", agora);
                motivos[n.Id] = new(DesfechoDoImpedimento(impedimento, n.Solicitacao), n.Solicitacao?.DataAgendada, null);
                naoLiberadas++;
                continue;
            }
            if (n.SolicitacaoId is { } sid && comAtendente.Contains(sid))
            {
                Encerrar(n, StatusComunicacao.SubstituidaPorAtendente,
                    $"Identificação concluída em {dia}; não liberada: uma atendente já está cuidando deste agendamento.",
                    agora);
                motivos[n.Id] = new(DesfechoLiberacao.AtendenteAssumiu, n.Solicitacao?.DataAgendada, null);
                naoLiberadas++;
                continue;
            }
            elegiveis.Add(n);
        }

        // Uma mensagem por agendamento: a confirmação (finalidade 1) vence o lembrete da mesma
        // solicitação — as duas sairiam como a mesma confirmação completa, uma atrás da outra.
        var liberadas = new List<Guid>();
        var cobertas = 0;
        foreach (var grupo in elegiveis.GroupBy(n => n.SolicitacaoId ?? n.Id))
        {
            var ordenadas = grupo
                .OrderBy(n => n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento ? 0 : 1)
                .ThenByDescending(n => n.CriadoEm)
                .ToList();
            var vencedora = ordenadas[0];
            vencedora.Status = StatusComunicacao.Pendente;
            vencedora.ProximaTentativaEm = agora;
            vencedora.MotivoFalha = null;
            // Envia mesmo se o carimbo do telefone no FHIR falhar (anti-loop de desafio).
            vencedora.IgnorarVerificacaoTelefone = true;
            // O paciente acabou de se identificar e está esperando a resposta: sai mesmo fora do horário.
            vencedora.IgnorarJanelaHorario = true;
            vencedora.Tentativas = 0;
            // Os recibos são do envio ANTERIOR (a primeira mensagem curta): sem zerá-los, a linha
            // mostraria "lida" antes de "enviada" e um envio novo que falhasse ainda exibiria a
            // entrega velha como se tivesse chegado.
            vencedora.EnviadoEm = null;
            vencedora.EntregueEm = null;
            vencedora.LidoEm = null;
            vencedora.VisualizadoEm = null;
            vencedora.MensagemWhatsAppId = null;
            vencedora.AtualizadoEm = agora;
            liberadas.Add(vencedora.Id);

            foreach (var outra in ordenadas.Skip(1))
            {
                Encerrar(outra, StatusComunicacao.Dispensada, MotivoCoberta, agora);
                cobertas++;
            }
        }

        var resultado = liberadas.Count > 0
            ? new LiberacaoResultado(liberadas.Count, DesfechoLiberacao.Liberou, null, null,
                $"{liberadas.Count} comunicação(ões) liberada(s)", liberadas)
            : SemLiberacao(motivos, penduradaId);

        logger.LogInformation(
            "Liberação após identificação do paciente {Paciente}: {Liberadas} liberada(s), {Cobertas} coberta(s), "
            + "{NaoLiberadas} não liberada(s) de {Candidatas} candidata(s); desfecho {Desfecho} ({Motivo}).",
            pacienteId, liberadas.Count, cobertas, naoLiberadas, candidatas.Count, resultado.Desfecho, resultado.Motivo);
        return resultado;
    }

    /// <summary>
    /// Zero liberadas: o motivo que se diz é o da comunicação que a pessoa estava respondendo (a
    /// pendurada); sem ela, o mais explicativo entre os encontrados.
    /// </summary>
    private static LiberacaoResultado SemLiberacao(Dictionary<Guid, Motivo> motivos, Guid? penduradaId)
    {
        var motivo = penduradaId is { } p && motivos.TryGetValue(p, out var daPendurada)
            ? daPendurada
            : motivos.Values.OrderBy(m => Array.IndexOf(PrioridadeDoMotivo, m.Desfecho)).FirstOrDefault()
              ?? new Motivo(DesfechoLiberacao.NadaPendente, null, null);

        // "Já passou" sem data não tem como ser dito: vira "não há aviso pendente".
        if (motivo.Desfecho == DesfechoLiberacao.AgendamentoPassou && motivo.DataAgendada is null)
            motivo = motivo with { Desfecho = DesfechoLiberacao.NadaPendente };

        var texto = motivo.Desfecho switch
        {
            DesfechoLiberacao.AtendenteAssumiu => "uma atendente já está cuidando do agendamento",
            DesfechoLiberacao.AgendamentoCancelado => "o agendamento foi cancelado",
            DesfechoLiberacao.AgendamentoPassou => "a data do agendamento já passou",
            DesfechoLiberacao.JaEnviado => "as informações do agendamento já tinham sido enviadas",
            _ => "não há aviso pendente para o paciente",
        };
        return new LiberacaoResultado(0, motivo.Desfecho, motivo.DataAgendada, motivo.Telefone, texto);
    }

    private static DesfechoLiberacao DesfechoDoImpedimento(ComunicacaoPacienteService.Impedimento i, Solicitacao? s)
        => i.Tipo switch
        {
            ComunicacaoPacienteService.ImpedimentoEnvio.SolicitacaoEncerrada => DesfechoLiberacao.AgendamentoCancelado,
            ComunicacaoPacienteService.ImpedimentoEnvio.SemDataFutura when s?.DataAgendada is not null
                => DesfechoLiberacao.AgendamentoPassou,
            _ => DesfechoLiberacao.NadaPendente,
        };

    /// <summary>Fim da linha para a comunicação: o motivo fica, e o worker não a pega mais.</summary>
    private static void Encerrar(ComunicacaoPaciente n, StatusComunicacao status, string motivo, DateTime agora)
    {
        n.Status = status;
        n.MotivoFalha = motivo.Length <= 1000 ? motivo : motivo[..1000];
        n.ProximaTentativaEm = null;
        n.AtualizadoEm = agora;
    }

    private static string Minuscula(string s) =>
        s.Length == 0 ? s : char.ToLower(s[0], PtBr) + s[1..];
}
