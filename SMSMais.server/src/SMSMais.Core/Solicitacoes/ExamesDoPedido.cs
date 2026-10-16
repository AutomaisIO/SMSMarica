using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Solicitacoes;

/// <summary>
/// "Um exame por código interno do pedido" (ADR-0074) num lugar só. Um pedido do SISREG com mais
/// de um procedimento continua sendo UMA solicitação — o SISREG cancela, marca presença e falta no
/// pedido inteiro — e cada procedimento vira o seu exame de imagem, com o seu accession, o seu item
/// de worklist, o seu estudo e o seu laudo.
/// </summary>
public static class ExamesDoPedido
{
    /// <summary>
    /// Accession do exame na posição <paramref name="ordem"/>: o principal (0) leva o número base,
    /// sem sufixo; os seguintes, <c>{base}-1</c>, <c>{base}-2</c>… (Bernardo, 16/10/2026: o
    /// original fica sem nada e o primeiro extra é o -1).
    /// <para>O número base sempre existe, então o gerador do dia continua contando a sequência
    /// pelos números sem sufixo — o extra nunca disputa número com outro pedido.</para>
    /// </summary>
    public static string Accession(string accessionPrincipal, int ordem) =>
        ordem <= 0 ? accessionPrincipal : $"{accessionPrincipal}-{ordem}";

    /// <summary>
    /// O procedimento que este exame executa: o item do pedido, nos extras; os campos da própria
    /// solicitação, no principal. Exige <see cref="ExameImagem.SolicitacaoProcedimento"/> e
    /// <see cref="ExameImagem.Solicitacao"/> carregados.
    /// </summary>
    public static ProcedimentoDoExame Procedimento(ExameImagem exame)
    {
        if (exame.SolicitacaoProcedimento is { } item)
            return new(item.ProcedimentoCodigoSisreg, item.ProcedimentoTexto, item.ProcedimentoSigtapCodigo);
        var s = exame.Solicitacao;
        return new(s?.ProcedimentoCodigoSisreg, s?.ProcedimentoTexto, s?.ProcedimentoSigtapCodigo);
    }

    /// <summary>
    /// Exame que guarda a anamnese do pedido, para cada exame informado. A anamnese é do PEDIDO
    /// (a mesma visita, o mesmo paciente, a mesma pergunta clínica) e mora no exame principal: o
    /// extra lê e grava a do principal, senão o laudo do <c>-1</c> travaria pedindo uma anamnese que
    /// a recepção já preencheu. Exame que é o principal (ou que não se acha) mapeia para ele mesmo.
    /// </summary>
    public static async Task<Dictionary<Guid, Guid>> ExameDaAnamneseAsync(
        SmsMaisDbContext db, IReadOnlyCollection<Guid> exameIds, CancellationToken ct)
    {
        var mapa = exameIds.Distinct().ToDictionary(id => id, id => id);
        if (mapa.Count == 0) return mapa;

        var ids = mapa.Keys.ToArray();
        var extras = await db.ExamesImagem.AsNoTracking()
            .Where(e => ids.Contains(e.Id) && e.Ordem > 0)
            .Select(e => new
            {
                e.Id,
                Principal = db.ExamesImagem
                    .Where(p => p.SolicitacaoPrincipalId == e.SolicitacaoId)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        foreach (var x in extras)
            if (x.Principal is { } principal) mapa[x.Id] = principal;
        return mapa;
    }

    /// <inheritdoc cref="ExameDaAnamneseAsync(SmsMaisDbContext, IReadOnlyCollection{Guid}, CancellationToken)"/>
    public static async Task<Guid> ExameDaAnamneseAsync(SmsMaisDbContext db, Guid exameId, CancellationToken ct) =>
        (await ExameDaAnamneseAsync(db, [exameId], ct))[exameId];
}

/// <summary>Procedimento de um exame: código interno do SISREG (vai em (0008,0100)), nome e
/// SIGTAP como vieram.</summary>
public sealed record ProcedimentoDoExame(string? CodigoSisreg, string? Texto, string? Sigtap);
