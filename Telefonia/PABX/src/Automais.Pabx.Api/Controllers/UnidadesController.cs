using Automais.Pabx.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Automais.Pabx.Api.Controllers;

public sealed record UnidadeDto(
    int Id,
    string Nome,
    string Grupo,
    string Lan,
    string TunnelIp,
    string Status,
    string? Endereco,
    string? Gestor,
    int QuantidadeRamais);

[ApiController]
[Route("api/unidades")]
public sealed class UnidadesController(PabxDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<UnidadeDto>> Listar(CancellationToken ct)
    {
        var unidades = await db.Unidades.AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new UnidadeDto(
                u.Id, u.Nome, u.Grupo, u.Lan, u.TunnelIp, u.Status, u.Endereco, u.Gestor, u.Ramais.Count))
            .ToListAsync(ct);
        return unidades;
    }
}
