namespace SMSMarica.Data.Entities.Pep;

/// <summary>
/// Marca d'água (watermark) da sincronização incremental por base de PEP. Uma linha
/// por <c>FonteId</c> (uma <c>IaFonte</c>). Guarda, por FASE, a data do último registro
/// importado com sucesso; a próxima importação incremental só traz o que mudou depois
/// disso. Atualizado ao concluir cada fase (ADR-0024).
///
/// <para><b>Nomes de fase são NEUTROS de propósito</b> — profissional, paciente,
/// atendimento, documento, internação. Antes eram os do Salux (<c>Baa</c>, <c>Edoc</c>,
/// <c>Fia</c>); com a segunda base a caminho (Klinikos), a estratégia nova herdaria
/// vocabulário de um PEP que não é o dela. Os conceitos são universais num prontuário:
/// o Klinikos chama de <c>Pronto_Atendimento</c> o que o Salux chama de BAA.</para>
///
/// <para><b>As COLUNAS do banco ainda têm os nomes antigos</b> (<c>ultimo_sync_baa_em</c>,
/// <c>ultimo_sync_edoc_em</c>, <c>ultimo_sync_fia_em</c>) — o mapeamento explícito está na
/// configuration. Renomear coluna exigiria migration com janela de incompatibilidade entre
/// o binário antigo e o novo, por zero ganho funcional; fica como limpeza futura.</para>
/// </summary>
public class PepSincronizacaoEstado
{
    /// <summary>Base (IaFonte) à qual este estado pertence. Chave primária.</summary>
    public Guid FonteId { get; set; }

    public DateTime? UltimoSyncProfissionalEm { get; set; }
    public DateTime? UltimoSyncPacienteEm { get; set; }
    public DateTime? UltimoSyncAtendimentoEm { get; set; }
    public DateTime? UltimoSyncDocumentoEm { get; set; }

    /// <summary>Internação (FIA): máximo de GREATEST(dt_baixa, dt_alta) importado — ADR-0025.</summary>
    public DateTime? UltimoSyncInternacaoEm { get; set; }

    /// <summary>CDC de eDoc: último ID_EDOC_MOVIMENTO_LOG processado (poll por PK — ADR-0024).</summary>
    public long? UltimoSyncLogDocumentoId { get; set; }

    /// <summary>
    /// Cursor de retomada do modo COMPLETO (escopo Tudo): <c>cd_paciente</c> do
    /// último bloco totalmente processado (paginação keyset, <c>cd_paciente DESC</c>).
    /// Gravado a cada bloco; permite retomar de onde parou após queda. Fica
    /// <c>null</c> quando a base foi importada inteira (próxima rodada começa do topo).
    /// </summary>
    public long? PacienteCursorCd { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
