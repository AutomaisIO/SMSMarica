namespace SMSMarica.Data.Entities.Pep;

/// <summary>
/// Marca d'água (watermark) da sincronização incremental por base de PEP. Uma linha
/// por <c>FonteId</c> (uma <c>IaFonte</c>). Guarda, por entidade, a data do último
/// registro importado com sucesso; a próxima importação incremental só traz o que
/// mudou depois disso. Atualizado apenas ao concluir uma execução com sucesso.
/// </summary>
public class PepSincronizacaoEstado
{
    /// <summary>Base (IaFonte) à qual este estado pertence. Chave primária.</summary>
    public Guid FonteId { get; set; }

    public DateTime? UltimoSyncMedicoEm { get; set; }
    public DateTime? UltimoSyncPacienteEm { get; set; }
    public DateTime? UltimoSyncBaaEm { get; set; }
    public DateTime? UltimoSyncEdocEm { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
