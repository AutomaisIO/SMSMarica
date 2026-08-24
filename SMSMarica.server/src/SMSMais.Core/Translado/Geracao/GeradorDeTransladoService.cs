using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Geo;
using SMSMais.Core.Geo.Google;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Translado.Geracao.Dtos;
using SMSMais.Core.Translado.Geracao.IA;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Geracao;

public sealed class GeradorDeTransladoService(
    SmsMaisDbContext db,
    IGeocodificadorService geo,
    IGoogleRoutesClient rotas,
    IDistribuidorIa distribuidor,
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

        // 4) Distribuição: Claude sugere paciente→veículo (revalidado aqui); heurística como fallback.
        var planos = new List<PlanoRota>();
        var iMotorista = 0;
        Motorista ProximoMotorista() => motoristas[iMotorista++ % motoristas.Count];

        // Monta o plano de um veículo+chunk: ordem de coleta via Google Routes (haversine como fallback).
        async Task<PlanoRota> MontarPlanoAsync(
            Veiculo v, Motorista motorista, Guid unidadeId, string unidadeNome, List<Candidato> chunk, List<Assento> livres)
        {
            var destino = chunk[0].Destino;
            List<Candidato> ordenado;
            int metrosTotal;
            int duracaoSeg;
            var otimizado = false;

            RotaOtimizada? otim = null;
            try
            {
                otim = await rotas.OtimizarColetaAsync([.. chunk.Select(c => c.Origem)], destino, ct);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Routes indisponível; usando heurística haversine.");
            }

            if (otim is not null && otim.OrdemColetas.Count == chunk.Count)
            {
                ordenado = [.. otim.OrdemColetas.Select(i => chunk[i])];
                metrosTotal = (int)(otim.DistanciaMetros * 2); // Routes mede a ida; dobra p/ ida + volta
                duracaoSeg = (int)(otim.DuracaoSegundos * 2);
                otimizado = true;
            }
            else
            {
                // coleta os mais distantes do destino primeiro (termina perto da unidade)
                ordenado = [.. chunk.OrderByDescending(c => DistanciaMetros(c.Origem, destino))];
                var metrosIda = 0.0;
                for (var i = 0; i < ordenado.Count; i++)
                {
                    if (i > 0) metrosIda += DistanciaMetros(ordenado[i - 1].Origem, ordenado[i].Origem);
                }
                metrosIda += DistanciaMetros(ordenado[^1].Origem, destino);
                metrosTotal = (int)Math.Round(metrosIda * 2); // ida + volta (aprox.)
                duracaoSeg = (int)Math.Round(metrosTotal / VelocidadeMediaMs);
            }

            return new PlanoRota(v, motorista, unidadeId, unidadeNome, ordenado, livres, metrosTotal, duracaoSeg, otimizado);
        }

        // Empacota por capacidade (acompanhante = 2 assentos); remove os usados de 'pendentes'.
        static List<Candidato> Empacotar(List<Candidato> pendentes, int capacidade)
        {
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
            return chunk;
        }

        var usouIa = false;
        string? justificativaIa = null;

        // 4a) Distribuição via Claude (revalidando capacidade/assentos no backend).
        if (request.UsarIa && candidatos.Count > 0 && veiculos.Count > 0 && motoristas.Count > 0)
        {
            var pacientesIa = candidatos.Select(c => new PacienteIa(
                c.SessaoId.ToString(), c.Nome, c.UnidadeId.ToString(), c.UnidadeNome,
                c.Origem.Latitude, c.Origem.Longitude, c.ComAcompanhante)).ToList();
            var veiculosIa = veiculos.Select(v =>
            {
                var livres = AssentosLivres(v);
                return new VeiculoIa(v.Id.ToString(), v.Placa, livres.Count,
                    livres.Count(a => a.Tipo == TipoAssento.Acompanhante));
            }).ToList();

            var sugestao = await distribuidor.DistribuirAsync(pacientesIa, veiculosIa, ct);
            var porVeiculo = veiculos.ToDictionary(v => v.Id);
            var mapa = new Dictionary<Guid, Guid>();
            foreach (var a in sugestao?.Atribuicoes ?? [])
            {
                if (Guid.TryParse(a.SessaoId, out var sid) && Guid.TryParse(a.VeiculoId, out var vid)
                    && porVeiculo.ContainsKey(vid))
                {
                    mapa[sid] = vid;
                }
            }

            if (mapa.Count > 0)
            {
                usouIa = true;
                justificativaIa = sugestao!.Justificativa;
                var atendidos = new HashSet<Guid>();

                foreach (var g in candidatos
                    .Where(c => mapa.ContainsKey(c.SessaoId))
                    .GroupBy(c => new { VeiculoId = mapa[c.SessaoId], c.UnidadeId, c.UnidadeNome }))
                {
                    var v = porVeiculo[g.Key.VeiculoId];
                    var livres = AssentosLivres(v);
                    var pendentes = g.ToList();
                    var chunk = Empacotar(pendentes, livres.Count);
                    if (chunk.Count == 0) continue;

                    var plano = await MontarPlanoAsync(v, ProximoMotorista(), g.Key.UnidadeId, g.Key.UnidadeNome, chunk, livres);
                    planos.Add(plano with { Justificativa = justificativaIa });
                    foreach (var c in chunk) atendidos.Add(c.SessaoId);
                }

                foreach (var c in candidatos.Where(c => !atendidos.Contains(c.SessaoId)))
                {
                    naoAlocadas.Add(new SessaoNaoAlocadaDto(c.SessaoId, c.PacienteId, c.Nome,
                        c.UnidadeId, c.UnidadeNome, "Não alocado pela IA (capacidade/distribuição)"));
                }
            }
        }

        // 4b) Heurística (IA desligada ou indisponível): por destino, empacota nos veículos em sequência.
        if (!usouIa)
        {
            var iVeiculo = 0;
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
                    var chunk = Empacotar(pendentes, livres.Count);
                    if (chunk.Count == 0) continue; // veículo sem assentos suficientes — pula

                    planos.Add(await MontarPlanoAsync(v, ProximoMotorista(), grupo.Key.UnidadeId, grupo.Key.UnidadeNome, chunk, livres));
                }
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
            data, request.Confirmar, UsouIa: usouIa, Aproximado: planos.Any(p => !p.Otimizado),
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
        geradoPor = p.Otimizado ? "google-routes-v1" : "heuristica-haversine-v1",
        distribuidoPor = p.Justificativa is null ? "heuristica" : "claude",
        justificativaIa = p.Justificativa,
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
        List<Candidato> Ordenado, List<Assento> Livres, int DistanciaMetros, int DuracaoSeg, bool Otimizado,
        string? Justificativa = null);
}
