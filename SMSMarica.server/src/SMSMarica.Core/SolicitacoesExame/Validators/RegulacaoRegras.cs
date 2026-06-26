namespace SMSMarica.Core.SolicitacoesExame.Validators;

/// <summary>
/// Régua do Código de Solicitação e da Chave de Confirmação (regulação): ambos obrigatórios.
/// Um valor é válido quando é o sentinela <c>0000</c> (exame extra-SUS em caráter emergencial)
/// OU um número a partir de 9999. Números abaixo de 9999 (e diferentes de 0000) são inválidos.
/// </summary>
public static class RegulacaoRegras
{
    public const string Sentinela = "0000";
    public const long Minimo = 9999;

    public const string MensagemInvalido =
        "Use 0000 (exame emergencial extra-SUS) ou um número a partir de 9999.";

    public static bool Valido(string? valor)
    {
        var v = (valor ?? string.Empty).Trim();
        if (v.Length == 0) return false;
        if (v == Sentinela) return true;
        return long.TryParse(v, out var n) && n >= Minimo;
    }
}
