using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Curadoria local que traduz um <see cref="ProcedimentoSigtap"/> em algo
/// "selecionável no formulário de solicitação". Adiciona o que o SIGTAP não
/// tem (modalidade DICOM, textos do worklist item) e permite que a equipe da
/// SMS personalize nomes amigáveis. Ver ADR-0006 para auditoria.
/// </summary>
public class TipoExame
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public Guid ProcedimentoSigtapId { get; set; }
    public ProcedimentoSigtap? ProcedimentoSigtap { get; set; }

    public ModalidadeDicom ModalidadeDicom { get; set; }

    /// <summary>
    /// Vai literal para a tag DICOM (0032,1060) Requested Procedure Description
    /// quando criamos o worklist item.
    /// </summary>
    public string RequestedProcedureDescription { get; set; } = string.Empty;

    /// <summary>Tag (0040,0007) Scheduled Procedure Step Description.</summary>
    public string ScheduledProcedureStepDescription { get; set; } = string.Empty;

    /// <summary>Códigos opcionais de protocolo (vai para tag 0040,0008).</summary>
    public List<string> CodigosProtocolo { get; set; } = [];

    public int? TempoEstimadoMinutos { get; set; }

    public Guid? UnidadePadraoId { get; set; }
    public Unidade? UnidadePadrao { get; set; }

    /// <summary>Visibilidade no formulário de solicitação. Soft-delete via <see cref="ExcluidoEm"/>.</summary>
    public bool Ativo { get; set; } = true;

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
