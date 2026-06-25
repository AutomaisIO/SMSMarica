namespace SMSMarica.Core.Worklist;

/// <summary>Estudo do PACS reduzido às chaves de casamento (QIDO-RS).</summary>
public sealed record EstudoPacsBasico(string StudyInstanceUID, string? AccessionNumber);

/// <summary>
/// Cliente QIDO-RS. Checa existência por AccessionNumber/StudyInstanceUID e busca
/// estudos por Patient ID (0010,0020) — base da auto-associação na chegada do exame.
/// Usado pelo <c>SincronizadorExamesService</c>.
/// </summary>
public interface IConsultaStudyClient
{
    /// <summary>"Este AccessionNumber já tem study no PACS?"</summary>
    Task<bool> StudyExisteAsync(string accessionNumber, CancellationToken cancellationToken = default);

    /// <summary>"Este StudyInstanceUID existe no PACS?"</summary>
    Task<bool> StudyExistePorStudyUidAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estudos cujo Patient ID (0010,0020) é igual a <paramref name="patientId"/> —
    /// o número da solicitação que o técnico digitou no campo PatientID do equipamento.
    /// </summary>
    Task<IReadOnlyList<EstudoPacsBasico>> BuscarPorPatientIdAsync(string patientId, CancellationToken cancellationToken = default);
}
