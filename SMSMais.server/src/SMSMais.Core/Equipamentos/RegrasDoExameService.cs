using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Equipamentos;

/// <summary>
/// Resolve a <see cref="RegraDoExame"/> de cada exame pelo aparelho que o fez (ADR-0075).
/// </summary>
public interface IRegrasDoExameService
{
    /// <summary>A regra de um exame (id público = <c>ExameImagem.Id</c>). Exame inexistente =
    /// <see cref="RegraDoExame.SemAparelho"/>.</summary>
    Task<RegraDoExame> ResolverAsync(Guid exameImagemId, CancellationToken ct = default);

    /// <summary>A regra de cada exame, em poucas consultas — para listas.</summary>
    Task<IReadOnlyDictionary<Guid, RegraDoExame>> ResolverAsync(
        IReadOnlyCollection<Guid> exameImagemIds, CancellationToken ct = default);
}

/// <summary>
/// O aparelho do exame, na ordem em que a decisão foi registrada:
/// <list type="number">
/// <item>o gravado no exame (a recepção escolheu a sala, ou a worklist amarrou);</item>
/// <item>o aparelho fixo do escopo da unidade para aquele tipo (<c>tipo_exame_unidade</c>);</item>
/// <item>os aparelhos ativos da unidade executante na modalidade do tipo — um só, é ele; vários,
/// <see cref="RegraDoExame.Combinar"/>.</item>
/// </list>
/// É a mesma dedução do envio à worklist (<c>ResolvedorEstacaoWorklist</c>), com uma diferença de
/// propósito: lá, vários candidatos é pergunta à recepção; aqui, basta que concordem. Em 20/10/2026,
/// os 78 exames sem aparelho definido eram todos de ultrassom, em unidades com dois ultrassons.
/// </summary>
public sealed class RegrasDoExameService(SmsMaisDbContext db) : IRegrasDoExameService
{
    public async Task<RegraDoExame> ResolverAsync(Guid exameImagemId, CancellationToken ct = default)
    {
        var regras = await ResolverAsync([exameImagemId], ct);
        return regras.TryGetValue(exameImagemId, out var regra) ? regra : RegraDoExame.SemAparelho;
    }

    public async Task<IReadOnlyDictionary<Guid, RegraDoExame>> ResolverAsync(
        IReadOnlyCollection<Guid> exameImagemIds, CancellationToken ct = default)
    {
        if (exameImagemIds.Count == 0) return new Dictionary<Guid, RegraDoExame>();
        var ids = exameImagemIds.Distinct().ToArray();

        var exames = await db.ExamesImagem.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                e.EquipamentoId,
                e.TipoExameId,
                Modalidade = e.TipoExame != null ? (ModalidadeDicom?)e.TipoExame.ModalidadeDicom : null,
                UnidadeId = e.Solicitacao!.UnidadeExecutanteId,
            })
            .ToListAsync(ct);
        if (exames.Count == 0) return new Dictionary<Guid, RegraDoExame>();

        var unidades = exames.Select(e => e.UnidadeId).Distinct().ToArray();
        var tipos = exames.Where(e => e.TipoExameId != null).Select(e => e.TipoExameId!.Value).Distinct().ToArray();

        // Aparelho fixo do escopo: o ativo primeiro, como na worklist.
        var escopos = tipos.Length == 0
            ? []
            : await db.TiposExameUnidade.AsNoTracking()
                .Where(t => t.ExcluidoEm == null && t.EquipamentoId != null
                    && tipos.Contains(t.TipoExameId) && unidades.Contains(t.UnidadeId))
                .OrderByDescending(t => t.Ativo)
                .Select(t => new { t.TipoExameId, t.UnidadeId, EquipamentoId = t.EquipamentoId!.Value })
                .ToListAsync(ct);
        var fixoPorPar = escopos
            .GroupBy(t => (t.TipoExameId, t.UnidadeId))
            .ToDictionary(g => g.Key, g => g.First().EquipamentoId);

        // O aparelho apontado (pelo exame ou pelo escopo) vale mesmo se hoje estiver inativo ou
        // excluído: foi ele que fez o exame. Os candidatos por modalidade, só os que estão em uso.
        var apontados = exames.Where(e => e.EquipamentoId != null).Select(e => e.EquipamentoId!.Value)
            .Concat(fixoPorPar.Values)
            .Distinct()
            .ToArray();
        var aparelhos = await db.Equipamentos.AsNoTracking()
            .Where(e => apontados.Contains(e.Id)
                || (unidades.Contains(e.UnidadeId) && e.Ativo && e.ExcluidoEm == null))
            .ToListAsync(ct);
        var porId = aparelhos.ToDictionary(e => e.Id, RegraDoExame.De);
        var candidatosPorPar = aparelhos
            .Where(e => e.Ativo && e.ExcluidoEm == null)
            .GroupBy(e => (e.UnidadeId, e.ModalidadeDicom))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RegraDoExame>)[.. g.OrderBy(e => e.Nome).Select(RegraDoExame.De)]);

        var resultado = new Dictionary<Guid, RegraDoExame>(exames.Count);
        foreach (var e in exames)
        {
            if (e.EquipamentoId is { } apontado && porId.TryGetValue(apontado, out var doExame))
                resultado[e.Id] = doExame;
            else if (e.TipoExameId is { } tipo
                     && fixoPorPar.TryGetValue((tipo, e.UnidadeId), out var fixo)
                     && porId.TryGetValue(fixo, out var doEscopo))
                resultado[e.Id] = doEscopo;
            else if (e.Modalidade is { } modalidade
                     && candidatosPorPar.TryGetValue((e.UnidadeId, modalidade), out var candidatos))
                resultado[e.Id] = RegraDoExame.Combinar(candidatos);
            else
                resultado[e.Id] = RegraDoExame.SemAparelho;
        }
        return resultado;
    }
}
