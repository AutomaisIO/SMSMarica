using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Geo;
using SMSMarica.Core.Unidades.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Unidades;

public sealed class UnidadesService(SmsMaricaDbContext db, IGeocodificadorService geo) : IUnidadesService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IGeocodificadorService _geo = geo;

    public async Task<IReadOnlyList<UnidadeListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var unidades = await _db.Unidades.AsNoTracking()
            .OrderBy(u => u.Nome)
            .ToListAsync(cancellationToken);
        return [.. unidades.Select(UnidadesMapper.ParaListItem)];
    }

    public async Task<UnidadeDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await _db.Unidades.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Unidade), id);
        return UnidadesMapper.ParaDto(u);
    }

    public async Task<Guid> CadastrarAsync(CadastrarUnidadeRequest request, CancellationToken cancellationToken = default)
    {
        var endereco = request.Endereco?.ParaEntidade();
        var u = new Unidade
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome.Trim(),
            Endereco = endereco,
            Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim(),
            Gps = await ResolverGpsAsync(request.Latitude, request.Longitude, endereco, cancellationToken),
            Externa = EhExterna(endereco),
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Unidades.Add(u);
        await _db.SaveChangesAsync(cancellationToken);
        return u.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarUnidadeRequest request, CancellationToken cancellationToken = default)
    {
        var u = await _db.Unidades.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Unidade), id);

        var endereco = request.Endereco?.ParaEntidade();
        u.Nome = request.Nome.Trim();
        u.Endereco = endereco;
        u.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        u.Gps = await ResolverGpsAsync(request.Latitude, request.Longitude, endereco, cancellationToken);
        u.Externa = EhExterna(endereco);
        u.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await _db.Unidades.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Unidade), id);

        if (!u.Ativo)
        {
            throw new ConflitoException("unidade.ja_inativa", "Unidade já está inativa.");
        }

        u.Ativo = false;
        u.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Coordenada manual (pin do gestor) tem prioridade; senão geocodifica o endereço
    /// (best-effort — null não bloqueia o cadastro, entra na fila de revisão).
    /// </summary>
    private async Task<Gps?> ResolverGpsAsync(double? latitude, double? longitude, Endereco? endereco, CancellationToken ct)
    {
        if (latitude is not null && longitude is not null)
        {
            return new Gps(latitude.Value, longitude.Value);
        }
        if (endereco is null) return null;

        var coord = await _geo.GeocodificarAsync(endereco, ct);
        return coord is null ? null : new Gps(coord.Latitude, coord.Longitude);
    }

    private static bool EhExterna(Endereco? endereco) =>
        endereco is not null
        && !string.IsNullOrWhiteSpace(endereco.Cidade)
        && !endereco.Cidade.Trim().ToLowerInvariant().Contains("maric");
}
