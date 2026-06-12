using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Imagem da assinatura (rubrica visual) de um médico, usada para carimbar o
/// PDF do laudo. Vive no smsmarica (artefato operacional), keyed pelo id do
/// Practitioner no hub FHIR — <b>sem FK</b>, pois o FHIR é serviço à parte
/// (ADR-0010). Não confundir com a assinatura digital ICP-Brasil (PAdES): esta
/// é apenas a rubrica gráfica. 1 assinatura ativa por médico.
/// </summary>
public class AssinaturaMedico
{
    public Guid Id { get; set; }

    /// <summary>Id do Practitioner (hub FHIR) — referência lógica, sem FK cross-serviço.</summary>
    public Guid MedicoId { get; set; }

    /// <summary>Imagem padronizada (PNG) em base64, já enquadrada no frame do formato.</summary>
    public string ImagemBase64 { get; set; } = string.Empty;

    /// <summary>MIME da imagem (ex.: image/png).</summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>Proporção/frame escolhido — orienta o layout do carimbo no PDF.</summary>
    public FormatoAssinaturaMedico Formato { get; set; } = FormatoAssinaturaMedico.Horizontal;

    // ---- Auditoria (ADR-0006) ----
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    public uint RowVersion { get; set; }
}
