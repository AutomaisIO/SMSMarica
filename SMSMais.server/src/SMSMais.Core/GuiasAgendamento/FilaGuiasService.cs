using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.GuiasAgendamento;

/// <summary>O que aconteceu com a vez de um sistema na fila.</summary>
public enum DesfechoFilaGuia { FilaVazia, Obtida, Falhou, Adiada, Descartada }

/// <summary>A guia do agendamento vigente de um pedido, para a tela do pedido.</summary>
/// <param name="Situacao">Pendente (na fila), Obtida, Falhou ou Descartada.</param>
/// <param name="DataAgendamento">O dia do agendamento a que a guia pertence.</param>
/// <param name="UltimoErro">Por que não veio, quando não veio.</param>
public sealed record GuiaDoAgendamentoDto(
    string Situacao,
    DateOnly DataAgendamento,
    DateTime? ObtidaEm,
    int Tentativas,
    string? UltimoErro,
    DateTime? ProximaTentativaEm);

public interface IFilaGuiasService
{
    /// <summary>
    /// Põe na fila, vazia e sem arquivo, a guia de todo agendamento de hoje em diante que ainda não
    /// tem uma — SER/SERNIT/ESUS pelo <c>agendamento_externo</c>, SISREG pela solicitação agendada — e
    /// tira da fila (Descartada) o que deixou de valer. Só banco, nenhuma requisição externa.
    /// </summary>
    Task<int> EnfileirarAsync(CancellationToken ct);

    /// <summary>Busca a PRÓXIMA guia do sistema (o atendimento mais perto primeiro) e a guarda no
    /// acervo do paciente. Uma por chamada: o ritmo é de quem chama (<see cref="FilaGuiasScheduler"/>).</summary>
    Task<DesfechoFilaGuia> ProcessarProximaAsync(SistemaRegulacao sistema, CancellationToken ct);

    /// <summary>A guia do agendamento vigente do pedido no espelho (SER/SERNIT/ESUS).</summary>
    Task<GuiaDoAgendamentoDto?> SituacaoPorEspelhoAsync(SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct);

    /// <summary>A guia da data atual da solicitação do SISREG.</summary>
    Task<GuiaDoAgendamentoDto?> SituacaoPorSolicitacaoAsync(Guid solicitacaoId, CancellationToken ct);

    /// <summary>O PDF da guia vigente do pedido no espelho — o mesmo documento do acervo.</summary>
    Task<ConteudoAcervo> ConteudoPorEspelhoAsync(SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct);

    /// <summary>O PDF da guia da data atual da solicitação do SISREG — o mesmo documento do acervo.</summary>
    Task<ConteudoAcervo> ConteudoPorSolicitacaoAsync(Guid solicitacaoId, CancellationToken ct);
}

public sealed class FilaGuiasService(
    SmsMaisDbContext db,
    IDocumentosPacienteService acervo,
    IEnumerable<IBaixadorGuia> baixadores,
    ILogger<FilaGuiasService> logger) : IFilaGuiasService
{
    /// <summary>Depois disto a guia fica <see cref="SituacaoGuiaAgendamento.Falhou"/> e sai da fila.</summary>
    internal const int MaximoTentativas = 5;

    // ===================== enfileirar =====================

    public async Task<int> EnfileirarAsync(CancellationToken ct)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var agora = DateTime.UtcNow;

        // SER/SERNIT/ESUS: o agendamento externo já é pedido × data.
        var externos = await db.AgendamentosExternos.AsNoTracking()
            .Where(a => a.Situacao == SituacaoAgendamentoExterno.Ativo && a.Data >= hoje
                        && !db.GuiasAgendamento.Any(g => g.AgendamentoExternoId == a.Id))
            .Select(a => new { a.Id, a.Sistema, a.PacienteId, a.Data })
            .ToListAsync(ct);
        foreach (var a in externos)
        {
            db.GuiasAgendamento.Add(Nova(a.Sistema, a.PacienteId, a.Data, agora, agendamentoExternoId: a.Id));
        }

        // SISREG: a solicitação agendada, uma guia por data (a remarcação pede guia nova).
        var sisreg = await SolicitacoesSisregAgendadasAsync(ct);
        var jaNaFila = (await db.GuiasAgendamento.AsNoTracking()
                .Where(g => g.SolicitacaoId != null && g.DataAtendimento >= hoje)
                .Select(g => new { SolicitacaoId = g.SolicitacaoId!.Value, g.DataAtendimento })
                .ToListAsync(ct))
            .Select(g => (g.SolicitacaoId, g.DataAtendimento))
            .ToHashSet();
        var novasSisreg = 0;
        foreach (var s in sisreg.Where(s => !jaNaFila.Contains((s.Id, s.Data))))
        {
            db.GuiasAgendamento.Add(Nova(SistemaRegulacao.Sisreg, s.PacienteId, s.Data, agora, solicitacaoId: s.Id));
            novasSisreg++;
        }
        await db.SaveChangesAsync(ct);

        // Fora da fila o que deixou de valer — não vale buscar (e, no SER/SERNIT, gastar via) à toa.
        var descartadas = await db.GuiasAgendamento
            .Where(g => g.Situacao == SituacaoGuiaAgendamento.Pendente
                        && (g.DataAtendimento < hoje
                            || (g.AgendamentoExternoId != null
                                && !db.AgendamentosExternos.Any(a => a.Id == g.AgendamentoExternoId
                                                                     && a.Situacao == SituacaoAgendamentoExterno.Ativo))))
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.Situacao, SituacaoGuiaAgendamento.Descartada)
                .SetProperty(g => g.AtualizadoEm, agora), ct);

        var vigentesSisreg = sisreg.Select(s => (s.Id, s.Data)).ToHashSet();
        var pendentesSisreg = await db.GuiasAgendamento
            .Where(g => g.Situacao == SituacaoGuiaAgendamento.Pendente && g.SolicitacaoId != null)
            .ToListAsync(ct);
        foreach (var g in pendentesSisreg.Where(g => !vigentesSisreg.Contains((g.SolicitacaoId!.Value, g.DataAtendimento))))
        {
            g.Situacao = SituacaoGuiaAgendamento.Descartada;
            g.AtualizadoEm = agora;
            descartadas++;
        }
        await db.SaveChangesAsync(ct);

        var novas = externos.Count + novasSisreg;
        if (novas + descartadas > 0)
        {
            logger.LogInformation("Fila de guias: {Novas} nova(s), {Descartadas} descartada(s).", novas, descartadas);
        }
        return novas;
    }

    private static GuiaAgendamento Nova(
        SistemaRegulacao sistema, Guid pacienteId, DateOnly data, DateTime agora,
        Guid? agendamentoExternoId = null, Guid? solicitacaoId = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Sistema = sistema,
        AgendamentoExternoId = agendamentoExternoId,
        SolicitacaoId = solicitacaoId,
        PacienteId = pacienteId,
        DataAtendimento = data,
        CriadoEm = agora,
    };

    private sealed record SolicitacaoAgendada(Guid Id, Guid PacienteId, DateOnly Data);

    /// <summary>
    /// As solicitações do SISREG agendadas de hoje em diante, com o dia em Brasília. Fora: o pedido
    /// sem número do SISREG (o "0000" do extra-SUS não tem ficha lá) e quem já chegou à recepção (a
    /// guia não serve mais) — a mesma régua do comando da chave.
    /// </summary>
    private async Task<List<SolicitacaoAgendada>> SolicitacoesSisregAgendadasAsync(CancellationToken ct)
    {
        var inicioDoDia = FusoBrasilia.InicioDoDiaAtualEmUtc();
        var linhas = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.Status == StatusSolicitacao.Agendada && s.ExcluidoEm == null && s.AutorizadoEm == null
                        && s.CodigoSolicitacao != null && s.CodigoSolicitacao != "0000"
                        && s.DataAgendada >= inicioDoDia)
            .Select(s => new { s.Id, s.PacienteId, s.DataAgendada, s.CodigoSolicitacao })
            .ToListAsync(ct);
        return linhas
            .Where(s => s.CodigoSolicitacao!.Trim().All(char.IsDigit))
            .Select(s => new SolicitacaoAgendada(s.Id, s.PacienteId, DiaEmBrasilia(s.DataAgendada!.Value)))
            .ToList();
    }

    private static DateOnly DiaEmBrasilia(DateTime utc) => DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(utc));

    // ===================== processar =====================

    public async Task<DesfechoFilaGuia> ProcessarProximaAsync(SistemaRegulacao sistema, CancellationToken ct)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var agora = DateTime.UtcNow;
        var guia = await db.GuiasAgendamento
            .Where(g => g.Sistema == sistema && g.Situacao == SituacaoGuiaAgendamento.Pendente
                        && g.DataAtendimento >= hoje
                        && (g.ProximaTentativaEm == null || g.ProximaTentativaEm <= agora))
            .OrderBy(g => g.DataAtendimento)
            .ThenBy(g => g.CriadoEm)
            .FirstOrDefaultAsync(ct);
        if (guia is null) return DesfechoFilaGuia.FilaVazia;

        if (!await AindaValeAsync(guia, ct))
        {
            guia.Situacao = SituacaoGuiaAgendamento.Descartada;
            guia.AtualizadoEm = agora;
            await db.SaveChangesAsync(ct);
            return DesfechoFilaGuia.Descartada;
        }

        // Idempotência: se uma vez anterior trouxe e guardou o PDF mas caiu antes de marcar a guia,
        // o documento já está no acervo — liga-se a ele em vez de buscar (e gastar via) de novo.
        var referencia = ReferenciaNoAcervo(guia.Id);
        var jaGuardado = await db.DocumentosPaciente.AsNoTracking()
            .Where(d => d.PacienteId == guia.PacienteId && d.OrigemReferencia == referencia && d.ExcluidoEm == null)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(ct);
        if (jaGuardado is { } existente)
        {
            Marcar(guia, existente);
            await db.SaveChangesAsync(ct);
            return DesfechoFilaGuia.Obtida;
        }

        var baixador = baixadores.FirstOrDefault(b => b.Sistema == sistema)
            ?? throw new InvalidOperationException($"Nenhum baixador de guia registrado para {sistema}.");

        // A tentativa é gravada ANTES de ir ao sistema: no SER/SERNIT ela gasta uma via.
        guia.Tentativas++;
        guia.UltimaTentativaEm = DateTime.UtcNow;
        guia.AtualizadoEm = guia.UltimaTentativaEm;
        await db.SaveChangesAsync(ct);

        try
        {
            var baixada = await baixador.BaixarAsync(guia, ct);
            var documento = await acervo.AdicionarAsync(new NovoDocumentoPaciente(
                guia.PacienteId, baixada.Titulo, baixada.Descricao, baixada.NomeArquivo, "application/pdf",
                baixada.Pdf, OrigemDocumentoPaciente.GuiaAgendamento, referencia, SituacaoDocumentoPaciente.Aceito), ct);
            Marcar(guia, documento.Id);
            await db.SaveChangesAsync(ct);
            return DesfechoFilaGuia.Obtida;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (GuiaAdiadaException ex)
        {
            guia.Tentativas--; // não buscou: não conta
            guia.UltimoErro = Cortar(ex.Message);
            guia.ProximaTentativaEm = DateTime.UtcNow + ex.Espera;
            guia.AtualizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return DesfechoFilaGuia.Adiada;
        }
        catch (FilaGuiasPausadaException ex)
        {
            // O sistema todo parou de aceitar (CAPTCHA): a guia não tem culpa — para a fila dele.
            guia.Tentativas--;
            guia.AtualizadoEm = DateTime.UtcNow;
            var config = await db.GuiaFilaConfiguracoes.FirstOrDefaultAsync(x => x.Sistema == sistema, ct);
            if (config is null)
            {
                config = GuiaFilaConfiguracao.Padrao(sistema);
                db.GuiaFilaConfiguracoes.Add(config);
            }
            config.PausadaAte = DateTime.UtcNow + ex.Duracao;
            config.MotivoPausa = Cortar(ex.Message, 300);
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Fila de guias do {Sistema} pausada até {Ate}: {Motivo}", sistema, config.PausadaAte, ex.Message);
            return DesfechoFilaGuia.Adiada;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Guia {Guia} ({Sistema}) não veio — tentativa {Tentativa}.", guia.Id, sistema, guia.Tentativas);
            guia.UltimoErro = Cortar(ex.Message);
            guia.AtualizadoEm = DateTime.UtcNow;
            if (guia.Tentativas >= MaximoTentativas)
            {
                guia.Situacao = SituacaoGuiaAgendamento.Falhou;
                guia.ProximaTentativaEm = null;
            }
            else
            {
                guia.ProximaTentativaEm = DateTime.UtcNow + Espera(guia.Tentativas);
            }
            await db.SaveChangesAsync(ct);
            return DesfechoFilaGuia.Falhou;
        }
    }

    /// <summary>O agendamento ainda é o que a guia diz? (Entre uma varredura e outra ele pode ter mudado.)</summary>
    private async Task<bool> AindaValeAsync(GuiaAgendamento guia, CancellationToken ct)
    {
        if (guia.AgendamentoExternoId is { } externo)
        {
            return await db.AgendamentosExternos.AnyAsync(
                a => a.Id == externo && a.Situacao == SituacaoAgendamentoExterno.Ativo, ct);
        }
        var s = await db.Solicitacoes.AsNoTracking()
            .Where(x => x.Id == guia.SolicitacaoId)
            .Select(x => new { x.Status, x.ExcluidoEm, x.DataAgendada })
            .FirstOrDefaultAsync(ct);
        return s is { Status: StatusSolicitacao.Agendada, ExcluidoEm: null, DataAgendada: { } d }
               && DiaEmBrasilia(d) == guia.DataAtendimento;
    }

    private static void Marcar(GuiaAgendamento guia, Guid documentoId)
    {
        guia.Situacao = SituacaoGuiaAgendamento.Obtida;
        guia.DocumentoPacienteId = documentoId;
        guia.ObtidaEm = DateTime.UtcNow;
        guia.AtualizadoEm = guia.ObtidaEm;
        guia.ProximaTentativaEm = null;
        guia.UltimoErro = null;
    }

    private static string Cortar(string mensagem, int maximo = 500) => mensagem.Length > maximo ? mensagem[..maximo] : mensagem;

    /// <summary>30 min, 1 h, 2 h, 4 h… — o que falha por sessão caída volta logo; o que falha
    /// sempre não martela o sistema de destino.</summary>
    internal static TimeSpan Espera(int tentativas) =>
        TimeSpan.FromMinutes(30 * Math.Pow(2, Math.Max(0, tentativas - 1)));

    internal static string ReferenciaNoAcervo(Guid guiaId) => $"guia-agendamento:{guiaId}";

    // ===================== leitura (tela do pedido) =====================

    public async Task<GuiaDoAgendamentoDto?> SituacaoPorEspelhoAsync(
        SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct) =>
        Dto(await GuiaPorEspelhoAsync(sistema, espelhoId, ct));

    public async Task<GuiaDoAgendamentoDto?> SituacaoPorSolicitacaoAsync(Guid solicitacaoId, CancellationToken ct) =>
        Dto(await GuiaPorSolicitacaoAsync(solicitacaoId, ct));

    public async Task<ConteudoAcervo> ConteudoPorEspelhoAsync(SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct) =>
        await ConteudoAsync(await GuiaPorEspelhoAsync(sistema, espelhoId, ct), espelhoId, ct);

    public async Task<ConteudoAcervo> ConteudoPorSolicitacaoAsync(Guid solicitacaoId, CancellationToken ct) =>
        await ConteudoAsync(await GuiaPorSolicitacaoAsync(solicitacaoId, ct), solicitacaoId, ct);

    private async Task<ConteudoAcervo> ConteudoAsync(GuiaAgendamento? guia, Guid pedidoId, CancellationToken ct)
    {
        if (guia is not { Situacao: SituacaoGuiaAgendamento.Obtida, DocumentoPacienteId: { } documentoId })
        {
            throw new NaoEncontradoException("Guia do agendamento", pedidoId);
        }
        return await acervo.ObterConteudoPorChaveAsync(
            guia.PacienteId, DocumentosPacienteService.Chave(TipoItemAcervo.Documento, documentoId), ct);
    }

    /// <summary>A guia do agendamento que vale para o pedido: o ativo; sem ativo, o mais recente —
    /// mesma régua do quadro "Contato com o paciente".</summary>
    private async Task<GuiaAgendamento?> GuiaPorEspelhoAsync(SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct)
    {
        var vigente = await db.AgendamentosExternos.AsNoTracking()
            .Where(a => a.OrigemId == espelhoId && a.Sistema == sistema)
            .OrderBy(a => a.Situacao == SituacaoAgendamentoExterno.Ativo ? 0 : 1)
            .ThenByDescending(a => a.CriadoEm)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);
        return vigente is null
            ? null
            : await db.GuiasAgendamento.AsNoTracking().FirstOrDefaultAsync(g => g.AgendamentoExternoId == vigente, ct);
    }

    /// <summary>A guia da data em que a solicitação está agendada; sem ela, a mais recente.</summary>
    private async Task<GuiaAgendamento?> GuiaPorSolicitacaoAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var guias = await db.GuiasAgendamento.AsNoTracking()
            .Where(g => g.SolicitacaoId == solicitacaoId)
            .OrderByDescending(g => g.DataAtendimento)
            .ToListAsync(ct);
        if (guias.Count == 0) return null;
        var data = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.Id == solicitacaoId)
            .Select(s => s.DataAgendada)
            .FirstOrDefaultAsync(ct);
        return (data is { } d ? guias.FirstOrDefault(g => g.DataAtendimento == DiaEmBrasilia(d)) : null) ?? guias[0];
    }

    private static GuiaDoAgendamentoDto? Dto(GuiaAgendamento? g) => g is null
        ? null
        : new GuiaDoAgendamentoDto(
            g.Situacao.ToString(), g.DataAtendimento, g.ObtidaEm, g.Tentativas, g.UltimoErro, g.ProximaTentativaEm);
}
