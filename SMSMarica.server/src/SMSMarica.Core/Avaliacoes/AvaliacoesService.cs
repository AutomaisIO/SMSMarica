using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Avaliacoes.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Avaliacoes;

public sealed class AvaliacoesService(SmsMaisDbContext db) : IAvaliacoesService
{
    private readonly SmsMaisDbContext _db = db;

    public async Task<IReadOnlyList<AvaliacaoListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var avaliacoes = await _db.Avaliacoes.AsNoTracking()
            .OrderByDescending(a => a.CriadoEm)
            .ToListAsync(cancellationToken);
        return [.. avaliacoes.Select(AvaliacoesMapper.ParaListItem)];
    }

    public async Task<AvaliacaoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await _db.Avaliacoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Avaliacao), id);
        return AvaliacoesMapper.ParaDto(a);
    }

    public async Task<Guid> RegistrarAsync(RegistrarAvaliacaoRequest request, CancellationToken cancellationToken = default)
    {
        var sessaoExiste = await _db.Sessoes.AsNoTracking()
            .AnyAsync(s => s.Id == request.SessaoId, cancellationToken);
        if (!sessaoExiste)
        {
            throw new NaoEncontradoException(nameof(SessaoDeTratamento), request.SessaoId);
        }

        var jaAvaliada = await _db.Avaliacoes.AsNoTracking()
            .AnyAsync(a => a.SessaoId == request.SessaoId, cancellationToken);
        if (jaAvaliada)
        {
            throw new ConflitoException("avaliacao.ja_registrada", "Esta sessão já foi avaliada.");
        }

        var a = new Avaliacao
        {
            Id = Guid.CreateVersion7(),
            SessaoId = request.SessaoId,
            Nota = request.Nota,
            Comentario = string.IsNullOrWhiteSpace(request.Comentario) ? null : request.Comentario.Trim(),
            CriadoEm = DateTime.UtcNow,
        };

        _db.Avaliacoes.Add(a);
        await _db.SaveChangesAsync(cancellationToken);
        return a.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarAvaliacaoRequest request, CancellationToken cancellationToken = default)
    {
        var a = await _db.Avaliacoes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Avaliacao), id);

        a.Nota = request.Nota;
        a.Comentario = string.IsNullOrWhiteSpace(request.Comentario) ? null : request.Comentario.Trim();

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await _db.Avaliacoes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Avaliacao), id);

        _db.Avaliacoes.Remove(a);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
