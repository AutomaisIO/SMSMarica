namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>
/// Anexo da manifestação (foto, documento, comprovante). O binário vive em <c>smsmarica.midia</c>;
/// aqui fica só a referência, opcionalmente presa ao evento que o trouxe.
/// </summary>
public sealed class OuvidoriaAnexo
{
    public Guid Id { get; set; }
    public Guid ManifestacaoId { get; set; }

    /// <summary>Evento a que o anexo pertence (complementação, resposta da área…); nulo = anexo do registro.</summary>
    public Guid? EventoId { get; set; }

    /// <summary>FK para <c>smsmarica.midia</c>.</summary>
    public Guid MidiaId { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    /// <summary>Se o cidadão vê no acompanhamento. Anexo da área é interno por padrão.</summary>
    public bool VisivelAoCidadao { get; set; }

    public DateTime CriadoEm { get; set; }

    public OuvidoriaManifestacao? Manifestacao { get; set; }
}
