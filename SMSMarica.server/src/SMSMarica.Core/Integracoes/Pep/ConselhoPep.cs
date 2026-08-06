namespace SMSMarica.Core.Integracoes.Pep;

/// <summary>
/// Número de conselho profissional (CRM, COREN…) vindo de PEP.
///
/// <para>Na origem é campo LIVRE, e o cadastro reflete isso. Medido no hub em 06/08/2026, 13
/// profissionais carregavam valores como <c>52137902-8</c>, <c>52.137338-8</c>,
/// <c>52 1341308</c>, <c>821.756</c> e <c>&amp;nbsp;</c>.</para>
///
/// <para>A régua é deliberadamente conservadora: <b>normaliza, não descarta</b>. A maioria
/// daqueles valores é registro REAL só mal formatado — jogar fora perderia dado profissional
/// legítimo. Some apenas o que não sobra nada depois de tirar a formatação (o
/// <c>&amp;nbsp;</c> vira string vazia).</para>
/// </summary>
public static class ConselhoPep
{
    /// <summary>Mínimo de dígitos para um número de conselho ser levado a sério.</summary>
    private const int MinimoDigitos = 3;

    /// <summary>
    /// Forma canônica (só dígitos), ou <c>null</c> quando não sobra número nenhum de verdade.
    /// </summary>
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var digitos = new string([.. valor.Where(char.IsDigit)]);
        return digitos.Length >= MinimoDigitos ? digitos : null;
    }

    /// <summary>O valor já está na forma canônica? Usado para não arrastar lixo antigo adiante.</summary>
    public static bool Valido(string? valor) =>
        valor is not null && Normalizar(valor) == valor;
}
