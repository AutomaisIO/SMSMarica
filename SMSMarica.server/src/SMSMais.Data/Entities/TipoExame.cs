using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// O procedimento como coisa executável: o que aparece na lista de solicitações, no exame e no
/// laudo, mais o que o DICOM precisa (modalidade, textos do worklist item).
///
/// <para><b>O nome é do SISREG, em MAIÚSCULAS, e não se arbitra sobre ele.</b> Até 10/08/2026 o
/// tipo era ancorado no SIGTAP, e o nome exibido era o rótulo curado à mão daquele SIGTAP — o que
/// apagava a diferença entre procedimentos distintos que compartilham código. Em produção,
/// "ULTRASSOM DE ARTICULAÇÃO" (SIGTAP 0205020062) estava exibindo <b>15 procedimentos diferentes</b>
/// do SISREG sob o mesmo nome (joelho D/E, ombro D/E, punho D/E, mão D/E, tornozelo D/E, antebraço,
/// panturrilha, perna, região inguinal), e "Ultrassom de tireoide" exibia
/// "ULTRA-SONOGRAFIA TRANSFONTANELAR - INFANTIL". Quem diz o que o exame é, é o SISREG.</para>
///
/// <para><b>O SIGTAP virou correlação secundária</b> — existe para faturamento e é opcional aqui.
/// Ele não identifica, não nomeia e não impede a execução.</para>
/// </summary>
public class TipoExame
{
    public Guid Id { get; set; }

    /// <summary>Nome do procedimento como o SISREG o informa, em MAIÚSCULAS. É o que o usuário vê.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Código do procedimento no SISREG (o <c>pa</c>). Respeitado e gravado quando o SISREG o
    /// informa — mas <b>não é a chave</b>: ver <see cref="Solicitacao.ProcedimentoCodigoSisreg"/>
    /// para a medição que mostra a coluna vazia em 33% das linhas. A chave é o <see cref="Nome"/>.
    /// </summary>
    public string? CodigoSisreg { get; set; }

    /// <summary>
    /// Nasceu da importação do SISREG, sem ninguém configurar. Enquanto for true com
    /// <see cref="ModalidadeDicom.Indefinida"/>, o tipo funciona para tudo (lista, laudo, exame)
    /// menos para ir ao PACS — falta a configuração DICOM.
    /// </summary>
    public bool AutoCriado { get; set; }

    /// <summary>Correlação para FATURAMENTO. Opcional — não é identidade nem gate de execução.</summary>
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
