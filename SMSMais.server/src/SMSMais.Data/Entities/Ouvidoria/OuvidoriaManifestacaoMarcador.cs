namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>Ligação N:N manifestação × marcador. Chave composta <c>(ManifestacaoId, MarcadorId)</c>.</summary>
public sealed class OuvidoriaManifestacaoMarcador
{
    public Guid ManifestacaoId { get; set; }
    public Guid MarcadorId { get; set; }

    public OuvidoriaManifestacao? Manifestacao { get; set; }
    public OuvidoriaMarcador? Marcador { get; set; }
}
