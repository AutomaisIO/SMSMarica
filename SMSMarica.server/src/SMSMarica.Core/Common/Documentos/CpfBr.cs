namespace SMSMarica.Core.Common.Documentos;

/// <summary>
/// CPF como chave de identidade nacional.
///
/// <para><b>A régua é o dígito verificador, não "11 dígitos"</b> — adendo do ADR-0041. Um
/// <c>00000000000</c> (preenchimento clássico de campo obrigatório na origem) tem 11 dígitos e
/// passaria como chave nacional, <b>fundindo duas pessoas</b> num único Patient. Já mordeu na
/// importação do Salux. Por isso todo ponto que decide "isto ancora identidade?" pergunta
/// <see cref="EhValido"/>, nunca o comprimento.</para>
///
/// <para>O nome é <c>CpfBr</c> e não <c>Cpf</c> de propósito: quase toda classe que precisa
/// validar também tem uma propriedade <c>Cpf</c>, e o nome curto ficaria sombreado.</para>
/// </summary>
public static class CpfBr
{
    /// <summary>Só os dígitos — aceita com ou sem máscara.</summary>
    public static string SoDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new string([.. valor.Where(char.IsDigit)]);

    /// <summary>
    /// CPF válido: 11 dígitos, não todos iguais, e os dois dígitos verificadores conferem.
    /// Aceita entrada com máscara.
    /// </summary>
    public static bool EhValido(string? valor)
    {
        var d = SoDigitos(valor);
        if (d.Length != 11) return false;

        // Repetições (000..., 111..., 999...) passam no cálculo do DV, mas não são CPF de ninguém.
        if (d.All(c => c == d[0])) return false;

        return Digito(d, 9) == d[9] - '0' && Digito(d, 10) == d[10] - '0';
    }

    /// <summary>DV da posição <paramref name="ate"/>, pelo módulo 11 da Receita.</summary>
    private static int Digito(string d, int ate)
    {
        var soma = 0;
        for (var i = 0; i < ate; i++) soma += (d[i] - '0') * (ate + 1 - i);
        var resto = soma * 10 % 11;
        return resto == 10 ? 0 : resto;
    }
}
