using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;

namespace SMSMarica.Core.Pacs;

/// <summary>
/// De onde veio um estudo do PACS: equipamento e unidade, deduzidos do AE Title de origem.
///
/// <para>Serve o caso que antes não tinha resposta — o estudo <b>órfão</b>. Sem vínculo com
/// solicitação não há paciente, tipo nem unidade do nosso lado; mas o AE que enviou as imagens
/// viaja junto delas e é o <c>Equipamento.IdentificadorDicom</c> cadastrado, que pertence a uma
/// unidade. Então "não dá pra saber de qual unidade é" era falso: dá, pela máquina.</para>
///
/// <para>Custa uma consulta QIDO por estudo (a tag só volta no nível de série), então a listagem
/// chama isto <b>só para as linhas sem associação</b> — as com vínculo já trazem a unidade do
/// próprio pedido.</para>
/// </summary>
public interface IOrigemEstudoService
{
    Task<IReadOnlyList<OrigemEstudoDto>> ResolverAsync(
        IReadOnlyList<string> studyInstanceUIDs, CancellationToken cancellationToken = default);
}

/// <param name="AeTitle">AE de origem lido do PACS.</param>
/// <param name="EquipamentoNome">Equipamento cadastrado com esse AE, ou <c>null</c> se o AE não é
/// de nenhum equipamento conhecido (ex.: o acervo legado importado sob outro AE).</param>
/// <param name="UnidadeNome">Unidade do equipamento, quando conhecido.</param>
public sealed record OrigemEstudoDto(
    string StudyInstanceUID,
    string AeTitle,
    string? EquipamentoNome,
    string? UnidadeNome);

internal sealed class OrigemEstudoService(SmsMaricaDbContext db, IConsultaStudyClient consulta)
    : IOrigemEstudoService
{
    /// <summary>Teto por chamada: é uma página da listagem, não uma varredura.</summary>
    private const int MaximoPorLote = 60;

    /// <summary>Consultas simultâneas ao PACS — o suficiente para uma página não pesar.</summary>
    private const int Paralelismo = 6;

    public async Task<IReadOnlyList<OrigemEstudoDto>> ResolverAsync(
        IReadOnlyList<string> studyInstanceUIDs, CancellationToken cancellationToken = default)
    {
        var uids = (studyInstanceUIDs ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaximoPorLote)
            .ToArray();
        if (uids.Length == 0) return [];

        var aePorUid = new Dictionary<string, string>(StringComparer.Ordinal);
        using var limitador = new SemaphoreSlim(Paralelismo);
        var tarefas = uids.Select(async uid =>
        {
            await limitador.WaitAsync(cancellationToken);
            try
            {
                // ObterAeOrigemAsync é tolerante: PACS fora do ar devolve null e a linha
                // simplesmente fica sem badge de origem — nunca derruba a listagem.
                return (Uid: uid, Ae: await consulta.ObterAeOrigemAsync(uid, cancellationToken));
            }
            finally
            {
                limitador.Release();
            }
        });

        foreach (var (uid, ae) in await Task.WhenAll(tarefas))
        {
            if (!string.IsNullOrWhiteSpace(ae)) aePorUid[uid] = ae;
        }
        if (aePorUid.Count == 0) return [];

        var aes = aePorUid.Values.Distinct(StringComparer.Ordinal).ToArray();
        var equipamentos = await db.Equipamentos.AsNoTracking()
            .Where(e => e.ExcluidoEm == null && e.IdentificadorDicom != null && aes.Contains(e.IdentificadorDicom))
            .Select(e => new { Ae = e.IdentificadorDicom!, e.Nome, UnidadeNome = e.Unidade!.Nome })
            .ToListAsync(cancellationToken);
        var porAe = equipamentos
            .GroupBy(e => e.Ae, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return [.. aePorUid.Select(par =>
        {
            var equip = porAe.GetValueOrDefault(par.Value);
            return new OrigemEstudoDto(par.Key, par.Value, equip?.Nome, equip?.UnidadeNome);
        })];
    }
}
