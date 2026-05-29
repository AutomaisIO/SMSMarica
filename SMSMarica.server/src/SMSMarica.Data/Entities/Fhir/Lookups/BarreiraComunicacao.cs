namespace SMSMarica.Data.Entities.Fhir.Lookups;

/// <summary>
/// Barreira de comunicação (acessibilidade — DataSUS). Surdez, cegueira,
/// mudez, autismo, intelectual, idioma estrangeiro, etc. Referenciada por
/// PatientCommunication.barreira_comunicacao_id.
/// </summary>
public sealed class BarreiraComunicacao
{
    public int Codigo { get; set; }
    public string Nome { get; set; } = string.Empty;
}
