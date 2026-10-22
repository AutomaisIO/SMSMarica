namespace SMSMais.Core.Integracoes.Pep.Estrategias.Klinikos;

/// <summary>
/// Esquema do banco do Klinikos. O produto é o mesmo nas três unidades de Maricá, mas o banco
/// mudou entre as versões — e o conector lê tabela e coluna, então a versão decide o SQL.
///
/// <list type="bullet">
/// <item><see cref="U2025"/> — UPA Maricá e Santa Rita (build <c>U.2025</c>): a evolução carrega o
/// <c>SPA_CODIGO</c> do boletim e os sinais vitais moram em <c>UPA_SinaisVitais</c>.</item>
/// <item><see cref="K2024"/> — Conde (build <c>K.2024</c>, medido em 21/10/2026): a evolução NÃO tem
/// <c>SPA_CODIGO</c> e chega ao boletim por <c>atendamb_codigo</c> →
/// <c>atendimento_ambulatorial.spa_codigo</c>; não existe <c>UPA_SinaisVitais</c> — os sinais
/// ficam no acolhimento, na classificação de risco e na própria evolução; a evolução tem
/// <c>Inativa</c> e <c>Rascunho</c>; o nome social do boletim é <c>spa_nome_social</c>.</item>
/// </list>
///
/// <para>A versão é <b>detectada</b> no banco a cada run, não declarada: é o próprio esquema que
/// diz qual SQL roda, então não há como a fonte afirmar uma versão e o banco ser outra. Esquema
/// que não bate com nenhuma versão conhecida PARA o run — ler coluna que não existe derruba a
/// fase no meio, e "adivinhar" o mais parecido grava prontuário com a ligação errada.</para>
/// </summary>
internal enum KlinikosLayout
{
    U2025,
    K2024,
}

internal static class KlinikosLayoutSonda
{
    /// <summary>
    /// Uma linha, só de metadado (<c>COL_LENGTH</c>/<c>OBJECT_ID</c>): não lê dado nenhum e custa
    /// milissegundos. Cada coluna é 1 quando o objeto existe.
    /// </summary>
    internal static string Sql() => """
        SELECT CASE WHEN COL_LENGTH('dbo.UPA_Evolucao', 'SPA_CODIGO') IS NULL THEN 0 ELSE 1 END AS evo_spa,
               CASE WHEN COL_LENGTH('dbo.UPA_Evolucao', 'atendamb_codigo') IS NULL THEN 0 ELSE 1 END AS evo_atendamb,
               CASE WHEN COL_LENGTH('dbo.UPA_Evolucao', 'Inativa') IS NULL THEN 0 ELSE 1 END AS evo_inativa,
               CASE WHEN COL_LENGTH('dbo.UPA_Evolucao', 'Rascunho') IS NULL THEN 0 ELSE 1 END AS evo_rascunho,
               CASE WHEN OBJECT_ID('dbo.UPA_SinaisVitais', 'U') IS NULL THEN 0 ELSE 1 END AS tab_sinais,
               CASE WHEN COL_LENGTH('dbo.UPA_ACOLHIMENTO', 'aco_pressaoarterial') IS NULL THEN 0 ELSE 1 END AS aco_vitais,
               CASE WHEN COL_LENGTH('dbo.Pronto_Atendimento', 'spa_nome_social') IS NULL THEN 0 ELSE 1 END AS spa_nome_social
        """;

    /// <summary>Colunas da sonda, na ordem do SQL — para a mensagem de erro dizer o que viu.</summary>
    internal static readonly string[] Colunas =
        ["evo_spa", "evo_atendamb", "evo_inativa", "evo_rascunho", "tab_sinais", "aco_vitais", "spa_nome_social"];

    /// <summary>
    /// Versão pelo que existe no banco, ou <c>null</c> quando nenhuma bate por inteiro. As duas
    /// assinaturas exigem TODAS as peças que o SQL daquela versão usa — meia assinatura é esquema
    /// desconhecido, não "quase U.2025".
    /// </summary>
    internal static KlinikosLayout? Detectar(IReadOnlyDictionary<string, bool> existe)
    {
        bool Tem(string c) => existe.TryGetValue(c, out var v) && v;

        if (Tem("evo_spa") && Tem("tab_sinais"))
            return KlinikosLayout.U2025;

        if (!Tem("evo_spa") && !Tem("tab_sinais") && Tem("evo_atendamb") && Tem("evo_inativa")
            && Tem("evo_rascunho") && Tem("aco_vitais") && Tem("spa_nome_social"))
            return KlinikosLayout.K2024;

        return null;
    }
}
