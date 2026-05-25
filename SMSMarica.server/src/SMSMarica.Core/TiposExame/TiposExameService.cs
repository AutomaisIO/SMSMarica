using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.TiposExame.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.TiposExame;

public sealed class TiposExameService(SmsMaricaDbContext db, IUsuarioAtualAccessor usuarioAtual) : ITiposExameService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<IReadOnlyList<TipoExameListItemDto>> ListarAsync(
        ModalidadeDicom? modalidade,
        bool incluirInativos,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TipoExame> query = _db.TiposExame.AsNoTracking()
            .Include(t => t.ProcedimentoSigtap)
            .Where(t => t.ExcluidoEm == null);

        if (!incluirInativos)
        {
            query = query.Where(t => t.Ativo);
        }

        if (modalidade.HasValue)
        {
            query = query.Where(t => t.ModalidadeDicom == modalidade);
        }

        var lista = await query
            .OrderBy(t => t.ModalidadeDicom)
            .ThenBy(t => t.Nome)
            .ToListAsync(cancellationToken);

        return [.. lista.Select(TiposExameMapper.ParaListItem)];
    }

    public async Task<TipoExameDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.TiposExame.AsNoTracking()
            .Include(x => x.ProcedimentoSigtap)
            .Include(x => x.UnidadePadrao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(TipoExame), id);

        return TiposExameMapper.ParaDto(t);
    }

    public async Task<Guid> CadastrarAsync(CadastrarTipoExameRequest request, CancellationToken cancellationToken = default)
    {
        var nome = request.Nome.Trim();

        if (await _db.TiposExame.AsNoTracking().AnyAsync(t => t.Nome == nome && t.ExcluidoEm == null, cancellationToken))
        {
            throw new ConflitoException("tipoExame.nome_duplicado", $"Já existe tipo de exame com o nome '{nome}'.");
        }

        await ValidarProcedimentoAsync(request.ProcedimentoSigtapId, cancellationToken);
        await ValidarUnidadeAsync(request.UnidadePadraoId, cancellationToken);

        var agora = DateTime.UtcNow;
        var tipo = new TipoExame
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            ProcedimentoSigtapId = request.ProcedimentoSigtapId,
            ModalidadeDicom = request.ModalidadeDicom,
            RequestedProcedureDescription = request.RequestedProcedureDescription.Trim(),
            ScheduledProcedureStepDescription = request.ScheduledProcedureStepDescription.Trim(),
            CodigosProtocolo = SanearLista(request.CodigosProtocolo),
            TempoEstimadoMinutos = request.TempoEstimadoMinutos,
            UnidadePadraoId = request.UnidadePadraoId,
            Ativo = true,
            CriadoEm = agora,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.TiposExame.Add(tipo);
        await _db.SaveChangesAsync(cancellationToken);
        return tipo.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarTipoExameRequest request, CancellationToken cancellationToken = default)
    {
        var t = await _db.TiposExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(TipoExame), id);

        var nome = request.Nome.Trim();
        if (nome != t.Nome &&
            await _db.TiposExame.AsNoTracking().AnyAsync(x => x.Nome == nome && x.ExcluidoEm == null && x.Id != id, cancellationToken))
        {
            throw new ConflitoException("tipoExame.nome_duplicado", $"Já existe tipo de exame com o nome '{nome}'.");
        }

        await ValidarProcedimentoAsync(request.ProcedimentoSigtapId, cancellationToken);
        await ValidarUnidadeAsync(request.UnidadePadraoId, cancellationToken);

        t.Nome = nome;
        t.ProcedimentoSigtapId = request.ProcedimentoSigtapId;
        t.ModalidadeDicom = request.ModalidadeDicom;
        t.RequestedProcedureDescription = request.RequestedProcedureDescription.Trim();
        t.ScheduledProcedureStepDescription = request.ScheduledProcedureStepDescription.Trim();
        t.CodigosProtocolo = SanearLista(request.CodigosProtocolo);
        t.TempoEstimadoMinutos = request.TempoEstimadoMinutos;
        t.UnidadePadraoId = request.UnidadePadraoId;
        t.Ativo = request.Ativo;
        t.AtualizadoEm = DateTime.UtcNow;
        t.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.TiposExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(TipoExame), id);

        var emUso = await _db.SolicitacoesExame.AsNoTracking()
            .AnyAsync(s => s.TipoExameId == id && s.ExcluidoEm == null, cancellationToken);
        if (emUso)
        {
            // Permitido excluir (soft) mesmo em uso — só impede no caso de querer apagar dura.
            // Snapshot vive no histórico das solicitações que apontam para o tipo.
        }

        var agora = DateTime.UtcNow;
        t.ExcluidoEm = agora;
        t.ExcluidoPor = _usuarioAtual.UsuarioId;
        t.AtualizadoEm = agora;
        t.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidarProcedimentoAsync(Guid procedimentoId, CancellationToken ct)
    {
        var existe = await _db.ProcedimentosSigtap.AsNoTracking().AnyAsync(p => p.Id == procedimentoId, ct);
        if (!existe)
        {
            throw new NaoEncontradoException(nameof(ProcedimentoSigtap), procedimentoId);
        }
    }

    private async Task ValidarUnidadeAsync(Guid? unidadeId, CancellationToken ct)
    {
        if (!unidadeId.HasValue) return;
        var existe = await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == unidadeId, ct);
        if (!existe)
        {
            throw new NaoEncontradoException(nameof(Unidade), unidadeId.Value);
        }
    }

    private static List<string> SanearLista(IReadOnlyList<string>? lista)
    {
        if (lista is null || lista.Count == 0) return [];
        return [.. lista.Select(x => x?.Trim() ?? string.Empty).Where(x => x.Length > 0)];
    }
}
