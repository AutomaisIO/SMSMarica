using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Auditoria.Dtos;
using SMSMarica.Core.Identidade;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Auditoria;

public sealed class AuditoriaService(SmsMaricaDbContext db, IUsuarioAtualAccessor usuarioAtual) : IAuditoriaService
{
    private const int TamanhoMaximo = 200;

    public async Task RegistrarAsync(
        string entidade,
        string entidadeId,
        string acao,
        string? valorAnterior,
        string? valorNovo,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = usuarioAtual.UsuarioId;
        var usuarioNome = usuarioId is null
            ? null
            : await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.NomeCompleto)
                .FirstOrDefaultAsync(cancellationToken);

        db.RegistrosAuditoria.Add(new RegistroAuditoria
        {
            Id = Guid.CreateVersion7(),
            Entidade = entidade,
            EntidadeId = entidadeId,
            Acao = acao,
            ValorAnterior = valorAnterior,
            ValorNovo = valorNovo,
            UsuarioId = usuarioId,
            UsuarioNome = usuarioNome,
            CriadoEm = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginaAuditoriaDto> BuscarAsync(AuditoriaFiltroDto filtro, CancellationToken cancellationToken = default)
    {
        var q = db.RegistrosAuditoria.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Entidade))
            q = q.Where(r => r.Entidade == filtro.Entidade);
        if (!string.IsNullOrWhiteSpace(filtro.EntidadeId))
            q = q.Where(r => r.EntidadeId == filtro.EntidadeId);
        if (filtro.UsuarioId is { } uid)
            q = q.Where(r => r.UsuarioId == uid);
        if (filtro.De is { } de)
            q = q.Where(r => r.CriadoEm >= de);
        if (filtro.Ate is { } ate)
            q = q.Where(r => r.CriadoEm <= ate);
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var padrao = $"%{filtro.Texto.Trim()}%";
            q = q.Where(r =>
                EF.Functions.ILike(r.ValorAnterior ?? string.Empty, padrao) ||
                EF.Functions.ILike(r.ValorNovo ?? string.Empty, padrao) ||
                EF.Functions.ILike(r.UsuarioNome ?? string.Empty, padrao) ||
                EF.Functions.ILike(r.Acao, padrao));
        }

        var total = await q.CountAsync(cancellationToken);

        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        var tamanho = Math.Clamp(filtro.Tamanho, 1, TamanhoMaximo);

        var itens = await q
            .OrderByDescending(r => r.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(r => new RegistroAuditoriaDto(
                r.Id, r.Entidade, r.EntidadeId, r.Acao,
                r.ValorAnterior, r.ValorNovo, r.UsuarioId, r.UsuarioNome, r.CriadoEm))
            .ToListAsync(cancellationToken);

        return new PaginaAuditoriaDto(itens, total, pagina, tamanho);
    }
}
