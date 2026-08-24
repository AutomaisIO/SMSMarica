namespace SMSMais.Core.Common.Tempo;

/// <summary>
/// Fuso de Brasília (GMT-3, sem horário de verão desde 2019). Centraliza a conversão
/// UTC → exibição (documentos/PDFs). REGRA ÚNICA: instantes UTC (timestamptz) são convertidos
/// para Brasília na exibição; <c>data_estudo</c> (wall-clock do DICOM) é exibido AS-IS.
/// </summary>
public static class FusoBrasilia
{
    /// <summary>Offset fixo de Brasília em horas (-3).</summary>
    public const int OffsetHoras = -3;

    /// <summary>Converte um instante UTC para o horário de exibição de Brasília.</summary>
    public static DateTime ParaExibicao(DateTime utc) => utc.AddHours(OffsetHoras);

    /// <inheritdoc cref="ParaExibicao(DateTime)"/>
    public static DateTime? ParaExibicao(DateTime? utc) => utc?.AddHours(OffsetHoras);

    /// <summary>
    /// Início do dia corrente de Brasília, expresso como instante UTC — para comparar com
    /// colunas "timestamp with time zone" (ex.: <c>solicitacao.data_agendada</c>). Usar
    /// <c>DateTime.UtcNow.Date</c> no lugar disso corta o dia às 21h de Brasília.
    /// </summary>
    public static DateTime InicioDoDiaAtualEmUtc() => DateTime.SpecifyKind(
        ParaExibicao(DateTime.UtcNow).Date.AddHours(-OffsetHoras), DateTimeKind.Utc);

    /// <summary>
    /// Caminho inverso de <see cref="ParaExibicao(DateTime)"/>: um wall-clock de Brasília lido de
    /// sistema externo (SER, SISREG, PEP) vira o instante UTC correspondente.
    ///
    /// <para>Existe porque as colunas de instante são <c>timestamp with time zone</c> e o Npgsql
    /// <b>recusa</b> gravar <c>DateTimeKind.Unspecified</c> nelas — o que transforma "esqueci de
    /// converter" em erro de runtime na hora do SaveChanges, não em dado com 3 horas de erro.</para>
    /// </summary>
    public static DateTime DeBrasiliaParaUtc(DateTime brasilia) => DateTime.SpecifyKind(
        DateTime.SpecifyKind(brasilia, DateTimeKind.Unspecified).AddHours(-OffsetHoras),
        DateTimeKind.Utc);
}
