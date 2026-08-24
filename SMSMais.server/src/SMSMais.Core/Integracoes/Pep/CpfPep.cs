namespace SMSMais.Core.Integracoes.Pep;

/// <summary>
/// Validação de CPF para uso como ÂNCORA DE IDENTIDADE no sincronismo. A régua anterior era
/// só <c>length == 11</c> — e a auditoria de 04/08/2026 mostrou o custo: "00000000000" (o
/// preenchimento clássico de campo obrigatório na recepção) passaria como chave nacional e
/// FUNDIRIA duas pessoas diferentes num único Patient, em silêncio. Fundir prontuários é o
/// pior desfecho possível deste sistema; CPF que não prova ser CPF não ancora nada.
/// </summary>
public static class CpfPep
{
    /// <summary>Só dígitos (o Klinikos guarda CPF em char com máscara/padding variados).</summary>
    public static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    /// <summary>
    /// CPF VÁLIDO: 11 dígitos, não-repdigit e com os dois dígitos verificadores corretos.
    /// É o teste que separa "chave nacional" de "preenchimento de campo obrigatório".
    /// </summary>
    public static bool Valido(string? cpf)
    {
        var d = Digitos(cpf);
        if (d.Length != 11) return false;
        if (d.Distinct().Count() == 1) return false; // 000…, 111…, 999…

        var dv1 = Dv(d, 10, 9);
        var dv2 = Dv(d, 11, 10);
        return d[9] - '0' == dv1 && d[10] - '0' == dv2;
    }

    private static int Dv(string d, int pesoInicial, int quantos)
    {
        var soma = 0;
        for (var i = 0; i < quantos; i++) soma += (d[i] - '0') * (pesoInicial - i);
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
