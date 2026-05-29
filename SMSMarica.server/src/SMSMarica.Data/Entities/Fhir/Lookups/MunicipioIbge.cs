namespace SMSMarica.Data.Entities.Fhir.Lookups;

/// <summary>
/// Município brasileiro (código IBGE 7 dígitos). Lookup populada por seed
/// das tabelas oficiais do IBGE. Referenciada por Address.municipio_ibge_id
/// e Patient.birth_municipio_id (extensão birthPlace).
/// </summary>
public sealed class MunicipioIbge
{
    /// <summary>Código IBGE de 7 dígitos (chave natural).</summary>
    public int Codigo { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>UF de 2 letras (RJ, SP…).</summary>
    public string Uf { get; set; } = string.Empty;
}
