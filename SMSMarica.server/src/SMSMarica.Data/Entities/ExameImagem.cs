using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Satélite de EXECUÇÃO de um exame de imagem — o artefato DICOM/PACS de uma
/// <see cref="Solicitacao"/> de categoria <see cref="CategoriaSolicitacao.Imagem"/>. Aderente ao
/// FHIR R4 <c>ImagingStudy</c>. Carrega as chaves que viajam com o estudo (AccessionNumber,
/// StudyInstanceUID), a integração com a Modality Worklist do dcm4chee e o ciclo de aquisição.
///
/// A regulação (paciente, unidades, solicitante, datas, nº SISREG, confirmação) NÃO está aqui —
/// vive na <see cref="Solicitacao"/> pai. O laudo se liga por <see cref="StudyInstanceUID"/> (sem
/// FK). Ver ADR-0021. Auditoria conforme ADR-0006.
/// </summary>
public class ExameImagem
{
    public Guid Id { get; set; }

    /// <summary>Regulação-pai (o pedido). 1 solicitação de imagem : 1 execução.</summary>
    public Guid SolicitacaoId { get; set; }
    public Solicitacao? Solicitacao { get; set; }

    // ---- Mapeamento clínico → DICOM ----

    /// <summary>Tipo de exame (modalidade DICOM + textos do worklist). NULLABLE = mapeamento
    /// pendente: o exame é importado mesmo sem casar o SIGTAP com um <c>TipoExame</c>, e a tela de
    /// mapeamento resolve depois (ADR-0021). Sem tipo, não vai à worklist.</summary>
    public Guid? TipoExameId { get; set; }
    public TipoExame? TipoExame { get; set; }

    // ---- Identificação DICOM ----

    /// <summary>"SMS{aaaa}{seq6}" — chave do pedido no equipamento e no Study (0008,0050).</summary>
    public string AccessionNumber { get; set; } = string.Empty;

    /// <summary>Pre-gerado (2.25.{guid-numerico}) — vai no Study e no Workitem. Após conciliação,
    /// pode ser substituído pelo UID REAL do estudo (ADR-0016).</summary>
    public string StudyInstanceUID { get; set; } = string.Empty;

    /// <summary>UID do workitem no dcm4chee (retornado pelo POST). Permite cancelar/atualizar.</summary>
    public string? WorklistItemUid { get; set; }

    /// <summary>Estação que vai executar — define o ScheduledStationAETitle e o WorklistLabel do
    /// item MWL. Nulo = deduzido da unidade executante + modalidade no momento do envio (caminho
    /// normal, quando a unidade tem um único equipamento na modalidade). Preenchido quando a
    /// recepção teve de ESCOLHER entre dois ou mais — aí a decisão fica registrada no exame, e não
    /// no acaso da ordem alfabética.</summary>
    public Guid? EquipamentoId { get; set; }
    public Equipamento? Equipamento { get; set; }

    // ---- Ciclo de aquisição ----

    public StatusSolicitacaoExame Status { get; set; } = StatusSolicitacaoExame.Solicitada;

    public DateTime? IniciadoEm { get; set; }

    /// <summary>Momento em que o servidor DETECTOU o exame no PACS (auditoria, UTC).</summary>
    public DateTime? RealizadoEm { get; set; }

    /// <summary>Data/hora REAL de execução, lida do DICOM (StudyDate+StudyTime) — fonte da verdade
    /// da data do exame/laudo. Wall-clock local (Kind=Unspecified) → timestamp without time zone.</summary>
    public DateTime? DataEstudo { get; set; }

    // Autorização da recepção vive na <see cref="Solicitacao"/> (é a frente de recepção, aplicável a
    // qualquer categoria). O envio ao PACS reage a ela: enfileira quando a solicitação é autorizada.

    // ---- Integração PACS / retry resiliente ----

    public string? ErroIntegracaoPacs { get; set; }
    public int TentativasEnvio { get; set; }
    public DateTime? UltimaTentativaEm { get; set; }

    /// <summary>Quando o worker deve (re)tentar. Null = sem tentativa agendada (estados terminais /
    /// ainda não autorizado). NÃO é preenchido na criação: nada vai ao PACS até a recepção autorizar.</summary>
    public DateTime? ProximaTentativaEm { get; set; }

    // ---- Cache de imagens/PDF (PreparadorImagensExameService) ----

    public DateTime? ImagensPreparadasEm { get; set; }
    public int ImagensPreparacaoTentativas { get; set; }

    // ---- Auditoria ADR-0006 ----

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    /// <summary>Concorrência otimista (PG xmin) — o worker de worklist atualiza concorrentemente.</summary>
    public uint RowVersion { get; set; }
}
