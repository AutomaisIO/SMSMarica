namespace SMSMarica.Core.Pacs;

/// <summary>
/// Reescrita da query string do QIDO-RS para carimbar o recorte por unidade.
///
/// <para>Mora aqui, e não no controller, por dois motivos: é lógica pura (dá para testar sem
/// HTTP nem banco) e é o ponto onde um descuido vaza exame de outra unidade — merece teste
/// próprio.</para>
/// </summary>
public static class EscopoAeQuery
{
    /// <summary>Chave de busca do dcm4chee para o AE que enviou as imagens (tag privada 7777,1037).</summary>
    public const string ChaveAeOrigem = "SendingApplicationEntityTitleOfSeries";

    /// <summary>
    /// AE que não existe e nunca vai existir, carimbado quando o escopo não alcança unidade
    /// nenhuma.
    ///
    /// <para>Não é preciosismo: medido no 5.34.3 de produção, <c>{chave}=</c> com valor VAZIO é
    /// tratado pelo dcm4chee como <b>ausência de filtro</b> e devolve o acervo inteiro (2615
    /// estudos), enquanto um AE desconhecido devolve 0. Ou seja, o caminho "sem acesso" falharia
    /// ABERTO — o oposto do ADR-0037 — se a lista de AEs vazia virasse query. O chamador corta
    /// antes (204), mas esta função não depende disso.</para>
    /// </summary>
    private const string AeImpossivel = "__SEM_ACESSO__";

    /// <summary>
    /// Caminhos de LISTAGEM do QIDO-RS — os únicos em que o recorte por unidade se aplica.
    /// Rotas aninhadas (<c>studies/{uid}/series</c>, <c>.../metadata</c>, frames) já são de um
    /// estudo específico: filtrar por AE ali não recortaria nada e o dcm4chee nem aceitaria a
    /// chave.
    /// </summary>
    public static bool EhListagem(string? caminho) =>
        caminho is "studies" or "studies/count"
                or "series" or "series/count"
                or "instances" or "instances/count";

    /// <summary>
    /// Troca qualquer AE de origem vindo do cliente pelo conjunto que o usuário pode ver.
    ///
    /// <para>Os AEs são escapados um a um e unidos por vírgula LITERAL: é a vírgula que o
    /// dcm4chee lê como "ou" (verificado em produção — US02-CDT(9) + DO-CDT(143) = 152).
    /// Escapar a vírgula junto com o valor transformaria a união num literal e devolveria
    /// zero.</para>
    ///
    /// <para>O parâmetro do cliente é sempre descartado, mesmo com escopo irrestrito: quem tem
    /// acesso global filtra pela unidade ativa, não digitando AE na URL.</para>
    /// </summary>
    public static string Aplicar(string? queryString, EscopoAeResultado escopo)
    {
        var q = queryString ?? string.Empty;
        if (q.StartsWith('?')) q = q[1..];

        var pares = q.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(par => !EhParamAeOrigem(par.Split('=', 2)[0]))
            .ToList();

        if (!escopo.SemRestricao)
        {
            var valor = escopo.SemAcesso
                ? AeImpossivel
                : string.Join(',', escopo.AeTitles.Select(Uri.EscapeDataString));
            pares.Add($"{ChaveAeOrigem}={valor}");
        }

        return pares.Count == 0 ? string.Empty : "?" + string.Join('&', pares);
    }

    /// <summary>O cliente tentou mandar o AE de origem? (palavra-chave ou tag privada 7777,1037).</summary>
    private static bool EhParamAeOrigem(string chave) =>
        chave.StartsWith("SendingApplicationEntityTitle", StringComparison.OrdinalIgnoreCase)
        || chave.Contains("77771037", StringComparison.Ordinal);
}
