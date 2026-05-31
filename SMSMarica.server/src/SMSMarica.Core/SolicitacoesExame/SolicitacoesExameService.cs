using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

public sealed class SolicitacoesExameService(
    SmsMaricaDbContext db,
    IGeradorIdentificadores geradorIds,
    IDcm4cheeUpsClient upsClient,
    INotificadorExame notificador,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<SolicitacoesExameService> logger)
    : ISolicitacoesExameService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IGeradorIdentificadores _geradorIds = geradorIds;
    private readonly IDcm4cheeUpsClient _upsClient = upsClient;
    private readonly INotificadorExame _notificador = notificador;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;
    private readonly ILogger<SolicitacoesExameService> _logger = logger;

    public async Task<IReadOnlyList<SolicitacaoExameListItemDto>> ListarAsync(
        FiltroSolicitacoesDto filtro,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SolicitacaoExame> query = _db.SolicitacoesExame.AsNoTracking()
            .Include(s => s.TipoExame)
            .Where(s => s.ExcluidoEm == null);

        if (filtro.Status.HasValue) query = query.Where(s => s.Status == filtro.Status);
        if (filtro.PacienteId.HasValue) query = query.Where(s => s.PacienteId == filtro.PacienteId);
        if (filtro.UnidadeId.HasValue) query = query.Where(s => s.UnidadeId == filtro.UnidadeId);
        if (filtro.TipoExameId.HasValue) query = query.Where(s => s.TipoExameId == filtro.TipoExameId);
        if (!string.IsNullOrWhiteSpace(filtro.AccessionNumber))
        {
            var a = filtro.AccessionNumber.Trim();
            query = query.Where(s => s.AccessionNumber == a);
        }
        if (filtro.DataInicial.HasValue)
        {
            var ini = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(s => s.CriadoEm >= ini);
        }
        if (filtro.DataFinal.HasValue)
        {
            var fim = filtro.DataFinal.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(s => s.CriadoEm <= fim);
        }

        var limite = filtro.Limite is <= 0 or > 500 ? 50 : filtro.Limite;
        var lista = await query
            .OrderByDescending(s => s.CriadoEm)
            .Take(limite)
            .ToListAsync(cancellationToken);

        return [.. lista.Select(SolicitacoesExameMapper.ParaListItem)];
    }

    public async Task<SolicitacaoExameDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await CarregarCompletoAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);
        return SolicitacoesExameMapper.ParaDto(s);
    }

    public async Task<SolicitacaoExameDto?> ObterPorAccessionAsync(string accession, CancellationToken cancellationToken = default)
    {
        var a = (accession ?? string.Empty).Trim();
        if (a.Length == 0) return null;
        var s = await CarregarCompletoAsync(x => x.AccessionNumber == a, cancellationToken);
        return s is null ? null : SolicitacoesExameMapper.ParaDto(s);
    }

    public async Task<SolicitacaoExameDto?> ObterPorStudyAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        var u = (studyInstanceUID ?? string.Empty).Trim();
        if (u.Length == 0) return null;
        var s = await CarregarCompletoAsync(x => x.StudyInstanceUID == u, cancellationToken);
        return s is null ? null : SolicitacoesExameMapper.ParaDto(s);
    }

    public async Task<Guid> CadastrarAsync(CadastrarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        await ValidarReferenciasAsync(request.PacienteId, request.TipoExameId, request.UnidadeId, cancellationToken);

        // Solicitante médico é Practitioner no hub FHIR; os dados vão nos snapshots
        // (SolicitanteNome/Crm/UfCrm). Validação de papel Médico foi descontinuada.

        var accession = await _geradorIds.ProximoAccessionAsync(cancellationToken);
        var studyUid = _geradorIds.NovoStudyInstanceUid();
        var agora = DateTime.UtcNow;

        var solicitacao = new SolicitacaoExame
        {
            Id = Guid.CreateVersion7(),
            AccessionNumber = accession,
            StudyInstanceUID = studyUid,

            PacienteId = request.PacienteId,
            TipoExameId = request.TipoExameId,
            UnidadeId = request.UnidadeId,

            SolicitanteUsuarioId = request.SolicitanteUsuarioId,
            SolicitanteNome = request.SolicitanteNome.Trim(),
            SolicitanteCrm = NormalizarDigitos(request.SolicitanteCrm),
            SolicitanteUfCrm = (request.SolicitanteUfCrm ?? string.Empty).Trim().ToUpperInvariant(),

            NumeroRegulacaoSus = NormalizaOpcional(request.NumeroRegulacaoSus),
            Justificativa = NormalizaOpcional(request.Justificativa),

            Status = StatusSolicitacaoExame.Solicitada,
            Prioridade = request.Prioridade,
            Observacoes = NormalizaOpcional(request.Observacoes),
            DataAgendada = request.DataAgendada,

            // Worker pega imediatamente no próximo tick (sem bloquear a resposta da API
            // esperando o PACS responder).
            ProximaTentativaEm = agora,

            CriadoEm = agora,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.SolicitacoesExame.Add(solicitacao);
        await _db.SaveChangesAsync(cancellationToken);

        return solicitacao.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);

        if (s.Status != StatusSolicitacaoExame.Solicitada)
        {
            throw new ConflitoException(
                "solicitacaoExame.imutavel",
                "Solicitação só pode ser editada enquanto está no status 'Solicitada'.");
        }

        await ValidarReferenciasAsync(s.PacienteId, request.TipoExameId, request.UnidadeId, cancellationToken);

        s.TipoExameId = request.TipoExameId;
        s.UnidadeId = request.UnidadeId;
        s.SolicitanteUsuarioId = request.SolicitanteUsuarioId;
        s.SolicitanteNome = request.SolicitanteNome.Trim();
        s.SolicitanteCrm = NormalizarDigitos(request.SolicitanteCrm);
        s.SolicitanteUfCrm = (request.SolicitanteUfCrm ?? string.Empty).Trim().ToUpperInvariant();
        s.NumeroRegulacaoSus = NormalizaOpcional(request.NumeroRegulacaoSus);
        s.Justificativa = NormalizaOpcional(request.Justificativa);
        s.Prioridade = request.Prioridade;
        s.Observacoes = NormalizaOpcional(request.Observacoes);
        s.DataAgendada = request.DataAgendada;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarAsync(Guid id, CancelarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);

        if (s.Status is not (StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Agendada))
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_cancelavel",
                $"Solicitação no status '{s.Status}' não pode ser cancelada.");
        }

        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0)
        {
            throw new ValidacaoException("solicitacaoExame.motivo_obrigatorio", "Motivo do cancelamento é obrigatório.");
        }

        if (!string.IsNullOrEmpty(s.WorklistItemUid))
        {
            await _upsClient.CancelarWorkitemAsync(s.WorklistItemUid, motivo, cancellationToken);
        }

        var agora = DateTime.UtcNow;
        s.Status = StatusSolicitacaoExame.Cancelada;
        s.CanceladoEm = agora;
        s.CanceladoPorUsuarioId = _usuarioAtual.UsuarioId;
        s.MotivoCancelamento = motivo;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReenviarWorklistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);

        if (s.Status is not (StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Enviada))
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_reenviavel",
                $"Só é possível reenviar worklist de solicitações em 'Solicitada' ou 'Enviada'. Atual: {s.Status}.");
        }

        // Apenas agenda o worker pra tentar agora — ele faz o POST/GET e
        // atualiza o status. UX: o front mostra "tentativa em andamento"
        // depois do refresh e o worker resolve em ~30s no pior caso.
        s.ProximaTentativaEm = DateTime.UtcNow;
        s.ErroIntegracaoPacs = null;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);

        if (s.Status is not (StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Cancelada))
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_excluivel",
                "Apenas solicitações em status 'Solicitada' ou 'Cancelada' podem ser excluídas.");
        }

        var agora = DateTime.UtcNow;
        s.ExcluidoEm = agora;
        s.ExcluidoPor = _usuarioAtual.UsuarioId;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarcarComoLaudadaAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        var uid = (studyInstanceUID ?? string.Empty).Trim();
        if (uid.Length == 0) return;

        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(
            x => x.StudyInstanceUID == uid && x.ExcluidoEm == null, cancellationToken);
        if (s is null) return; // study sem solicitação amarrada — ok.

        if (s.Status is StatusSolicitacaoExame.Cancelada or StatusSolicitacaoExame.Laudada) return;

        s.Status = StatusSolicitacaoExame.Laudada;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarcarComoRealizadaAsync(Guid id, DateTime realizadoEm, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken);
        if (s is null) return;
        if (s.Status is StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada or StatusSolicitacaoExame.Cancelada) return;

        s.Status = StatusSolicitacaoExame.Realizada;
        s.RealizadoEm = realizadoEm;
        s.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Notifica (whatsapp/push no futuro; log no MVP).
        await _notificador.NotificarRealizadoAsync(s, cancellationToken);
    }

    public async Task ProcessarTentativaEnvioAsync(Guid solicitacaoId, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame
            .Include(x => x.TipoExame).ThenInclude(t => t!.ProcedimentoSigtap)
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId && x.ExcluidoEm == null, cancellationToken);

        if (s is null) return;

        // Estado terminal ou intermediário que não interessa pro worker.
        if (s.Status is not (StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Enviada))
        {
            // Limpa o agendamento para não voltar.
            if (s.ProximaTentativaEm is not null)
            {
                s.ProximaTentativaEm = null;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var agora = DateTime.UtcNow;
        s.TentativasEnvio += 1;
        s.UltimaTentativaEm = agora;

        try
        {
            if (s.Status == StatusSolicitacaoExame.Solicitada)
            {
                // 1ª etapa: POST UPS-RS.
                var uid = await _upsClient.CriarWorkitemAsync(s, cancellationToken);
                s.WorklistItemUid = uid;
                s.Status = StatusSolicitacaoExame.Enviada;
                s.ErroIntegracaoPacs = null;
                // Agenda confirmação imediata (worker pega no próximo tick para fazer GET).
                s.ProximaTentativaEm = agora;
                s.AtualizadoEm = agora;
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Solicitação {Accession} enviada ao PACS (workitem {Uid}) — aguardando confirmação.",
                    s.AccessionNumber, uid);
            }
            else // Enviada
            {
                // 2ª etapa: GET de confirmação.
                if (string.IsNullOrEmpty(s.WorklistItemUid))
                {
                    // Estado inconsistente — volta pra Solicitada pra reenviar.
                    s.Status = StatusSolicitacaoExame.Solicitada;
                    s.ProximaTentativaEm = agora;
                    s.AtualizadoEm = agora;
                    await _db.SaveChangesAsync(cancellationToken);
                    return;
                }

                var existe = await _upsClient.WorkitemExisteAsync(s.WorklistItemUid, cancellationToken);
                if (existe)
                {
                    s.Status = StatusSolicitacaoExame.Agendada;
                    s.ErroIntegracaoPacs = null;
                    s.ProximaTentativaEm = null; // estado terminal do envio
                    s.AtualizadoEm = agora;
                    await _db.SaveChangesAsync(cancellationToken);
                    await _notificador.NotificarAgendadoAsync(s, cancellationToken);
                    _logger.LogInformation(
                        "Solicitação {Accession} confirmada como Agendada no PACS.",
                        s.AccessionNumber);
                }
                else
                {
                    // GET 404 — workitem sumiu (expirou ou foi limpo). Volta pra Solicitada
                    // e reenvia imediatamente.
                    _logger.LogWarning(
                        "Workitem {Uid} de {Accession} sumiu do PACS — voltando para Solicitada.",
                        s.WorklistItemUid, s.AccessionNumber);
                    s.Status = StatusSolicitacaoExame.Solicitada;
                    s.WorklistItemUid = null;
                    s.ErroIntegracaoPacs = "Workitem não encontrado no PACS (404) — reenviando.";
                    s.ProximaTentativaEm = agora;
                    s.AtualizadoEm = agora;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Falha temporária — agenda nova tentativa com backoff exponencial.
            var espera = CalcularBackoff(s.TentativasEnvio);
            s.ErroIntegracaoPacs = ex.Message;
            s.ProximaTentativaEm = agora.Add(espera);
            s.AtualizadoEm = agora;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex,
                "Tentativa {N} de envio de {Accession} falhou. Próxima em {Espera}.",
                s.TentativasEnvio, s.AccessionNumber, espera);
        }
    }

    // ---- helpers ----

    /// <summary>
    /// Backoff exponencial limitado: 30s, 1min, 2min, 5min, 15min, 30min, 1h, 2h (cap).
    /// </summary>
    private static TimeSpan CalcularBackoff(int tentativas)
    {
        return tentativas switch
        {
            <= 1 => TimeSpan.FromSeconds(30),
            2 => TimeSpan.FromMinutes(1),
            3 => TimeSpan.FromMinutes(2),
            4 => TimeSpan.FromMinutes(5),
            5 => TimeSpan.FromMinutes(15),
            6 => TimeSpan.FromMinutes(30),
            7 => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(2),
        };
    }

    private async Task<SolicitacaoExame?> CarregarCompletoAsync(
        System.Linq.Expressions.Expression<Func<SolicitacaoExame, bool>> filtro,
        CancellationToken cancellationToken)
    {
        return await _db.SolicitacoesExame.AsNoTracking()
            .Include(s => s.TipoExame)
            .Include(s => s.Unidade)
            .Where(s => s.ExcluidoEm == null)
            .FirstOrDefaultAsync(filtro, cancellationToken);
    }

    private async Task ValidarReferenciasAsync(Guid pacienteId, Guid tipoExameId, Guid unidadeId, CancellationToken ct)
    {
        // PacienteId referencia o hub FHIR — validação de existência fica a cargo do hub.
        if (!await _db.TiposExame.AsNoTracking().AnyAsync(t => t.Id == tipoExameId && t.ExcluidoEm == null && t.Ativo, ct))
        {
            throw new NaoEncontradoException(nameof(TipoExame), tipoExameId);
        }

        if (!await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == unidadeId, ct))
        {
            throw new NaoEncontradoException(nameof(Unidade), unidadeId);
        }
    }

    private static string NormalizarDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new([.. valor.Where(char.IsDigit)]);

    private static string? NormalizaOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
