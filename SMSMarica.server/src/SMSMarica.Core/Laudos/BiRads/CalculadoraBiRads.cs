namespace SMSMarica.Core.Laudos.BiRads;

/// <summary>
/// Motor de cálculo da categoria BI-RADS (ACR BI-RADS® 5ª edição) para laudos
/// de mamografia.
///
/// <para>
/// Regra-mãe (NÃO é soma de pontos): a categoria final é o <b>achado mais
/// suspeito</b> entre os itens marcados — o "most suspicious finding drives the
/// category". Marcar 5 frases benignas (BI-RADS 2) e 1 frase de calcificações
/// finas lineares segmentares (BI-RADS 4C) resulta em <b>4C</b>.
/// </para>
///
/// <para>
/// A categoria 0 (incompleto) é ortogonal à escala de suspeição: significa que o
/// estudo ainda não pode ser avaliado (faltam incidências/US/comparação). Por
/// isso ela só "vence" quando o achado definitivo mais alto é benigno/negativo
/// (1 ou 2) — diante de um achado suspeito (3+), a suspeição domina.
/// </para>
///
/// <para>
/// O resultado é sempre uma <b>SUGESTÃO</b>: a profissional pode sobrescrever
/// (o serviço guarda tanto o sugerido quanto o final).
/// </para>
/// </summary>
public static class CalculadoraBiRads
{
    /// <summary>Categorias canônicas aceitas (string preserva 4A/4B/4C no laudo).</summary>
    public static readonly IReadOnlyList<string> Categorias =
        ["0", "1", "2", "3", "4", "4A", "4B", "4C", "5", "6"];

    /// <summary>Normaliza para a forma canônica ("4a" → "4A"), ou null se vazio.</summary>
    public static string? Normalizar(string? categoria) =>
        string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim().ToUpperInvariant();

    /// <summary>True se a string é uma categoria BI-RADS válida.</summary>
    public static bool EhCategoriaValida(string? categoria) =>
        Normalizar(categoria) is { } n && Categorias.Contains(n);

    /// <summary>
    /// Ordem de suspeição: quanto maior o rank, mais suspeito. A categoria 0
    /// (incompleto) fica fora da escala (tratada à parte em <see cref="Sugerir"/>).
    /// </summary>
    private static int? RankSuspeicao(string? categoria) => Normalizar(categoria) switch
    {
        "1" => 10,
        "2" => 20,
        "3" => 30,
        "4" => 40,
        "4A" => 41,
        "4B" => 42,
        "4C" => 43,
        "5" => 50,
        "6" => 60,
        _ => null, // "0", null ou inválido
    };

    /// <summary>
    /// Sugere a categoria BI-RADS a partir das contribuições dos itens marcados.
    /// Itens sem contribuição (null/vazio) são ignorados. Retorna null quando
    /// nenhum item carrega contribuição.
    /// </summary>
    public static string? Sugerir(IEnumerable<string?> contribuicoes)
    {
        var validas = (contribuicoes ?? [])
            .Select(Normalizar)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        if (validas.Count == 0) return null;

        var temIncompleto = validas.Contains("0");
        var comRank = validas.Where(c => RankSuspeicao(c) is not null).ToList();

        if (comRank.Count == 0)
            return temIncompleto ? "0" : null;

        var maisSuspeito = comRank
            .OrderByDescending(c => RankSuspeicao(c)!.Value)
            .First();

        // 0 (incompleto) só prevalece quando o achado definitivo mais alto é
        // benigno/negativo (≤ BI-RADS 2): "parece benigno, mas preciso completar".
        if (temIncompleto && RankSuspeicao(maisSuspeito) <= RankSuspeicao("2"))
            return "0";

        return maisSuspeito;
    }

    /// <summary>
    /// Conduta/recomendação padrão associada à categoria (ACR 5ª ed.). Texto-base
    /// para o laudo; a profissional pode ajustar. String vazia se categoria inválida.
    /// </summary>
    public static string Conduta(string? categoria) => Normalizar(categoria) switch
    {
        "0" => "Avaliação incompleta — necessita de incidências adicionais, "
             + "ultrassonografia complementar ou comparação com exames anteriores.",
        "1" => "Mamografia negativa. Rastreamento de rotina conforme a faixa etária/risco.",
        "2" => "Achados benignos. Rastreamento de rotina conforme a faixa etária/risco.",
        "3" => "Achado provavelmente benigno. Recomenda-se controle por imagem em 6 meses.",
        "4" or "4A" or "4B" or "4C" =>
            "Achado suspeito. Recomenda-se correlação com estudo histopatológico (biópsia).",
        "5" => "Achado altamente suspeito de malignidade. Recomenda-se biópsia/conduta apropriada.",
        "6" => "Malignidade comprovada por biópsia.",
        _ => string.Empty,
    };
}
