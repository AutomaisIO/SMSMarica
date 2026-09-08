using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Regras;

/// <param name="Demanda">Quantas solicitações históricas o procedimento teve naquele sistema.</param>
public sealed record DemandaPorSistemaDto(SistemaRegulacao Sistema, int Demanda);

/// <param name="Demanda">Soma da demanda nos sistemas pedidos — é por ela que a lista é ordenada.</param>
/// <param name="TotalRegras">Regras cadastradas, ativas e inativas.</param>
/// <param name="RegrasAtivas">Quantas dessas realmente valem hoje no wizard.</param>
public sealed record ProcedimentoRegradoDto(
    Guid ProcedimentoId,
    string Nome,
    TipoProcedimentoRegulacao Tipo,
    int Demanda,
    IReadOnlyList<DemandaPorSistemaDto> PorSistema,
    int TotalRegras,
    int RegrasAtivas);

public interface IRegulacaoProcedimentoRegradoService
{
    /// <summary>
    /// Os procedimentos mais pedidos, para a tela de regras abrir com trabalho na frente em vez
    /// de uma caixa de busca vazia.
    /// </summary>
    Task<IReadOnlyList<ProcedimentoRegradoDto>> MaisPedidosAsync(
        IReadOnlyCollection<SistemaRegulacao> sistemas, int limite, CancellationToken ct);
}

/// <summary>
/// Ranking dos procedimentos por <b>demanda histórica real</b> (plano 03).
///
/// <para><b>Por que demanda e não número de regras:</b> a curadoria tem 999 procedimentos pela
/// frente e tempo para poucos. Ordenar por quantidade de regra colocaria no topo o que o manual
/// escreveu mais, que não é o que a rede mais pede. Ordenar por demanda coloca no topo
/// eletrocardiograma (51.671 pedidos) e consulta em oftalmologia (37.549) — onde uma regra mal
/// ativada trava fila de verdade.</para>
///
/// <para><b>O topo é por sistema, e depois somado</b>, porque as escalas não se comparam: o SISREG
/// tem 1.017.902 solicitações históricas, o SER 25.857 e o SERNIT 1.223. Um ranking único por
/// contagem bruta seria o ranking do SISREG com ruído — o SERNIT inteiro não alcançaria a
/// centésima linha. Pegando o topo de cada um e unindo, cada sistema traz os seus.</para>
/// </summary>
public sealed class RegulacaoProcedimentoRegradoService(
    SmsMaisDbContext db, IMemoryCache cache) : IRegulacaoProcedimentoRegradoService
{
    /// <summary>
    /// Seis horas. A agregação do SISREG varre 1.017.902 linhas e leva ~4 s; e o insumo é
    /// histórico importado uma vez por dia, então recalcular a cada abertura de tela seria pagar
    /// 4 segundos para obter o mesmo número.
    /// </summary>
    private static readonly TimeSpan DuracaoCache = TimeSpan.FromHours(6);

    public async Task<IReadOnlyList<ProcedimentoRegradoDto>> MaisPedidosAsync(
        IReadOnlyCollection<SistemaRegulacao> sistemas, int limite, CancellationToken ct)
    {
        limite = Math.Clamp(limite, 1, 500);
        var pedidos = sistemas.Count == 0
            ? [SistemaRegulacao.Sisreg, SistemaRegulacao.Ser, SistemaRegulacao.Sernit]
            : sistemas.Distinct().ToArray();

        // Topo de cada sistema, separado: unir antes de cortar deixaria o SERNIT de fora.
        var porProcedimento = new Dictionary<Guid, Dictionary<SistemaRegulacao, int>>();
        foreach (var sistema in pedidos)
        {
            foreach (var (procedimentoId, n) in await TopDoSistemaAsync(sistema, limite, ct))
            {
                if (!porProcedimento.TryGetValue(procedimentoId, out var mapa))
                {
                    porProcedimento[procedimentoId] = mapa = [];
                }
                mapa[sistema] = n;
            }
        }

        if (porProcedimento.Count == 0) return [];

        var ids = porProcedimento.Keys.ToArray();

        var nomes = await db.RegulacaoProcedimentos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.NomeCanonico, p.Tipo })
            .ToListAsync(ct);

        // As regras cabem todas na memória (são centenas), então uma consulta resolve as duas
        // contagens sem N+1.
        var contagens = await db.RegulacaoRegras.AsNoTracking()
            .Where(r => ids.Contains(r.ProcedimentoId))
            .GroupBy(r => r.ProcedimentoId)
            .Select(g => new { Id = g.Key, Total = g.Count(), Ativas = g.Count(r => r.Ativo) })
            .ToDictionaryAsync(x => x.Id, ct);

        return
        [
            .. nomes
                .Select(p =>
                {
                    var mapa = porProcedimento[p.Id];
                    contagens.TryGetValue(p.Id, out var c);
                    return new ProcedimentoRegradoDto(
                        p.Id,
                        p.NomeCanonico,
                        p.Tipo,
                        mapa.Values.Sum(),
                        [.. mapa.Select(x => new DemandaPorSistemaDto(x.Key, x.Value))
                                .OrderByDescending(x => x.Demanda)],
                        c?.Total ?? 0,
                        c?.Ativas ?? 0);
                })
                .OrderByDescending(x => x.Demanda)
                .ThenBy(x => x.Nome),
        ];
    }

    private async Task<IReadOnlyList<(Guid ProcedimentoId, int Demanda)>> TopDoSistemaAsync(
        SistemaRegulacao sistema, int limite, CancellationToken ct)
    {
        // O limite entra na chave: pedir 100 e depois 200 não pode devolver a lista curta.
        var chave = $"regulacao-top-{sistema}-{limite}";
        if (cache.TryGetValue<IReadOnlyList<(Guid, int)>>(chave, out var pronto) && pronto is not null)
        {
            return pronto;
        }

        var top = sistema switch
        {
            SistemaRegulacao.Sisreg => await TopSisregAsync(limite, ct),
            SistemaRegulacao.Ser => await TopPorRotuloAsync(sistema, limite, ct),
            SistemaRegulacao.Sernit => await TopPorRotuloAsync(sistema, limite, ct),
            _ => [],
        };

        cache.Set(chave, top, DuracaoCache);
        return top;
    }

    /// <summary>
    /// SISREG casa por <b>código</b>: <c>solicitacao.procedimento_codigo_sisreg</c> é a mesma
    /// chave que a origem do catálogo guarda. Junção exata, feita no banco — são 1.017.902
    /// linhas e 1.212 códigos distintos; trazer isso para a memória seria absurdo.
    /// </summary>
    private async Task<IReadOnlyList<(Guid, int)>> TopSisregAsync(int limite, CancellationToken ct)
    {
        var linhas = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.ProcedimentoCodigoSisreg != null)
            .Join(
                db.RegulacaoProcedimentoOrigens.AsNoTracking()
                    .Where(o => o.Sistema == SistemaRegulacao.Sisreg),
                s => s.ProcedimentoCodigoSisreg,
                o => o.ChaveExterna,
                (s, o) => o.ProcedimentoId)
            .GroupBy(id => id)
            .Select(g => new { Id = g.Key, N = g.Count() })
            .OrderByDescending(x => x.N)
            .Take(limite)
            .ToListAsync(ct);

        return [.. linhas.Select(x => (x.Id, x.N))];
    }

    /// <summary>
    /// SER e SERNIT não gravam o código na solicitação, só o <b>rótulo digitado</b> — e a mesma
    /// coisa aparece como "CONSULTA EM OFTALMOLOGIA" e "Consulta  em Oftalmologia". A junção é
    /// pelo rótulo normalizado, e é feita na memória de propósito: são poucas centenas de
    /// rótulos distintos de cada lado, e normalizar em SQL exigiria a extensão <c>unaccent</c>.
    /// </summary>
    private async Task<IReadOnlyList<(Guid, int)>> TopPorRotuloAsync(
        SistemaRegulacao sistema, int limite, CancellationToken ct)
    {
        var demanda = sistema == SistemaRegulacao.Ser
            ? await db.SerSolicitacoes.AsNoTracking()
                .GroupBy(s => s.Recurso)
                .Select(g => new { Rotulo = g.Key, N = g.Count() })
                .ToListAsync(ct)
            : await db.SernitSolicitacoes.AsNoTracking()
                .GroupBy(s => s.Recurso)
                .Select(g => new { Rotulo = g.Key, N = g.Count() })
                .ToListAsync(ct);

        var origens = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.Sistema == sistema)
            .Select(o => new { o.ProcedimentoId, o.RotuloExterno })
            .ToListAsync(ct);

        var porRotulo = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var o in origens)
        {
            porRotulo.TryAdd(ChaveRotulo.Normalizar(o.RotuloExterno), o.ProcedimentoId);
        }

        var soma = new Dictionary<Guid, int>();
        foreach (var d in demanda)
        {
            if (porRotulo.TryGetValue(ChaveRotulo.Normalizar(d.Rotulo), out var procedimentoId))
            {
                soma[procedimentoId] = soma.GetValueOrDefault(procedimentoId) + d.N;
            }
        }

        return
        [
            .. soma.OrderByDescending(x => x.Value).Take(limite).Select(x => (x.Key, x.Value)),
        ];
    }
}
