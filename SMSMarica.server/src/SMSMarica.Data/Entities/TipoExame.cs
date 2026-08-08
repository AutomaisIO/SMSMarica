using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Curadoria local que torna um procedimento "selecionável no formulário de solicitação" e
/// executável: adiciona modalidade DICOM, textos do worklist item e nome amigável. Ver ADR-0006
/// para auditoria.
///
/// <para><b>O eixo é o procedimento do SISREG</b> (<see cref="SisregProcedimentoId"/>), não o
/// SIGTAP. O SISREG numera procedimento num espaço próprio, derivado de um SIGTAP defasado; os
/// números se parecem e às vezes coincidem, mas não são equivalentes. Pendurar o tipo de exame no
/// SIGTAP já fez o procedimento errado ser escolhido — "transfontanelar" do SISREG caiu na linha
/// SIGTAP de tireoide e o exame de dois recém-nascidos foi para o aparelho rotulado como tireoide.
/// Como o médico regulador escolhe olhando o SISREG, é o SISREG que identifica o procedimento.</para>
/// </summary>
public class TipoExame
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Procedimento do SISREG que este tipo executa — o vínculo que vale.</summary>
    public Guid? SisregProcedimentoId { get; set; }
    public Sisreg.SisregProcedimentoSigtap? SisregProcedimento { get; set; }

    /// <summary>
    /// SIGTAP oficial, quando alguém já confirmou o de-para. Serve ao FATURAMENTO e é opcional:
    /// um procedimento novo do SISREG entra e fica executável sem nunca ter passado por aqui.
    /// <para>Era obrigatório e virou nullable quando o eixo passou a ser o SISREG — os tipos
    /// antigos seguem apontando para cá até serem convertidos.</para>
    /// </summary>
    public Guid? ProcedimentoSigtapId { get; set; }
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

    /// <summary>
    /// Quando false, solicitações deste tipo NÃO são enviadas à Modality Worklist
    /// do PACS (dcm4chee/Fuji). Permite pausar a integração por tipo enquanto o
    /// equipamento não está mapeado para executar via worklist.
    /// </summary>
    public bool EnviarParaWorklist { get; set; } = true;

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
