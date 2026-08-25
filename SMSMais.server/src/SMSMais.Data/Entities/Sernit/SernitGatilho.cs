namespace SMSMais.Data.Entities.Sernit;

/// <summary>O que disparou o gatilho.</summary>
public enum TipoGatilhoSernit
{
    /// <summary>A situação da solicitação mudou entre duas varreduras.</summary>
    MudancaSituacao = 1,

    /// <summary>Apareceu um evento <c>FollowUP</c> novo na trilha (não vem com mudança de situação —
    /// FollowUP é <c>Em fila -&gt; Em fila</c>).</summary>
    NovoFollowUp = 2,

    /// <summary>Solicitação que não existia na base entrou na varredura.</summary>
    NovaSolicitacao = 3,

    /// <summary>Mudou a coluna "Agendado para" sem mudar a situação (remarcação).</summary>
    MudancaAgendamento = 4,
}

/// <summary>
/// Fila de gatilhos da integração com o SERNIT — subsistema irmão do SER-RJ (ADR-0042 §7).
///
/// <para>Registra o que muda no instante da varredura (o dado que se perde se não capturado).
/// Idempotência: índice único em <c>(sernit_solicitacao_id, tipo, chave_evento)</c>.</para>
/// </summary>
public class SernitGatilho
{
    public Guid Id { get; set; }

    public Guid SernitSolicitacaoId { get; set; }
    public SernitSolicitacao? SernitSolicitacao { get; set; }

    /// <summary>Redundante com a FK, para o consumidor não precisar de join só para saber de qual
    /// solicitação do SERNIT se trata.</summary>
    public string IdSernit { get; set; } = string.Empty;

    public TipoGatilhoSernit Tipo { get; set; }

    /// <summary>Discriminador da ocorrência dentro do tipo (data do evento em ISO para FollowUP;
    /// situação de destino para mudança). Compõe o índice único.</summary>
    public string ChaveEvento { get; set; } = string.Empty;

    public SituacaoSernit? SituacaoAnterior { get; set; }
    public SituacaoSernit? SituacaoAtual { get; set; }

    /// <summary>Payload do que mudou, em JSON (formato livre — o consumidor ainda está sendo desenhado).</summary>
    public string? PayloadJson { get; set; }

    public DateTime CriadoEm { get; set; }

    // ---- Consumo ----

    public DateTime? ProcessadoEm { get; set; }
    public string? ProcessadoPor { get; set; }
}
