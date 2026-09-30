namespace SMSMais.Core.Regulacao.Indicadores.Dtos;

/// <summary>Sistema de regulação de origem dos indicadores.</summary>
public enum FonteIndicadorRegulacao
{
    Sisreg = 1,
    Ser = 2,
    Sernit = 3,

    /// <summary>ESUS de São Gonçalo (ADR-0063).</summary>
    EsusSg = 4,
}

/// <summary>
/// Selo de origem de cada número — é o que impede um número parcial de ser lido como oficial.
/// Mesmos quatro selos do relatório em PDF de 30/09/2026 (docs/regulacao/relatorio-2025-2026).
/// </summary>
public enum SeloIndicador
{
    /// <summary>Lido do próprio sistema de origem (ou do espelho fiel dele).</summary>
    Oficial = 1,

    /// <summary>Derivado de dados oficiais; a regra está na nota.</summary>
    Calculado = 2,

    /// <summary>Sabidamente incompleto — piso; a nota diz o que falta.</summary>
    Parcial = 3,

    /// <summary>O sistema de origem não fornece.</summary>
    Indisponivel = 4,
}

public enum FormatoIndicador
{
    Inteiro = 1,
    Percentual = 2,
    Dias = 3,
}

/// <summary>Uma série mensal. <see cref="Anual"/> traz o valor do ano calculado do jeito certo (razão de
/// somas, mediana de todos os casos) — somar ou tirar média de meses distorce percentual e tempo.</summary>
public sealed record SerieIndicadorDto(
    string Rotulo,
    SeloIndicador Selo,
    string? Nota,
    FormatoIndicador Formato,
    bool Destaque,
    bool Subitem,
    IReadOnlyDictionary<string, decimal?> Valores,
    IReadOnlyDictionary<string, decimal?> Anual,
    AgregacaoIndicador Agregacao);

/// <summary>Como fechar o bloco anual quando não há <see cref="SerieIndicadorDto.Anual"/>.</summary>
public enum AgregacaoIndicador
{
    Soma = 1,
    UltimoMes = 2,
    Media = 3,
}

public sealed record TabelaIndicadorDto(
    string Titulo,
    IReadOnlyList<string> Colunas,
    IReadOnlyList<IReadOnlyList<string?>> Linhas,
    string? Nota);

public sealed record GraficoIndicadorDto(string Tipo, IReadOnlyList<string> Series);

public sealed record ResumoTempoDto(int N, int? Mediana, int? P90, int? Menor, int? Maior);

public sealed record SecaoIndicadorDto(
    string Id,
    string Titulo,
    string? Texto,
    bool Indisponivel,
    IReadOnlyList<SerieIndicadorDto> Series,
    GraficoIndicadorDto? Grafico,
    TabelaIndicadorDto? Motivos,
    IReadOnlyList<TabelaIndicadorDto> Tabelas,
    ResumoTempoDto? ResumoJudicial);

public sealed record IndicadoresRegulacaoDto(
    FonteIndicadorRegulacao Fonte,
    string Sistema,
    string NomeSistema,
    IReadOnlyList<string> Meses,
    DateTime GeradoEm,
    IReadOnlyList<string> Cobertura,
    IReadOnlyList<SecaoIndicadorDto> Secoes);
