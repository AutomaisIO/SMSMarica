using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Worklist;

/// <summary>
/// Cliente do endpoint UPS-RS do dcm4chee (Unified Procedure Step via REST).
/// Cria, consulta e cancela workitems que o equipamento puxa via C-FIND MWL.
/// </summary>
public interface IDcm4cheeUpsClient
{
    /// <summary>
    /// Cria um workitem agendado a partir de uma <see cref="SolicitacaoExame"/>
    /// (com Paciente e TipoExame carregados). Retorna o UID do workitem criado.
    /// </summary>
    Task<string> CriarWorkitemAsync(SolicitacaoExame solicitacao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela um workitem existente. Best-effort — se não existir mais no
    /// dcm4chee, retorna silenciosamente.
    /// </summary>
    Task CancelarWorkitemAsync(string workitemUid, string motivo, CancellationToken cancellationToken = default);
}
