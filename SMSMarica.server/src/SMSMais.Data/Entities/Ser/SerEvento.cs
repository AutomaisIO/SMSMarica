namespace SMSMais.Data.Entities.Ser;

/// <summary>
/// Um evento da trilha "Histórico da Solicitação" do SER — ADR-0042.
///
/// <para>É o dado mais valioso da integração: responde <b>quando</b> a solicitação foi
/// cancelada, agendada, pendenciada, teve alta ou recebeu tentativa de contato, com
/// <b>quem</b> fez e de qual <b>IP</b>. O SISREG não fornece nada equivalente.</para>
///
/// <para><b>Idempotência do re-scraping:</b> índice único em
/// <c>(ser_solicitacao_id, data_evento, evento)</c>. O SER não numera os eventos, então a
/// identidade é a tripla. Reler o histórico inteiro todo dia (necessário para pegar FollowUP)
/// só insere o que é novo.</para>
/// </summary>
public class SerEvento
{
    public Guid Id { get; set; }

    public Guid SerSolicitacaoId { get; set; }
    public SerSolicitacao? SerSolicitacao { get; set; }

    /// <summary>Instante do evento como o SER registra (horário de Brasília, já normalizado
    /// pela régua central de datas).</summary>
    public DateTime DataEvento { get; set; }

    /// <summary>Verbo do SER: <c>Solicitar</c>, <c>FollowUP</c>, <c>Pendenciar</c>,
    /// <c>Cancelar</c>. Guardado como TEXTO CRU de propósito — o SER pode introduzir um verbo
    /// novo, e um enum transformaria isso em falha de importação em vez de dado.</summary>
    public string Evento { get; set; } = string.Empty;

    /// <summary>Situação antes, em texto do SER ("Em fila"). Texto pelo mesmo motivo do verbo.</summary>
    public string? EstadoAnterior { get; set; }

    public string? EstadoAtual { get; set; }

    /// <summary>Central de regulação que registrou ("AMBULATÓRIO ESTADUAL",
    /// "Central Regulacao Estadual").</summary>
    public string? CentralRegulacao { get; set; }

    public string? UnidadeExecutora { get; set; }

    /// <summary>Quem fez, como o SER grava (nome livre, não é usuário do nosso sistema).</summary>
    public string? Usuario { get; set; }

    /// <summary>Lotação do usuário no evento ("Gestor: GESTOR SMS MARICA",
    /// "Operador da Central: AMBULATÓRIO ESTADUAL").</summary>
    public string? LotacaoEvento { get; set; }

    /// <summary>IP registrado pelo SER. Vem de graça e é rastreabilidade real.</summary>
    public string? Ip { get; set; }

    /// <summary>Texto livre — é aqui que mora o conteúdo da tentativa de contato
    /// ("SEM CONTATO: DIVERSAS TENTATIVAS...") e o motivo do cancelamento.</summary>
    public string? Observacao { get; set; }

    /// <summary>Quando ESTE evento foi capturado por nós (não é a data do evento).</summary>
    public DateTime CapturadoEm { get; set; }
}
