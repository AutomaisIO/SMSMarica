using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Geo;

/// <summary>
/// Cache local de geocodificação (schema smsmarica). Evita regeocodificar o mesmo
/// endereço e mantém o hub FHIR intocado. Chave = hash do endereço normalizado.
/// </summary>
/// <remarks>
/// Antiga <c>Geocodigo</c> / <c>tfd_geocodigo</c>. Geocodificação é infraestrutura
/// compartilhada (o TFD é um dos consumidores, não o dono) — ADR-0038.
/// </remarks>
public class GeoEndereco
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 do endereço normalizado (lowercase, sem acentos). Único.</summary>
    public string Hash { get; set; } = string.Empty;
    public string EnderecoNormalizado { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public FonteGeocodigo Fonte { get; set; } = FonteGeocodigo.Google;

    /// <summary>Precisão reportada pela fonte (ex.: ROOFTOP, APPROXIMATE).</summary>
    public string? Precisao { get; set; }

    /// <summary>Geocódigo de baixa confiança/pendente de revisão (pin manual) pelo gestor.</summary>
    public bool RevisaoPendente { get; set; }

    public DateTime GeocodificadoEm { get; set; }
}
