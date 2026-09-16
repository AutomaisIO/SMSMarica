using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Entities.Sernit;

/// <summary>
/// Um evento da trilha "Histórico da Solicitação" do SERNIT — subsistema irmão do SER-RJ (ADR-0042).
///
/// <para>Responde <b>quando</b> a solicitação foi cancelada, agendada, pendenciada, teve alta ou
/// recebeu tentativa de contato, com <b>quem</b> fez e de qual <b>IP</b>.</para>
///
/// <para><b>Idempotência do re-scraping:</b> índice único em
/// <c>(sernit_solicitacao_id, data_evento, evento)</c>. O SERNIT não numera os eventos, então a
/// identidade é a tripla. Reler o histórico inteiro todo dia (para pegar FollowUP) só insere o novo.</para>
/// </summary>
public class SernitEvento
{
    public Guid Id { get; set; }

    public Guid SernitSolicitacaoId { get; set; }
    public SernitSolicitacao? SernitSolicitacao { get; set; }

    /// <summary>Instante do evento (horário de Brasília, normalizado pela régua central de datas).</summary>
    public DateTime DataEvento { get; set; }

    /// <summary>Verbo do SERNIT: <c>Solicitar</c>, <c>FollowUP</c>, <c>Pendenciar</c>,
    /// <c>Cancelar</c>. Guardado como TEXTO CRU — verbo novo vira dado, não falha de importação.</summary>
    public string Evento { get; set; } = string.Empty;

    /// <summary>Verbo tipado — classificado UMA vez, na captura (ou pelo backfill). O texto cru
    /// continua em <see cref="Evento"/>.</summary>
    public TipoEventoExterno TipoEvento { get; set; }

    /// <summary>Categoria do FollowUP pelo classificador de regex da regulação (FalhaContato,
    /// SemVaga, ReclassificacaoRisco…). Só em <see cref="TipoEventoExterno.FollowUp"/>; null
    /// enquanto o worker não classificou.</summary>
    public string? FollowUpCategoria { get; set; }

    /// <summary>Hash das regras que produziram <see cref="FollowUpCategoria"/>. Regras editadas
    /// pela tela mudam o hash, e o worker reclassifica o que ficou diferente.</summary>
    public string? FollowUpRegrasHash { get; set; }

    public string? EstadoAnterior { get; set; }
    public string? EstadoAtual { get; set; }

    public string? CentralRegulacao { get; set; }
    public string? UnidadeExecutora { get; set; }

    /// <summary>Quem fez, como o SERNIT grava (nome livre, não é usuário do nosso sistema).</summary>
    public string? Usuario { get; set; }

    public string? LotacaoEvento { get; set; }

    /// <summary>IP registrado pelo SERNIT. Rastreabilidade real, de graça.</summary>
    public string? Ip { get; set; }

    /// <summary>Texto livre — conteúdo da tentativa de contato e motivo do cancelamento.</summary>
    public string? Observacao { get; set; }

    /// <summary>Quando ESTE evento foi capturado por nós (não é a data do evento).</summary>
    public DateTime CapturadoEm { get; set; }
}
