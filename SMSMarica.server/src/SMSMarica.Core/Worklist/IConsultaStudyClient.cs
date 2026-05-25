namespace SMSMarica.Core.Worklist;

/// <summary>
/// Cliente QIDO-RS focado em "este AccessionNumber já tem study no PACS?".
/// Usado pelo <c>SincronizadorExamesService</c> para promover solicitações
/// de Agendada → Realizada.
/// </summary>
public interface IConsultaStudyClient
{
    Task<bool> StudyExisteAsync(string accessionNumber, CancellationToken cancellationToken = default);
}
