using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Vínculo explícito entre um estudo do PACS (<see cref="StudyInstanceUID"/> REAL
/// do dcm4chee, vindo do equipamento) e uma <see cref="SolicitacaoExame"/>.
/// <para>
/// Para exames que chegam via worklist, a associação é IMPLÍCITA (o AccessionNumber
/// "SMS..." casa com a solicitação). Esta entidade cobre os exames que chegaram
/// SEM worklist (accession estranho) e precisam ser ligados ao pedido/paciente para
/// poder laudar — seja manualmente (operador) ou automaticamente (Patient ID DICOM
/// trazendo o nº da solicitação). NUNCA reescrevemos o DICOM no PACS; o vínculo vive
/// só aqui. Soft-delete = "desassociar" (libera o slot único do estudo para
/// reassociar). No máximo 1 associação ATIVA por estudo.
/// </para>
/// </summary>
public class ExameAssociacao
{
    public Guid Id { get; set; }

    /// <summary>StudyInstanceUID REAL do estudo no dcm4chee (o que veio do equipamento). Sem FK local.</summary>
    public string StudyInstanceUID { get; set; } = string.Empty;

    /// <summary>Solicitação à qual o estudo foi vinculado (FK smsmarica → smsmarica).</summary>
    public Guid SolicitacaoExameId { get; set; }
    public SolicitacaoExame? SolicitacaoExame { get; set; }

    /// <summary>
    /// Paciente (fhir.patient) espelhado da solicitação no momento da associação —
    /// <b>sem FK local</b>. Atalho para exibição/auditoria; o NOME continua vindo do
    /// hub FHIR via IPacienteResolver (nunca persistimos nome).
    /// </summary>
    public Guid PacienteId { get; set; }

    /// <summary>Snapshot do AccessionNumber DICOM cru do estudo na associação (auditoria/reversão).</summary>
    public string? AccessionNumberDicomOriginal { get; set; }

    /// <summary>Manual (operador) ou Automatica (Patient ID na chegada).</summary>
    public OrigemAssociacaoExame Origem { get; set; } = OrigemAssociacaoExame.Manual;

    // ---- Auditoria (ADR-0006) ----
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }
}
