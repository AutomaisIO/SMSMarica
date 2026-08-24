namespace SMSMais.Data.Entities.Ser;

/// <summary>O que disparou o gatilho.</summary>
public enum TipoGatilhoSer
{
    /// <summary>A situação da solicitação mudou entre duas varreduras
    /// (ex.: Em fila → Agendada, → Cancelada, → Alta).</summary>
    MudancaSituacao = 1,

    /// <summary>Apareceu um evento <c>FollowUP</c> novo na trilha. <b>Não</b> vem acompanhado de
    /// mudança de situação — FollowUP é <c>Em fila -&gt; Em fila</c>.</summary>
    NovoFollowUp = 2,

    /// <summary>Solicitação que não existia na base entrou na varredura.</summary>
    NovaSolicitacao = 3,

    /// <summary>Mudou a coluna "Agendado para" sem mudar a situação (remarcação).</summary>
    MudancaAgendamento = 4,
}

/// <summary>
/// Fila de gatilhos da integração com o SER — ADR-0042 §7.
///
/// <para><b>Nasce sem consumidor, de propósito.</b> Nenhum processo lê esta tabela ainda; ela
/// existe porque o dado que dispara o gatilho é exatamente o que se perde se não for capturado
/// no instante da varredura. Quando a regra de negócio for definida (avisar o cidadão, abrir
/// TFD, alertar a regulação), o consumidor lê daqui em vez de precisar re-varrer o passado.</para>
///
/// <para>Idempotência: índice único em <c>(ser_solicitacao_id, tipo, chave_evento)</c>.
/// <see cref="ChaveEvento"/> é o que distingue duas ocorrências do mesmo tipo — a data do evento
/// para FollowUP, a situação de destino para mudança de situação.</para>
/// </summary>
public class SerGatilho
{
    public Guid Id { get; set; }

    public Guid SerSolicitacaoId { get; set; }
    public SerSolicitacao? SerSolicitacao { get; set; }

    /// <summary>Redundante com a FK, mas guardado para o consumidor futuro não precisar de join
    /// só para saber de qual solicitação do SER se trata.</summary>
    public string IdSer { get; set; } = string.Empty;

    public TipoGatilhoSer Tipo { get; set; }

    /// <summary>Discriminador da ocorrência dentro do tipo (data do evento em ISO para
    /// FollowUP; situação de destino para mudança). Compõe o índice único.</summary>
    public string ChaveEvento { get; set; } = string.Empty;

    /// <summary>Situação antes / depois — preenchidas em <see cref="TipoGatilhoSer.MudancaSituacao"/>.</summary>
    public SituacaoSer? SituacaoAnterior { get; set; }
    public SituacaoSer? SituacaoAtual { get; set; }

    /// <summary>Payload do que mudou, em JSON. Formato deliberadamente livre: o consumidor ainda
    /// não existe, e fixar colunas agora seria adivinhar o que ele vai precisar.</summary>
    public string? PayloadJson { get; set; }

    public DateTime CriadoEm { get; set; }

    // ---- Consumo (ainda ninguém consome) ----

    /// <summary>Quando foi processado. Null = na fila. O consumidor futuro carimba aqui.</summary>
    public DateTime? ProcessadoEm { get; set; }

    /// <summary>Quem processou (nome do processo/consumidor), para rastrear quando houver mais
    /// de um interessado no mesmo gatilho.</summary>
    public string? ProcessadoPor { get; set; }
}
