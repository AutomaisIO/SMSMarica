using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Rastreamento.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Rastreamento;

public sealed class RastreamentoService(SmsMaricaDbContext db) : IRastreamentoService
{
    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<PontoGpsDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var pontos = await _db.PontosGps.AsNoTracking()
            .OrderByDescending(p => p.CapturadoEm)
            .Take(100)
            .ToListAsync(cancellationToken);
        return [.. pontos.Select(RastreamentoMapper.ParaDto)];
    }

    public async Task<Guid> RegistrarPontoAsync(RegistrarPontoGpsRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _db.Motoristas.AsNoTracking().AnyAsync(m => m.Id == request.MotoristaId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Motorista), request.MotoristaId);
        }

        var capturadoEmUtc = request.CapturadoEm.Kind == DateTimeKind.Utc
            ? request.CapturadoEm
            : request.CapturadoEm.ToUniversalTime();

        var p = new PontoGps
        {
            Id = Guid.CreateVersion7(),
            MotoristaId = request.MotoristaId,
            Coordenada = new Gps(request.Latitude, request.Longitude),
            CapturadoEm = capturadoEmUtc,
            CriadoEm = DateTime.UtcNow,
        };

        _db.PontosGps.Add(p);
        await _db.SaveChangesAsync(cancellationToken);
        return p.Id;
    }

    public async Task<IReadOnlyList<PontoGpsDto>> ListarPontosPorMotoristaAsync(
        Guid motoristaId,
        DateTime? desde,
        DateTime? ate,
        CancellationToken cancellationToken = default)
    {
        var query = _db.PontosGps.AsNoTracking().Where(p => p.MotoristaId == motoristaId);
        if (desde is not null) query = query.Where(p => p.CapturadoEm >= desde);
        if (ate is not null) query = query.Where(p => p.CapturadoEm <= ate);

        var pontos = await query
            .OrderBy(p => p.CapturadoEm)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return [.. pontos.Select(RastreamentoMapper.ParaDto)];
    }

    public async Task<IReadOnlyList<GeofenceDto>> ListarGeofencesAsync(CancellationToken cancellationToken = default)
    {
        var geofences = await _db.Geofences.AsNoTracking().ToListAsync(cancellationToken);
        return [.. geofences.Select(RastreamentoMapper.ParaDto)];
    }

    public async Task<GeofenceDto> ObterGeofencePorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var g = await _db.Geofences.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Geofence), id);
        return RastreamentoMapper.ParaDto(g);
    }

    public async Task<Guid> CadastrarGeofenceAsync(CadastrarGeofenceRequest request, CancellationToken cancellationToken = default)
    {
        var g = new Geofence
        {
            Id = Guid.CreateVersion7(),
            Tipo = request.Tipo,
            ReferenciaId = request.ReferenciaId,
            Centro = new Gps(request.Latitude, request.Longitude),
            RaioMetros = request.RaioMetros,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Geofences.Add(g);
        await _db.SaveChangesAsync(cancellationToken);
        return g.Id;
    }

    public async Task AtualizarGeofenceAsync(Guid id, AtualizarGeofenceRequest request, CancellationToken cancellationToken = default)
    {
        var g = await _db.Geofences.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Geofence), id);

        g.Centro = new Gps(request.Latitude, request.Longitude);
        g.RaioMetros = request.RaioMetros;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletarGeofenceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var g = await _db.Geofences.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Geofence), id);

        _db.Geofences.Remove(g);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EventoChegadaDto>> ListarEventosPorRotaAsync(Guid rotaId, CancellationToken cancellationToken = default)
    {
        var eventos = await _db.EventosChegada.AsNoTracking()
            .Where(e => e.RotaDiariaId == rotaId)
            .OrderBy(e => e.OcorridoEm)
            .ToListAsync(cancellationToken);
        return [.. eventos.Select(RastreamentoMapper.ParaDto)];
    }
}
