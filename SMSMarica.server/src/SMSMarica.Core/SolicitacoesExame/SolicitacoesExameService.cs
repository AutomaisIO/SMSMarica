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
            .Include(s => s.Paciente).ThenInclude(p => p!.Usuario)
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

        if (request.SolicitanteUsuarioId.HasValue)
        {
            var existeMedico = await _db.Medicos.AsNoTracking()
                .AnyAsync(m => m.UsuarioId == request.SolicitanteUsuarioId && m.ExcluidoEm == null, cancellationToken);
            if (!existeMedico)
            {
                throw new ValidacaoException(
                    "solicitacaoExame.solicitante_invalido",
                    "Usuário informado não tem papel Médico ativo.");
            }
        }

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

            CriadoEm = agora,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.SolicitacoesExame.Add(solicitacao);
        await _db.SaveChangesAsync(cancellationToken);

        // Tenta criar o workitem no dcm4chee. Falha não anula a solicitação —
        // ela fica como Solicitada com ErroIntegracaoPacs para reenvio.
        await TentarCriarWorkitemAsync(solicitacao.Id, cancellationToken);

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

        if (s.Status != StatusSolicitacaoExame.Solicitada)
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_reenviavel",
                $"Só é possível reenviar worklist de solicitações no status 'Solicitada'. Atual: {s.Status}.");
        }

        await TentarCriarWorkitemAsync(s.Id, cancellationToken);

        // Se ainda houver erro, propaga (o front mostra) — TentarCriarWorkitemAsync grava ErroIntegracaoPacs.
        var atualizada = await _db.SolicitacoesExame.AsNoTracking()
            .FirstAsync(x => x.Id == id, cancellationToken);
        if (atualizada.Status != StatusSolicitacaoExame.Agendada)
        {
            throw new ConflitoException("solicitacaoExame.reenvio_falhou", atualizada.ErroIntegracaoPacs ?? "Falha ao reenviar.");
        }
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

    // ---- helpers ----

    private async Task TentarCriarWorkitemAsync(Guid id, CancellationToken cancellationToken)
    {
        var s = await _db.SolicitacoesExame
            .Include(x => x.Paciente).ThenInclude(p => p!.Usuario)
            .Include(x => x.TipoExame).ThenInclude(t => t!.ProcedimentoSigtap)
            .FirstAsync(x => x.Id == id, cancellationToken);

        try
        {
            var uid = await _upsClient.CriarWorkitemAsync(s, cancellationToken);
            s.WorklistItemUid = uid;
            s.Status = StatusSolicitacaoExame.Agendada;
            s.ErroIntegracaoPacs = null;
            s.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await _notificador.NotificarAgendadoAsync(s, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Falha ao criar worklist item para solicitação {Id}. Fica como Solicitada para reenvio.", id);
            s.ErroIntegracaoPacs = ex.Message;
            s.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<SolicitacaoExame?> CarregarCompletoAsync(
        System.Linq.Expressions.Expression<Func<SolicitacaoExame, bool>> filtro,
        CancellationToken cancellationToken)
    {
        return await _db.SolicitacoesExame.AsNoTracking()
            .Include(s => s.Paciente).ThenInclude(p => p!.Usuario)
            .Include(s => s.TipoExame)
            .Include(s => s.Unidade)
            .Where(s => s.ExcluidoEm == null)
            .FirstOrDefaultAsync(filtro, cancellationToken);
    }

    private async Task ValidarReferenciasAsync(Guid pacienteId, Guid tipoExameId, Guid unidadeId, CancellationToken ct)
    {
        if (!await _db.Pacientes.AsNoTracking().AnyAsync(p => p.Id == pacienteId && p.ExcluidoEm == null, ct))
        {
            throw new NaoEncontradoException(nameof(Paciente), pacienteId);
        }

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
