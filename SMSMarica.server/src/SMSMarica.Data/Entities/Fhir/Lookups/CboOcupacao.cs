namespace SMSMarica.Data.Entities.Fhir.Lookups;

/// <summary>
/// Classificação Brasileira de Ocupações. Código de 6 dígitos.
/// Referenciada por Patient.occupation_cbo (extensão eSUS).
/// </summary>
public sealed class CboOcupacao
{
    /// <summary>Código CBO de 6 dígitos (chave natural).</summary>
    public int Codigo { get; set; }

    public string Titulo { get; set; } = string.Empty;
}
