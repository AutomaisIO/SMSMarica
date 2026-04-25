using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Unidades.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Unidades;

public sealed class UnidadesService(SmsMaricaDbContext db) : IUnidadesService
{
    private readonly SmsMaricaDbContext _db = db;

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
        var u = new Unidade
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome.Trim(),
            Endereco = request.Endereco?.ParaEntidade(),
            Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim(),
            Gps = ConstruirGps(request.Latitude, request.Longitude),
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

        u.Nome = request.Nome.Trim();
        u.Endereco = request.Endereco?.ParaEntidade();
        u.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        u.Gps = ConstruirGps(request.Latitude, request.Longitude);
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

    private static Gps? ConstruirGps(double? latitude, double? longitude) =>
        latitude is null || longitude is null ? null : new Gps(latitude.Value, longitude.Value);
}
