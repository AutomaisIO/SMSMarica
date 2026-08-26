using System.Text.Json;
using SMSMais.Core.Conversas;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Verificações de identidade dos comandos do robô. Reutilizado por cancelamento (4 díg CPF +
/// mês/ano nascimento), consulta de exame/laudo (4 díg CPF) e verificação cadastral.
/// </summary>
public static class GateIdentidade
{
    /// <summary>Confere os 3 primeiros dígitos do CPF (aceita CPF inteiro/mais dígitos; usa os 3 primeiros).</summary>
    public static bool CpfInicioConfere(string? cpfPaciente, string? cpfInformado)
    {
        var a = SoDigitos(cpfPaciente);
        var b = SoDigitos(cpfInformado);
        return a.Length >= 3 && b.Length >= 3 && a[..3] == b[..3];
    }

    /// <summary>Confere mês e ano de nascimento.</summary>
    public static bool NascimentoMesAnoConfere(DateOnly? nascimento, int? mes, int? ano) =>
        nascimento is { } d && mes is not null && ano is not null && d.Month == mes && d.Year == ano;

    /// <summary>O número da conversa é o telefone VERIFICADO do paciente? (dispensa o gate de CPF).</summary>
    public static bool NumeroVerificado(string? telefoneVerificado, string? telefoneConversa) =>
        !string.IsNullOrWhiteSpace(telefoneVerificado) && !string.IsNullOrWhiteSpace(telefoneConversa)
        && TelefoneWhatsApp.Canonizar(telefoneVerificado!) == TelefoneWhatsApp.Canonizar(telefoneConversa!);

    public static string SoDigitos(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>Lê um inteiro de um arg JSON (aceita número ou string numérica).</summary>
    public static int? LerInt(JsonElement args, string nome)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(nome, out var el))
            return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(new string(el.GetString()?.Where(char.IsDigit).ToArray()), out var m))
            return m;
        return null;
    }

    public static bool LerBool(JsonElement args, string nome)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(nome, out var el))
            return false;
        if (el.ValueKind == JsonValueKind.True) return true;
        if (el.ValueKind == JsonValueKind.String)
            return string.Equals(el.GetString()?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public static string? LerString(JsonElement args, string nome) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(nome, out var el)
            && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
