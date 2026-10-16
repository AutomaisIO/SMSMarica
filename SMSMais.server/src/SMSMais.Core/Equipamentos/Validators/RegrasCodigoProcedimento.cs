using System.Text.RegularExpressions;

namespace SMSMais.Core.Equipamentos.Validators;

/// <summary>Régua do esquema do código do procedimento (0008,0102) — ADR-0074. É VR <c>SH</c>: até
/// 16 caracteres; fica restrito a letras, dígitos, ponto, hífen e sublinhado para não levar ao
/// aparelho o que ele não sabe ler (acento, barra invertida, espaço).</summary>
internal static partial class RegrasCodigoProcedimento
{
    public const string MensagemEsquema =
        "O esquema do código vai até 16 caracteres: letras, números, ponto, hífen ou sublinhado (ex.: 99SMS).";

    public static bool EsquemaValido(string? esquema) =>
        string.IsNullOrWhiteSpace(esquema) || Esquema().IsMatch(esquema.Trim());

    [GeneratedRegex("^[A-Za-z0-9._-]{1,16}$")]
    private static partial Regex Esquema();
}
