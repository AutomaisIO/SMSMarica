using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Geo;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Translado.Geracao.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Translado.Geracao;

public sealed class GeradorDeTransladoService(
    SmsMaricaDbContext db,
    IGeocodificadorService geo,
    IPacientesService pacientes,
    ILogger<GeradorDeTransladoService> logger) : IGeradorDeTransladoService
{
    private const double VelocidadeMediaMs = 40_000.0 / 3600.0; // 40 km/h em m/s

    public async Task<ResultadoGeracaoDto> GerarAsync(GerarTransladoRequest request, CancellationToken ct = default)
    {
        var data = request.Data;

        // 1) Sessões elegíveis do dia (mesma regra de ListarSessoesElegiveis, mas por data).
        var rows = await (
            from s in db.Sessoes.AsNoTracking()
            join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join u in db.Unidades.AsNoTracking() on t.UnidadeId equals u.Id
            where t.Ativo
                && (s.Status == StatusSessao.Pendente || s.Status == StatusSessao.Confirmada)
                && s.DataPrevista <= data
                && !db.Alocacoes.AsNoTracking().Any(a => a.SessaoId == s.Id
                    && db.Rotas.Any(r => r.Id == a.RotaDiariaId && r.Status != StatusRota.Cancelada))
            select new
            {
                s.Id,
                t.PacienteId,
                t.UnidadeId,
                UnidadeNome = u.Nome,
                Lat = (double?)u.Gps!.Latitude,
                Lng = (double?)u.Gps!.Longitude,
                Acomp = s.AcompanhanteEsperado == true,
            }).ToListAsync(ct);

        var naoAlocadas = new List<SessaoNaoAlocadaDto>();
        var candidatos = new List<Candidato>();
        var cache = new Dictionary<Guid, (string Nome, Coordenada? Origem)>();

        // 2) Resolve nome + origem (geocodificada) de cada paciente.
        foreach (var r in rows)
        {
            if (!cache.TryGetValue(r.PacienteId, out var pinfo))
            {
                string nome = string.Empty;
                Coordenada? origem = null;
                try
                {
                    var pac = await pacientes.ObterPorIdAsync(r.PacienteId, ct);
                    nome = pac.NomeCompleto;
                    origem = await geo.GeocodificarAsync(pac.Endereco?.ParaEntidade(), ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Geração: paciente {Id} não resolvido.", r.PacienteId);
                }
                pinfo = (nome, origem);
                cache[r.PacienteId] = pinfo;
            }

            var destino = r.Lat is not null && r.Lng is not null ? new Coordenada(r.Lat.Value, r.Lng.Value) : null;
            if (pinfo.Origem is null || destino is null)
            {
                naoAlocadas.Add(new SessaoNaoAlocadaDto(r.Id, r.PacienteId, pinfo.Nome, r.UnidadeId, r.UnidadeNome,
                    pinfo.Origem is null ? "Sem geolocalização do paciente (revisar endereço)" : "Unidade de destino sem coordenada"));
                continue;
            }
            candidatos.Add(new Candidato(r.Id, r.PacienteId, pinfo.Nome, r.UnidadeId, r.UnidadeNome, pinfo.Origem, destino, r.Acomp));
        }

        // 3) Frota e motoristas disponíveis.
        var veiculos = await db.Veiculos.AsNoTracking()
            .Where(v => v.Ativo)
            .Include(v => v.Fileiras).ThenInclude(f => f.Assentos)
            .ToListAsync(ct);
        var motoristas = await db.Motoristas.AsNoTracking()
            .Where(m => m.ExcluidoEm == null)
            .Include(m => m.Usuario)
            .ToListAsync(ct);

        // 4) Distribuição (heurística): por destino, empacota nos veículos respeitando capacidade.
        var planos = new List<PlanoRota>();
        var iVeiculo = 0;
        var iMotorista = 0;

        foreach (var grupo in candidatos.GroupBy(c => new { c.UnidadeId, c.UnidadeNome }))
        {
            var pendentes = grupo.ToList();
            while (pendentes.Count > 0)
            {
                if (iVeiculo >= veiculos.Count || motoristas.Count == 0)
                {
                    foreach (var c in pendentes)
                        naoAlocadas.Add(new SessaoNaoAlocadaDto(c.SessaoId, c.PacienteId, c.Nome, c.UnidadeId, c.UnidadeNome, "Sem veículo/motorista disponível"));
                    pendentes.Clear();
                    break;
                }

                var v = veiculos[iVeiculo++];
                var livres = AssentosLivres(v);
                var capacidade = livres.Count;

                var chunk = new List<Candidato>();
                var usados = 0;
                foreach (var c in pendentes.ToList())
                {
                    var precisa = 1 + (c.ComAcompanhante ? 1 : 0);
                    if (usados + precisa <= capacidade)
                    {
                        chunk.Add(c);
                        usados += precisa;
                        pendentes.Remove(c);
                    }
                }
                if (chunk.Count == 0) continue; // veículo sem assentos suficientes — pula

                var motorista = motoristas[iMotorista++ % motoristas.Count];
                var destino = chunk[0].Destino;
                // coleta os mais distantes do destino primeiro (termina perto da unidade)
                var ordenado = chunk.OrderByDescending(c => DistanciaMetros(c.Origem, destino)).ToList();

                var metrosIda = 0.0;
                for (var i = 0; i < ordenado.Count; i++)
                {
                    if (i > 0) metrosIda += DistanciaMetros(ordenado[i - 1].Origem, ordenado[i].Origem);
                }
                metrosIda += DistanciaMetros(ordenado[^1].Origem, destino);
                var metrosTotal = (int)Math.Round(metrosIda * 2); // ida + volta (aprox.)
                var duracaoSeg = (int)Math.Round(metrosTotal / VelocidadeMediaMs);

                planos.Add(new PlanoRota(v, motorista, grupo.Key.UnidadeId, grupo.Key.UnidadeNome, ordenado, livres, metrosTotal, duracaoSeg));
            }
        }

        // 5) Persiste (se confirmar) ou só devolve o preview.
        if (request.Confirmar)
        {
            var antigas = await db.Rotas
                .Where(r => r.Data == data && r.Origem == OrigemRota.Gerada && r.Status != StatusRota.Concluida)
                .Include(r => r.Alocacoes)
                .ToListAsync(ct);
            db.Alocacoes.RemoveRange(antigas.SelectMany(r => r.Alocacoes));
            db.Rotas.RemoveRange(antigas);
            await db.SaveChangesAsync(ct);
        }

        var rotasDto = new List<RotaGeradaDto>();
        foreach (var p in planos)
        {
            Guid? rotaId = null;
            if (request.Confirmar)
            {
                rotaId = await PersistirAsync(data, p, ct);
            }

            var paradas = p.Ordenado
                .Select((c, idx) => new ParadaGeradaDto(idx + 1, c.SessaoId, c.PacienteId, c.Nome, c.ComAcompanhante))
                .ToList();

            rotasDto.Add(new RotaGeradaDto(
                rotaId, p.Veiculo.Id, p.Veiculo.Placa, p.Motorista.Id,
                p.Motorista.Usuario?.NomeCompleto ?? "Motorista",
                p.UnidadeId, p.UnidadeNome, p.Ordenado.Count, p.DistanciaMetros, p.DuracaoSeg, paradas));
        }

        return new ResultadoGeracaoDto(
            data, request.Confirmar, UsouIa: false, Aproximado: true,
            TotalSessoes: rows.Count,
            TotalAlocadas: planos.Sum(p => p.Ordenado.Count),
            Rotas: rotasDto, NaoAlocadas: naoAlocadas);
    }

    private async Task<Guid> PersistirAsync(DateOnly data, PlanoRota p, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var rota = new RotaDiaria
        {
            Id = Guid.CreateVersion7(),
            Data = data,
            VeiculoId = p.Veiculo.Id,
            MotoristaId = p.Motorista.Id,
            Status = StatusRota.Planejada,
            Origem = OrigemRota.Gerada,
            GeradaEm = agora,
            DistanciaTotalMetros = p.DistanciaMetros,
            DuracaoEstimadaSegundos = p.DuracaoSeg,
            PlanoRotaJson = SerializarPlano(p),
            CriadoEm = agora,
        };
        db.Rotas.Add(rota);

        var usadas = new HashSet<Guid>();
        Assento? Pegar(bool acompanhante)
        {
            var disp = p.Livres.Where(a => !usadas.Contains(a.Id));
            var pick = acompanhante
                ? disp.FirstOrDefault(a => a.Tipo == TipoAssento.Acompanhante) ?? disp.FirstOrDefault()
                : disp.FirstOrDefault(a => a.Tipo == TipoAssento.Passageiro) ?? disp.FirstOrDefault();
            if (pick is not null) usadas.Add(pick.Id);
            return pick;
        }

        for (var i = 0; i < p.Ordenado.Count; i++)
        {
            var c = p.Ordenado[i];
            var assento = Pegar(acompanhante: false);
            if (assento is null) break; // capacidade já validada; segurança

            db.Alocacoes.Add(new Alocacao
            {
                Id = Guid.CreateVersion7(),
                RotaDiariaId = rota.Id,
                SessaoId = c.SessaoId,
                AssentoId = assento.Id,
                Tipo = TipoAlocacao.Paciente,
                Parada = TipoParada.Coleta,
                OrdemParada = i + 1,
                CriadoEm = agora,
            });

            if (c.ComAcompanhante)
            {
                var assentoAcomp = Pegar(acompanhante: true);
                if (assentoAcomp is not null)
                {
                    db.Alocacoes.Add(new Alocacao
                    {
                        Id = Guid.CreateVersion7(),
                        RotaDiariaId = rota.Id,
                        SessaoId = c.SessaoId,
                        AssentoId = assentoAcomp.Id,
                        Tipo = TipoAlocacao.Acompanhante,
                        Parada = TipoParada.Coleta,
                        OrdemParada = i + 1,
                        CriadoEm = agora,
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return rota.Id;
    }

    private static List<Assento> AssentosLivres(Veiculo v) =>
        [.. v.Fileiras.OrderBy(f => f.Ordem)
            .SelectMany(f => f.Assentos
                .Where(a => !a.Excluido && !a.Bloqueado && a.Tipo != TipoAssento.Motorista)
                .OrderBy(a => a.Numero))];

    private static string SerializarPlano(PlanoRota p) => JsonSerializer.Serialize(new
    {
        geradoPor = "heuristica-haversine-v1",
        unidadeId = p.UnidadeId,
        unidade = p.UnidadeNome,
        distanciaMetros = p.DistanciaMetros,
        duracaoSegundos = p.DuracaoSeg,
        paradas = p.Ordenado.Select((c, i) => new
        {
            ordem = i + 1,
            sessaoId = c.SessaoId,
            pacienteId = c.PacienteId,
            paciente = c.Nome,
            comAcompanhante = c.ComAcompanhante,
            lat = c.Origem.Latitude,
            lng = c.Origem.Longitude,
        }),
    });

    private static double DistanciaMetros(Coordenada a, Coordenada b)
    {
        const double raio = 6_371_000;
        var dLat = (b.Latitude - a.Latitude) * Math.PI / 180;
        var dLon = (b.Longitude - a.Longitude) * Math.PI / 180;
        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(a.Latitude * Math.PI / 180) * Math.Cos(b.Latitude * Math.PI / 180)
               * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return raio * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    private sealed record Candidato(
        Guid SessaoId, Guid PacienteId, string Nome, Guid UnidadeId, string UnidadeNome,
        Coordenada Origem, Coordenada Destino, bool ComAcompanhante);

    private sealed record PlanoRota(
        Veiculo Veiculo, Motorista Motorista, Guid UnidadeId, string UnidadeNome,
        List<Candidato> Ordenado, List<Assento> Livres, int DistanciaMetros, int DuracaoSeg);
}
