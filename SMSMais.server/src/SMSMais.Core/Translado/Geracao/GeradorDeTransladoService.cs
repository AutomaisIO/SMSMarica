using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Geo;
using SMSMais.Core.Geo.Google;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Translado.Geracao.Dtos;
using SMSMais.Core.Translado.Geracao.IA;
using SMSMais.Core.Tratamentos.Dtos;
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

        // 1) Sessões elegíveis do dia (mesma regra de ListarSessoesElegiveis, mas por data). Quem está
        // numa rota gerada ainda planejada deste dia continua elegível: o passo 5 troca essas rotas, e
        // sem isso o "Gerar e salvar" de novo apagava a rota sem replanejar ninguém.
        var rows = await (
            from s in db.Sessoes.AsNoTracking()
            join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join u in db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
            where t.Ativo
                && (s.Status == StatusSessao.Pendente || s.Status == StatusSessao.Confirmada)
                && s.DataPrevista <= data
                && !db.Alocacoes.AsNoTracking().Any(a => a.SessaoId == s.Id
                    && db.Rotas.Any(r => r.Id == a.RotaDiariaId && r.Status != StatusRota.Cancelada
                        && !(r.Data == data && r.Origem == OrigemRota.Gerada && r.Status == StatusRota.Planejada)))
            select new
            {
                s.Id,
                t.PacienteId,
                UnidadeId = u.Id,
                UnidadeNome = u.Nome,
                Lat = (double?)u.Gps!.Latitude,
                Lng = (double?)u.Gps!.Longitude,
                AcompEsperado = s.AcompanhanteEsperado,
                t.Mobilidade,
                t.DificuldadeVeiculoAlto,
                t.Isolamento,
                t.UsaOxigenio,
                t.NecessitaAjuda,
                t.AjudaDescricao,
                AcompEscolhidos = s.Acompanhantes.Count,
                LimiteAcomp = t.QuantidadeAcompanhantes,
            }).ToListAsync(ct);

        // Condição do paciente e acompanhantes: vão para a tela e decidem veículo e assentos (etapa 4).
        var leituraPorSessao = rows.ToDictionary(
            r => r.Id,
            r => (Necessidades: new NecessidadesDto(r.Mobilidade, r.DificuldadeVeiculoAlto, r.Isolamento, r.UsaOxigenio, r.NecessitaAjuda, r.AjudaDescricao),
                  Previstos: AcomodacaoNoVeiculo.AcompanhantesDaViagem(r.AcompEsperado, r.AcompEscolhidos, r.LimiteAcomp),
                  Limite: r.LimiteAcomp));

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
                    pinfo.Origem is null ? "Sem geolocalização do paciente (revisar endereço)" : "Unidade de atendimento sem coordenada (revisar o cadastro)"));
                continue;
            }
            candidatos.Add(new Candidato(r.Id, r.PacienteId, pinfo.Nome, r.UnidadeId, r.UnidadeNome, pinfo.Origem, destino,
                new NecessidadeViagem(r.Id, r.Mobilidade, r.Isolamento, r.DificuldadeVeiculoAlto, leituraPorSessao[r.Id].Previstos)));
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
            Veiculo v, Motorista motorista, Guid unidadeId, string unidadeNome, List<Candidato> chunk,
            Dictionary<Guid, Acomodacao> acomodacao)
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

            return new PlanoRota(v, motorista, unidadeId, unidadeNome, ordenado, acomodacao, metrosTotal, duracaoSeg, otimizado);
        }

        // Enche o veículo na ordem de 'pendentes' com quem cabe pela condição (assento de cadeirante, maca,
        // isolamento, um banco por acompanhante); tira de 'pendentes' quem entrou. Isolamento vai sozinho.
        static (List<Candidato> Chunk, Dictionary<Guid, Acomodacao> Acomodacao) Empacotar(
            List<Candidato> pendentes, Veiculo v, List<Assento> livres)
        {
            var chunk = new List<Candidato>();
            var acomodacao = new Dictionary<Guid, Acomodacao>();
            foreach (var c in pendentes.ToList())
            {
                if (chunk.Count > 0 && (c.Necessidade.Isolamento || chunk[0].Necessidade.Isolamento)) continue;
                var tentativa = AcomodacaoNoVeiculo.Acomodar(v, livres, [.. chunk.Select(x => x.Necessidade), c.Necessidade]);
                if (tentativa is null) continue;
                chunk.Add(c);
                acomodacao = tentativa;
                pendentes.Remove(c);
            }
            return (chunk, acomodacao);
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
                    var pendentes = g.OrderBy(c => AcomodacaoNoVeiculo.Exigencia(c.Necessidade)).ToList();
                    var (chunk, acomodacao) = Empacotar(pendentes, v, AssentosLivres(v));
                    if (chunk.Count == 0) continue;

                    var plano = await MontarPlanoAsync(v, ProximoMotorista(), g.Key.UnidadeId, g.Key.UnidadeNome, chunk, acomodacao);
                    planos.Add(plano with { Justificativa = justificativaIa });
                    foreach (var c in chunk) atendidos.Add(c.SessaoId);
                }

                foreach (var c in candidatos.Where(c => !atendidos.Contains(c.SessaoId)))
                {
                    naoAlocadas.Add(new SessaoNaoAlocadaDto(c.SessaoId, c.PacienteId, c.Nome,
                        c.UnidadeId, c.UnidadeNome, "Não coube no veículo sugerido pela IA (assentos ou condição do paciente)"));
                }
            }
        }

        // 4b) Heurística (IA desligada ou indisponível): por destino, quem mais exige (maca, isolamento,
        // cadeirante na cadeira, acompanhantes) escolhe veículo primeiro — o que serve para ele e menos faz
        // falta aos outros — e o veículo é completado com quem couber. Um veículo por rota.
        if (!usouIa)
        {
            var restantes = veiculos.ToList();
            var grupos = candidatos
                .GroupBy(c => new { c.UnidadeId, c.UnidadeNome })
                .OrderBy(g => g.Min(c => AcomodacaoNoVeiculo.Exigencia(c.Necessidade)));
            foreach (var grupo in grupos)
            {
                var pendentes = grupo.OrderBy(c => AcomodacaoNoVeiculo.Exigencia(c.Necessidade)).ToList();
                while (pendentes.Count > 0)
                {
                    var primeiro = pendentes[0];
                    var v = motoristas.Count == 0 ? null : restantes
                        .Select((x, i) => (Veiculo: x, Ordem: i, Livres: AssentosLivres(x)))
                        .Where(x => AcomodacaoNoVeiculo.Acomodar(x.Veiculo, x.Livres, [primeiro.Necessidade]) is not null)
                        .OrderBy(x => AcomodacaoNoVeiculo.Preferencia(x.Veiculo, x.Livres, primeiro.Necessidade))
                        .ThenBy(x => x.Ordem)
                        .Select(x => x.Veiculo)
                        .FirstOrDefault();
                    if (v is null)
                    {
                        naoAlocadas.Add(new SessaoNaoAlocadaDto(primeiro.SessaoId, primeiro.PacienteId, primeiro.Nome,
                            primeiro.UnidadeId, primeiro.UnidadeNome,
                            restantes.Count == 0 || motoristas.Count == 0
                                ? "Sem veículo/motorista disponível"
                                : AcomodacaoNoVeiculo.MotivoSemVeiculo(primeiro.Necessidade)));
                        pendentes.RemoveAt(0);
                        continue;
                    }

                    restantes.Remove(v);
                    var (chunk, acomodacao) = Empacotar(pendentes, v, AssentosLivres(v));
                    planos.Add(await MontarPlanoAsync(v, ProximoMotorista(), grupo.Key.UnidadeId, grupo.Key.UnidadeNome, chunk, acomodacao));
                }
            }
        }

        // 5) Persiste (se confirmar) ou só devolve o preview.
        if (request.Confirmar)
        {
            // Gerar de novo substitui só o que ainda é plano. Rota em andamento está com o carro na rua
            // (e com o tablet seguindo ela); concluída e cancelada são histórico. As sessões da em
            // andamento e da concluída não voltam para cá (passo 1); as da planejada, sim.
            var antigas = await db.Rotas
                .Where(r => r.Data == data && r.Origem == OrigemRota.Gerada && r.Status == StatusRota.Planejada)
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
                .Select((c, idx) =>
                {
                    var leitura = leituraPorSessao.GetValueOrDefault(c.SessaoId);
                    return new ParadaGeradaDto(idx + 1, c.SessaoId, c.PacienteId, c.Nome, c.ComAcompanhante,
                        leitura.Necessidades, leitura.Previstos, leitura.Limite);
                })
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

        // Os assentos já foram decididos ao montar o plano (AcomodacaoNoVeiculo): o que foi conferido é o
        // que se grava.
        Alocacao Alocar(Guid sessaoId, Assento assento, TipoAlocacao tipo, int ordem) => new()
        {
            Id = Guid.CreateVersion7(),
            RotaDiariaId = rota.Id,
            SessaoId = sessaoId,
            AssentoId = assento.Id,
            Tipo = tipo,
            Parada = TipoParada.Coleta,
            OrdemParada = ordem,
            CriadoEm = agora,
        };
        for (var i = 0; i < p.Ordenado.Count; i++)
        {
            var c = p.Ordenado[i];
            var lugar = p.Acomodacao[c.SessaoId];
            db.Alocacoes.Add(Alocar(c.SessaoId, lugar.Paciente, TipoAlocacao.Paciente, i + 1));
            foreach (var a in lugar.Acompanhantes)
                db.Alocacoes.Add(Alocar(c.SessaoId, a, TipoAlocacao.Acompanhante, i + 1));
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
        unidadeAtendimentoId = p.UnidadeId,
        unidadeAtendimento = p.UnidadeNome,
        distanciaMetros = p.DistanciaMetros,
        duracaoSegundos = p.DuracaoSeg,
        paradas = p.Ordenado.Select((c, i) => new
        {
            ordem = i + 1,
            sessaoId = c.SessaoId,
            pacienteId = c.PacienteId,
            paciente = c.Nome,
            comAcompanhante = c.ComAcompanhante,
            acompanhantes = c.Necessidade.Acompanhantes,
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
        Coordenada Origem, Coordenada Destino, NecessidadeViagem Necessidade)
    {
        public bool ComAcompanhante => Necessidade.Acompanhantes > 0;
    }

    private sealed record PlanoRota(
        Veiculo Veiculo, Motorista Motorista, Guid UnidadeId, string UnidadeNome,
        List<Candidato> Ordenado, Dictionary<Guid, Acomodacao> Acomodacao, int DistanciaMetros, int DuracaoSeg,
        bool Otimizado, string? Justificativa = null);
}
