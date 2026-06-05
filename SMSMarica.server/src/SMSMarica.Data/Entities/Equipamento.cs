using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Equipamento de uma unidade (ex.: mamógrafo, ultrassom, tomógrafo) — recurso
/// agendável para exames de imagem. Infra operacional do <c>smsmarica</c>; canônico
/// FHIR seria Device, mas fica local (ver ADR-0013).
/// </summary>
public class Equipamento
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public Guid UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    public ModalidadeDicom ModalidadeDicom { get; set; }

    /// <summary>Identificador DICOM opcional (AE Title / Station Name) — para o worklist do PACS no futuro.</summary>
    public string? IdentificadorDicom { get; set; }

    /// <summary>Visibilidade no agendamento. Soft-delete via <see cref="ExcluidoEm"/>.</summary>
    public bool Ativo { get; set; } = true;

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
