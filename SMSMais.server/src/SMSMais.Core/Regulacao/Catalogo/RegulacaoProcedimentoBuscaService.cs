using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pgvector;
using Pgvector.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Sigtap;
using SMSMais.Core.Inteligencia.Provedores;
using SMSMais.Core.Regulacao.Catalogo.Dtos;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Catalogo;

/// <inheritdoc cref="IRegulacaoProcedimentoBuscaService"/>
public sealed class RegulacaoProcedimentoBuscaService(
    SmsMaisDbContext db,
    IServicoEmbeddings embeddings,
    CacheVetorConsulta cache,
    IRegulacaoConfiguracaoService configuracao,
    ILogger<RegulacaoProcedimentoBuscaService> log) : IRegulacaoProcedimentoBuscaService
{
    private const int TamanhoMinimoTermo = 3;
    private const int LimiteMaximo = 50;
    private const int CandidatosVetoriais = 30;

    public async Task<RegulacaoBuscaResultadoDto> BuscarAsync(
        string termo, TipoProcedimentoRegulacao? tipo, int limite, CancellationToken ct)
    {
        var bruto = (termo ?? string.Empty).Trim();
        if (bruto.Length < TamanhoMinimoTermo)
        {
            throw new ValidacaoException("q", $"Informe ao menos {TamanhoMinimoTermo} caracteres.");
        }

        limite = Math.Clamp(limite <= 0 ? 20 : limite, 1, LimiteMaximo);
        var normalizado = SugestaoSigtap.Normalizar(bruto);

        // Ordem importa: o lexical entra primeiro e com score cheio. Quem digita o nome exato do
        // procedimento tem de vê-lo no topo, e não atrás de um vizinho semântico bem cotado.
        var ordenados = new List<Guid>();
        var scores = new Dictionary<Guid, double>();

        foreach (var id in await BuscarLexicalAsync(normalizado, tipo, ct))
        {
            if (scores.TryAdd(id, 1.0)) ordenados.Add(id);
        }

        // O corte vem da configuração (plano 09) para ser calibrável sem deploy — a busca
        // semântica é o tipo de coisa que se ajusta olhando resultado real.
        var corte = (double)(await configuracao.ObterEntidadeAsync(ct)).BuscaCorteDistancia;

        var (vetoriais, degradada) = await BuscarVetorialAsync(normalizado, tipo, corte, ct);
        foreach (var (id, score) in vetoriais)
        {
            if (scores.TryAdd(id, score)) ordenados.Add(id);
        }

        var pagina = ordenados.Take(limite).ToList();
        var itens = await MontarItensAsync(pagina, scores, ct);
        return new RegulacaoBuscaResultadoDto(itens, degradada);
    }

    private async Task<List<Guid>> BuscarLexicalAsync(
        string normalizado, TipoProcedimentoRegulacao? tipo, CancellationToken ct)
    {
        var padrao = $"%{normalizado}%";

        // `Unaccent` sem qualificar funciona porque a conexão sobe com search_path = smsmarica
        // (ver SMSMais.Data/DependencyInjection.cs) — a extensão está nesse schema, não no public.
        var porCanonico = db.RegulacaoProcedimentos.AsNoTracking()
            .Where(p => p.Ativo
                && (tipo == null || p.Tipo == tipo)
                && EF.Functions.ILike(EF.Functions.Unaccent(p.NomeNormalizado), padrao))
            .Select(p => p.Id);

        // Casar também pelo rótulo da origem é o que faz "1ª vez" achar o recurso do SER: o nome
        // canônico é um só, mas cada sistema escreve o procedimento do seu jeito.
        var porOrigem = db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.Ativo
                && o.Procedimento!.Ativo
                && (tipo == null || o.Procedimento!.Tipo == tipo)
                && EF.Functions.ILike(EF.Functions.Unaccent(o.RotuloExterno), padrao))
            .Select(o => o.ProcedimentoId);

        return await porCanonico.Union(porOrigem).ToListAsync(ct);
    }

    private async Task<(List<(Guid Id, double Score)> Itens, bool Degradada)> BuscarVetorialAsync(
        string normalizado, TipoProcedimentoRegulacao? tipo, double corte, CancellationToken ct)
    {
        Vector consulta;
        try
        {
            consulta = new Vector(await cache.ObterOuEmbedarAsync(normalizado, embeddings, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // O provedor de embeddings é externo e cai. A busca continua valendo só com o
            // lexical, e a tela avisa — melhor do que devolver "nada encontrado", que faria o
            // operador concluir que o procedimento não existe.
            log.LogWarning(ex, "Busca da regulação: provedor de embeddings indisponível; caiu para lexical.");
            return ([], true);
        }

        try
        {
            var vizinhos = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
                .Where(o => o.Ativo && o.Embedding != null
                    && o.Procedimento!.Ativo
                    && (tipo == null || o.Procedimento!.Tipo == tipo))
                .Select(o => new { o.ProcedimentoId, Distancia = o.Embedding!.CosineDistance(consulta) })
                .OrderBy(x => x.Distancia)
                .Take(CandidatosVetoriais)
                .ToListAsync(ct);

            // Um canônico pode ter várias origens: fica com a menor distância entre elas.
            var melhores = vizinhos
                .Where(x => x.Distancia <= corte)
                .GroupBy(x => x.ProcedimentoId)
                .Select(g => (Id: g.Key, Score: 1 - g.Min(x => x.Distancia)))
                .OrderByDescending(x => x.Score)
                .ToList();

            return (melhores, false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Busca da regulação: parte vetorial falhou; caiu para lexical.");
            return ([], true);
        }
    }

    public async Task<RegulacaoProcedimentoDetalheDto> ObterAsync(Guid procedimentoId, CancellationToken ct)
    {
        var p = await db.RegulacaoProcedimentos.AsNoTracking()
            .Where(x => x.Id == procedimentoId)
            .Select(x => new { x.Id, x.NomeCanonico, x.Tipo, x.ProcedimentoSigtapId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("Procedimento canônico da regulação", procedimentoId);

        var codigoSigtap = p.ProcedimentoSigtapId is null
            ? null
            : await db.ProcedimentosSigtap.AsNoTracking()
                .Where(s => s.Id == p.ProcedimentoSigtapId)
                .Select(s => s.Codigo)
                .FirstOrDefaultAsync(ct);

        var origens = await CarregarOrigensAsync([p.Id], ct);
        var minhas = origens.GetValueOrDefault(p.Id, []);
        var executantes = await CarregarExecutantesAsync(minhas, ct);

        return new RegulacaoProcedimentoDetalheDto(
            p.Id, p.NomeCanonico, p.Tipo, codigoSigtap, minhas, executantes, MontarExisteExterno(minhas));
    }

    // ---------------------------------------------------------------- montagem

    private async Task<IReadOnlyList<RegulacaoProcedimentoItemDto>> MontarItensAsync(
        List<Guid> ids, Dictionary<Guid, double> scores, CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        var cabecalhos = await db.RegulacaoProcedimentos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.NomeCanonico, p.Tipo })
            .ToDictionaryAsync(p => p.Id, ct);

        var origens = await CarregarOrigensAsync(ids, ct);
        var todasOrigens = origens.Values.SelectMany(x => x).ToList();
        var executantesPorCodigo = await CarregarExecutantesPorCodigoAsync(todasOrigens, ct);

        var itens = new List<RegulacaoProcedimentoItemDto>(ids.Count);
        foreach (var id in ids)
        {
            if (!cabecalhos.TryGetValue(id, out var c)) continue;
            var minhas = origens.GetValueOrDefault(id, []);
            itens.Add(new RegulacaoProcedimentoItemDto(
                c.Id, c.NomeCanonico, c.Tipo, scores.GetValueOrDefault(id),
                minhas,
                AgruparExecutantes(minhas, executantesPorCodigo),
                MontarExisteExterno(minhas)));
        }
        return itens;
    }

    private async Task<Dictionary<Guid, List<RegulacaoOrigemDto>>> CarregarOrigensAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var linhas = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.Ativo && ids.Contains(o.ProcedimentoId))
            .OrderBy(o => o.Sistema).ThenBy(o => o.RotuloExterno)
            .Select(o => new { o.ProcedimentoId, o.Id, o.Sistema, o.RotuloExterno, o.Ramo, o.ChaveExterna })
            .ToListAsync(ct);

        return linhas
            .GroupBy(o => o.ProcedimentoId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(o => new RegulacaoOrigemDto(o.Id, o.Sistema, o.RotuloExterno, o.Ramo, o.ChaveExterna)).ToList());
    }

    private static ExisteExternoDto MontarExisteExterno(IReadOnlyList<RegulacaoOrigemDto> origens) =>
        new(
            Ser: origens.Any(o => o.Sistema == SistemaRegulacao.Ser),
            SerAmbulatorioEstadual: origens.Any(o => o.Sistema == SistemaRegulacao.Ser && o.Ramo == "AE"),
            Sernit: origens.Any(o => o.Sistema == SistemaRegulacao.Sernit));

    private async Task<IReadOnlyList<ExecutanteInternoDto>> CarregarExecutantesAsync(
        IReadOnlyList<RegulacaoOrigemDto> origens, CancellationToken ct)
    {
        var mapa = await CarregarExecutantesPorCodigoAsync(origens, ct);
        return AgruparExecutantes(origens, mapa);
    }

    /// <summary>
    /// Oferta interna: escala do SISREG ligada, dentro da vigência e ainda vindo no arquivo.
    ///
    /// <para>As três condições são necessárias — <c>Ativa</c> <b>não</b> quer dizer vigente (a
    /// escala pode estar ligada e com vigência vencida), e <c>Ausente</c> marca a que parou de
    /// vir no arquivo sem ser apagada.</para>
    /// </summary>
    private async Task<Dictionary<string, List<EscalaAgrupada>>> CarregarExecutantesPorCodigoAsync(
        IReadOnlyList<RegulacaoOrigemDto> origens, CancellationToken ct)
    {
        var codigos = origens
            .Where(o => o.Sistema == SistemaRegulacao.Sisreg)
            .Select(o => o.ChaveExterna)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (codigos.Count == 0) return [];

        // Código de grupo (termina em 000) não tem escala própria: a oferta está nos itens dele.
        var prefixosDeGrupo = codigos
            .Where(c => c.EndsWith("000", StringComparison.Ordinal) && c.Length >= 4)
            .Select(c => c[..^3])
            .ToList();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var linhas = await db.SisregEscalas.AsNoTracking()
            .Where(e => e.Status == StatusEscalaSisreg.Ativa
                && !e.Ausente
                && e.VigenciaFim >= hoje
                && (codigos.Contains(e.ProcedimentoCodigo)
                    || prefixosDeGrupo.Any(p => e.ProcedimentoCodigo.StartsWith(p))))
            .Select(e => new
            {
                e.ProcedimentoCodigo,
                e.UnidadeId,
                e.UnidadeNomeSisreg,
                e.Cnes,
                e.VagasTotal,
                e.VigenciaInicio,
            })
            .ToListAsync(ct);

        var porCodigo = new Dictionary<string, List<EscalaAgrupada>>(StringComparer.Ordinal);
        foreach (var codigo in codigos)
        {
            var prefixo = codigo.EndsWith("000", StringComparison.Ordinal) && codigo.Length >= 4
                ? codigo[..^3]
                : null;

            porCodigo[codigo] = [.. linhas
                .Where(l => l.ProcedimentoCodigo == codigo
                    || (prefixo is not null && l.ProcedimentoCodigo.StartsWith(prefixo, StringComparison.Ordinal)))
                .GroupBy(l => new { l.UnidadeId, l.UnidadeNomeSisreg, l.Cnes })
                .Select(g => new EscalaAgrupada(
                    g.Key.UnidadeId,
                    g.Key.UnidadeNomeSisreg,
                    g.Key.Cnes,
                    g.Sum(x => x.VagasTotal),
                    g.Where(x => x.VigenciaInicio > hoje).Select(x => (DateOnly?)x.VigenciaInicio).Min()))];
        }
        return porCodigo;
    }

    private static IReadOnlyList<ExecutanteInternoDto> AgruparExecutantes(
        IReadOnlyList<RegulacaoOrigemDto> origens, Dictionary<string, List<EscalaAgrupada>> porCodigo)
    {
        if (porCodigo.Count == 0) return [];

        return [.. origens
            .Where(o => o.Sistema == SistemaRegulacao.Sisreg)
            .SelectMany(o => porCodigo.GetValueOrDefault(o.ChaveExterna, []))
            .GroupBy(e => e.UnidadeId)
            .Select(g => new ExecutanteInternoDto(
                g.Key,
                g.First().Nome,
                g.First().Cnes,
                g.Sum(x => x.VagasTotal),
                g.Select(x => x.ProximaVigencia).Where(d => d.HasValue).Min()))
            .OrderByDescending(x => x.VagasTotal)
            .ThenBy(x => x.Nome, StringComparer.Ordinal)];
    }

    private sealed record EscalaAgrupada(
        Guid UnidadeId, string Nome, string? Cnes, int VagasTotal, DateOnly? ProximaVigencia);
}
