using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Rastreamento.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento;

public sealed class RastreamentoService(
    SmsMaisDbContext db,
    IRastreamentoNotificador notificador,
    Pacientes.Fhir.IPacienteResolver pacienteResolver) : IRastreamentoService
{
    private readonly SmsMaisDbContext _db = db;

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

        // Tempo real: empurra a posição para o painel e quem acompanha o motorista.
        await notificador.PosicaoAtualizadaAsync(
            p.MotoristaId, request.Latitude, request.Longitude, capturadoEmUtc, cancellationToken);

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

    // ---------------- Mapa da frota ao vivo ----------------

    public async Task<IReadOnlyList<FrotaVeiculoDto>> ListarFrotaAsync(DateOnly? data, CancellationToken cancellationToken = default)
    {
        var dia = data ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioDia = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var fimDia = inicioDia.AddDays(1);

        var rotas = await _db.Rotas.AsNoTracking()
            .Where(r => r.Data == dia && r.Status != StatusRota.Cancelada)
            .Include(r => r.Veiculo)
            .Include(r => r.Motorista).ThenInclude(m => m!.Usuario)
            .ToListAsync(cancellationToken);

        var motoristaIds = rotas.Select(r => r.MotoristaId).Distinct().ToList();
        var rotaIds = rotas.Select(r => r.Id).ToList();

        // Última posição por motorista (pontos do dia).
        var pontos = motoristaIds.Count == 0 ? [] : await _db.PontosGps.AsNoTracking()
            .Where(p => motoristaIds.Contains(p.MotoristaId) && p.CapturadoEm >= inicioDia && p.CapturadoEm < fimDia)
            .OrderByDescending(p => p.CapturadoEm)
            .Select(p => new { p.MotoristaId, p.Coordenada.Latitude, p.Coordenada.Longitude, p.CapturadoEm })
            .ToListAsync(cancellationToken);
        var ultimoPorMotorista = pontos
            .GroupBy(p => p.MotoristaId)
            .ToDictionary(g => g.Key, g => g.First());

        // Última posição do dia de cada veículo com tablet (poucos veículos; índice veiculo_id+capturado_em).
        var veiculosComTablet = await _db.DispositivosVeiculo.AsNoTracking()
            .Where(d => d.TokenHash != null)
            .Select(d => d.VeiculoId)
            .ToListAsync(cancellationToken);
        var ultimaDoTablet = new Dictionary<Guid, PosicaoVeiculo>();
        foreach (var veiculoId in veiculosComTablet)
        {
            var ultima = await _db.PosicoesVeiculo.AsNoTracking()
                .Where(p => p.VeiculoId == veiculoId && p.CapturadoEm >= inicioDia && p.CapturadoEm < fimDia)
                .OrderByDescending(p => p.CapturadoEm)
                .FirstOrDefaultAsync(cancellationToken);
            if (ultima is not null) ultimaDoTablet[veiculoId] = ultima;
        }

        // Quantidade de pacientes por rota.
        var pacientesPorRota = rotaIds.Count == 0 ? [] : await _db.Alocacoes.AsNoTracking()
            .Where(a => rotaIds.Contains(a.RotaDiariaId) && a.Tipo == TipoAlocacao.Paciente)
            .GroupBy(a => a.RotaDiariaId)
            .Select(g => new { RotaId = g.Key, Qtd = g.Select(x => x.SessaoId).Distinct().Count() })
            .ToDictionaryAsync(x => x.RotaId, x => x.Qtd, cancellationToken);

        var frota = rotas.Select(r =>
        {
            double? lat = null, lng = null, velocidade = null, rumo = null;
            DateTime? atualizadoEm = null;
            var origem = "motorista";
            if (ultimoPorMotorista.TryGetValue(r.MotoristaId, out var pt))
            {
                lat = pt.Latitude;
                lng = pt.Longitude;
                atualizadoEm = pt.CapturadoEm;
            }
            // O tablet do carro vale quando é mais recente que o app do motorista.
            if (ultimaDoTablet.TryGetValue(r.VeiculoId, out var tb) && (atualizadoEm is null || tb.CapturadoEm > atualizadoEm))
            {
                lat = tb.Coordenada.Latitude;
                lng = tb.Coordenada.Longitude;
                atualizadoEm = tb.CapturadoEm;
                velocidade = tb.VelocidadeKmh;
                rumo = tb.Rumo;
                origem = "tablet";
            }

            return new FrotaVeiculoDto(
                r.Id, r.Status, r.VeiculoId,
                r.Veiculo?.Placa ?? string.Empty,
                r.Veiculo?.Modelo ?? string.Empty,
                r.MotoristaId,
                r.Motorista?.Usuario?.NomeCompleto ?? "Motorista",
                pacientesPorRota.GetValueOrDefault(r.Id, 0),
                lat, lng, atualizadoEm, velocidade, rumo, origem);
        }).ToList();

        // Veículos com tablet que andaram hoje sem rota no dia: aparecem só pelo tablet.
        var veiculosEmRota = rotas.Select(r => r.VeiculoId).ToHashSet();
        var soTablet = ultimaDoTablet.Keys.Where(v => !veiculosEmRota.Contains(v)).ToList();
        if (soTablet.Count > 0)
        {
            var veiculos = await _db.Veiculos.AsNoTracking()
                .Where(v => soTablet.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken);
            foreach (var veiculoId in soTablet)
            {
                var tb = ultimaDoTablet[veiculoId];
                var v = veiculos.GetValueOrDefault(veiculoId);
                frota.Add(new FrotaVeiculoDto(
                    null, null, veiculoId, v?.Placa ?? string.Empty, v?.Modelo ?? string.Empty,
                    null, null, 0,
                    tb.Coordenada.Latitude, tb.Coordenada.Longitude, tb.CapturadoEm,
                    tb.VelocidadeKmh, tb.Rumo, "tablet"));
            }
        }

        return frota;
    }

    // ---------------- FT5: pacientes aguardando + "puxar" ----------------

    public async Task<IReadOnlyList<PacienteAguardandoDto>> ListarAguardandoAsync(
        Guid? motoristaId, CancellationToken cancellationToken = default)
    {
        var dados = await (
            from s in _db.Sessoes.AsNoTracking()
            join t in _db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join u in _db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
            where s.Status == StatusSessao.AguardandoRetorno && u.Externa
            orderby s.DataPrevista
            select new
            {
                s.Id,
                s.TratamentoId,
                t.PacienteId,
                UnidadeId = u.Id,
                UnidadeNome = u.Nome,
                Lat = (double?)u.Gps!.Latitude,
                Lng = (double?)u.Gps!.Longitude,
                Acomp = s.AcompanhanteEsperado,
                s.DataPrevista,
            }).ToListAsync(cancellationToken);

        (double Lat, double Lng)? origem = null;
        if (motoristaId is not null)
        {
            var ultimo = await _db.PontosGps.AsNoTracking()
                .Where(p => p.MotoristaId == motoristaId.Value)
                .OrderByDescending(p => p.CapturadoEm)
                .FirstOrDefaultAsync(cancellationToken);
            if (ultimo is not null) origem = (ultimo.Coordenada.Latitude, ultimo.Coordenada.Longitude);
        }

        var nomes = await pacienteResolver.ResolverManyAsync(dados.Select(d => d.PacienteId), cancellationToken);

        return [.. dados
            .Select(d => new PacienteAguardandoDto(
                d.Id, d.TratamentoId, d.PacienteId,
                nomes.TryGetValue(d.PacienteId, out var r) ? r.Nome : string.Empty,
                d.UnidadeId, d.UnidadeNome, d.Lat, d.Lng,
                origem is not null && d.Lat is not null && d.Lng is not null
                    ? (int)DistanciaMetros(origem.Value.Lat, origem.Value.Lng, d.Lat.Value, d.Lng.Value)
                    : null,
                d.Acomp == true, d.DataPrevista))
            .OrderBy(x => x.DistanciaMetros ?? int.MaxValue)];
    }

    public async Task MarcarAguardandoRetornoAsync(Guid sessaoId, CancellationToken cancellationToken = default)
    {
        var s = await _db.Sessoes.FirstOrDefaultAsync(x => x.Id == sessaoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);
        s.Status = StatusSessao.AguardandoRetorno;
        s.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> PuxarAsync(PuxarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var rota = await _db.Rotas.FirstOrDefaultAsync(r => r.Id == request.RotaId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), request.RotaId);

        if (rota.Status is StatusRota.Concluida or StatusRota.Cancelada)
        {
            throw new ConflitoException("rota.estado_invalido", $"Rota no estado {rota.Status} não aceita puxar pacientes.");
        }

        var sessao = await _db.Sessoes.FirstOrDefaultAsync(s => s.Id == request.SessaoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), request.SessaoId);

        var assentos = await (
            from a in _db.Assentos.AsNoTracking()
            join f in _db.Fileiras.AsNoTracking() on a.FileiraId equals f.Id
            where f.VeiculoId == rota.VeiculoId && !a.Excluido && !a.Bloqueado && a.Tipo != TipoAssento.Motorista
            orderby f.Ordem, a.Numero
            select new { a.Id, a.Tipo }).ToListAsync(cancellationToken);

        var ocupados = await _db.Alocacoes.AsNoTracking()
            .Where(a => a.RotaDiariaId == request.RotaId)
            .Select(a => a.AssentoId)
            .ToListAsync(cancellationToken);

        var livres = assentos.Where(a => !ocupados.Contains(a.Id)).ToList();
        var assentoPaciente = livres.FirstOrDefault(a => a.Tipo == TipoAssento.Passageiro) ?? livres.FirstOrDefault();
        if (assentoPaciente is null)
        {
            throw new ConflitoException("rota.sem_assento", "Não há assento livre nesta rota para puxar o paciente.");
        }

        var maxOrdem = await _db.Alocacoes
            .Where(a => a.RotaDiariaId == request.RotaId)
            .MaxAsync(a => (int?)a.OrdemParada, cancellationToken) ?? 0;
        var ordem = maxOrdem + 1;
        var agora = DateTime.UtcNow;

        var alocPaciente = new Alocacao
        {
            Id = Guid.CreateVersion7(),
            RotaDiariaId = request.RotaId,
            SessaoId = sessao.Id,
            AssentoId = assentoPaciente.Id,
            Tipo = TipoAlocacao.Paciente,
            Parada = TipoParada.Retorno,
            OrdemParada = ordem,
            CriadoEm = agora,
        };
        _db.Alocacoes.Add(alocPaciente);

        if (sessao.AcompanhanteEsperado == true)
        {
            var assentoAcomp = livres.FirstOrDefault(a => a.Id != assentoPaciente.Id && a.Tipo == TipoAssento.Acompanhante)
                ?? livres.FirstOrDefault(a => a.Id != assentoPaciente.Id);
            if (assentoAcomp is not null)
            {
                _db.Alocacoes.Add(new Alocacao
                {
                    Id = Guid.CreateVersion7(),
                    RotaDiariaId = request.RotaId,
                    SessaoId = sessao.Id,
                    AssentoId = assentoAcomp.Id,
                    Tipo = TipoAlocacao.Acompanhante,
                    Parada = TipoParada.Retorno,
                    OrdemParada = ordem,
                    CriadoEm = agora,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await notificador.RotaAtualizadaAsync(request.RotaId, cancellationToken);
        return alocPaciente.Id;
    }

    private static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6_371_000; // raio da Terra (m)
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
               * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
