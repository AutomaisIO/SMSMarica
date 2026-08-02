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

    /// <summary>Internação (FIA): máximo de GREATEST(dt_baixa, dt_alta) importado — ADR-0025.</summary>
    public DateTime? UltimoSyncFiaEm { get; set; }

    /// <summary>CDC de eDoc: último ID_EDOC_MOVIMENTO_LOG processado (poll por PK — ADR-0024).</summary>
    public long? UltimoSyncEdocLogId { get; set; }

    /// <summary>
    /// Cursor de retomada do modo COMPLETO (escopo Tudo): <c>cd_paciente</c> do
    /// último bloco totalmente processado (paginação keyset, <c>cd_paciente DESC</c>).
    /// Gravado a cada bloco; permite retomar de onde parou após queda. Fica
    /// <c>null</c> quando a base foi importada inteira (próxima rodada começa do topo).
    /// </summary>
    public long? PacienteCursorCd { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
