namespace SMSMarica.Data.Entities.Fhir.Lookups;

/// <summary>
/// Etnia indígena (catálogo DataSUS). Obrigatório quando Patient.race = Indigena
/// (extensão br-individual-ethnicGroup).
/// </summary>
public sealed class EtniaIndigena
{
    /// <summary>Código DataSUS da etnia (chave natural).</summary>
    public int Codigo { get; set; }

    public string Nome { get; set; } = string.Empty;
}
