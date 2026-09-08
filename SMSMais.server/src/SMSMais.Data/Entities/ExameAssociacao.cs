using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

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

    /// <summary>Exame de imagem (execução) ao qual o estudo foi vinculado (FK smsmarica → smsmarica).</summary>
    public Guid ExameImagemId { get; set; }
    public ExameImagem? ExameImagem { get; set; }

    /// <summary>
    /// Paciente (fhir.patient) espelhado da solicitação no momento da associação —
    /// <b>sem FK local</b>. Atalho para exibição/auditoria; o NOME continua vindo do
    /// hub FHIR via IPacienteResolver (nunca persistimos nome).
    /// </summary>
    public Guid PacienteId { get; set; }

    /// <summary>Snapshot do AccessionNumber DICOM cru do estudo na associação (auditoria/reversão).</summary>
    public string? AccessionNumberDicomOriginal { get; set; }

    /// <summary>
    /// <b>PatientID que estava DENTRO do objeto</b> antes da reescrita — o que a técnica digitou no
    /// equipamento (uma data, um timestamp, às vezes o nome da pessoa). Guardado porque a reescrita
    /// substitui a identidade no arquivo: sem este campo, depois de associar não sobra prova de como
    /// o estudo chegou, e uma associação para o paciente errado fica sem rastro.
    /// </summary>
    public string? PatientIdDicomOriginal { get; set; }

    /// <summary>PatientName cru do objeto antes da reescrita. Mesmo motivo do
    /// <see cref="PatientIdDicomOriginal"/>.</summary>
    public string? NomePacienteDicomOriginal { get; set; }

    /// <summary>
    /// StudyInstanceUID do estudo ANTES da reescrita. O estudo reescrito nasce com UID novo (o
    /// antigo está colado ao que foi rejeitado), então sem isto não há como achar o original no
    /// acervo — que agora fica rejeitado, e não apagado, por 90 dias.
    /// </summary>
    public string? StudyInstanceUidOriginal { get; set; }

    /// <summary>Manual (operador) ou Automatica (Patient ID na chegada).</summary>
    public OrigemAssociacaoExame Origem { get; set; } = OrigemAssociacaoExame.Manual;

    /// <summary>
    /// Status da solicitação ANTES de a associação promovê-la a Realizada. Guardado
    /// para que o "desassociar" reverta ao ponto anterior. Null quando a solicitação
    /// já estava em Realizada/Laudada/Cancelada (a associação não a promoveu).
    /// </summary>
    public StatusSolicitacaoExame? StatusSolicitacaoAnterior { get; set; }

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
