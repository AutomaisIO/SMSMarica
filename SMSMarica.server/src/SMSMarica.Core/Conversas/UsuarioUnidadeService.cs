using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Conversas;

namespace SMSMarica.Core.Conversas;

public sealed class UsuarioUnidadeService(
    SmsMaricaDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IUsuarioUnidadeService
{
    public async Task<IReadOnlyList<Guid>> ObterUnidadeIdsAsync(Guid usuarioId, CancellationToken ct = default) =>
        await db.UsuarioUnidades.AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId)
            .Select(x => x.UnidadeId)
            .ToListAsync(ct);

    public async Task<Guid?> ObterPrincipalIdAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var vinculos = await db.UsuarioUnidades.AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId)
            .Select(x => new { x.UnidadeId, x.Principal })
            .ToListAsync(ct);

        // Prefere a marcada como principal; senão, se cobre só uma, usa essa.
        return vinculos.FirstOrDefault(x => x.Principal)?.UnidadeId
            ?? (vinculos.Count == 1 ? vinculos[0].UnidadeId : null);
    }

    public async Task<IReadOnlyList<VinculoUnidadeDto>> ObterVinculosAsync(Guid usuarioId, CancellationToken ct = default) =>
        await db.UsuarioUnidades.AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId)
            .OrderByDescending(x => x.Principal)
            .Select(x => new VinculoUnidadeDto(x.UnidadeId, x.Unidade!.Nome, x.Principal))
            .ToListAsync(ct);

    public async Task DefinirVinculosAsync(Guid usuarioId, DefinirVinculosUnidadeRequest request, CancellationToken ct = default)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId && u.ExcluidoEm == null, ct))
            throw new NaoEncontradoException("Usuário", usuarioId);

        if (request.Unidades.Count(x => x.Principal) > 1)
            throw new ValidacaoException("unidades", "Só pode haver uma unidade principal.");

        var unidadeIds = request.Unidades.Select(x => x.UnidadeId).Distinct().ToList();
        if (unidadeIds.Count != request.Unidades.Count)
            throw new ValidacaoException("unidades", "Unidades duplicadas no vínculo.");

        var existentes = await db.Unidades.CountAsync(u => unidadeIds.Contains(u.Id), ct);
        if (existentes != unidadeIds.Count)
            throw new ValidacaoException("unidades", "Uma ou mais unidades não existem.");

        // Substitui o conjunto inteiro (replace-all) — simples e idempotente.
        var atuais = await db.UsuarioUnidades.Where(x => x.UsuarioId == usuarioId).ToListAsync(ct);
        db.UsuarioUnidades.RemoveRange(atuais);

        var agora = DateTime.UtcNow;
        foreach (var item in request.Unidades)
        {
            db.UsuarioUnidades.Add(new UsuarioUnidade
            {
                UsuarioId = usuarioId,
                UnidadeId = item.UnidadeId,
                Principal = item.Principal,
                CriadoEm = agora,
                CriadoPor = usuarioAtual.UsuarioId,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UsuarioDaUnidadeDto>> ListarUsuariosDaUnidadeAsync(Guid unidadeId, CancellationToken ct = default)
    {
        if (!await db.Unidades.AsNoTracking().AnyAsync(u => u.Id == unidadeId, ct))
            throw new NaoEncontradoException("Unidade", unidadeId);

        return await db.UsuarioUnidades.AsNoTracking()
            .Where(x => x.UnidadeId == unidadeId && x.Usuario!.ExcluidoEm == null)
            .OrderBy(x => x.Usuario!.NomeCompleto)
            .Select(x => new UsuarioDaUnidadeDto(
                x.UsuarioId, x.Usuario!.NomeCompleto, x.Usuario!.Email, x.Usuario!.Ativo, x.Principal))
            .ToListAsync(ct);
    }

    public async Task AdicionarUsuarioAsync(Guid unidadeId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await db.Unidades.AsNoTracking().AnyAsync(u => u.Id == unidadeId, ct))
            throw new NaoEncontradoException("Unidade", unidadeId);
        if (!await db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == usuarioId && u.ExcluidoEm == null, ct))
            throw new NaoEncontradoException("Usuário", usuarioId);
        if (await db.UsuarioUnidades.AnyAsync(x => x.UnidadeId == unidadeId && x.UsuarioId == usuarioId, ct))
            return; // idempotente

        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = usuarioId,
            UnidadeId = unidadeId,
            Principal = false,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoverUsuarioAsync(Guid unidadeId, Guid usuarioId, CancellationToken ct = default)
    {
        var vinculo = await db.UsuarioUnidades
            .FirstOrDefaultAsync(x => x.UnidadeId == unidadeId && x.UsuarioId == usuarioId, ct);
        if (vinculo is null) return; // idempotente

        db.UsuarioUnidades.Remove(vinculo);
        await db.SaveChangesAsync(ct);
    }
}
