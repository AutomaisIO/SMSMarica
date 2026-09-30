using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Entities.EsusSg;

/// <summary>
/// Um marco na trilha de um pedido do ESUS de São Gonçalo (ADR-0063).
///
/// <para><b>Montado, não lido.</b> A conta de Maricá não vê histórico por pedido no ESUS. A trilha
/// sai dos marcos que as listas trazem com data e autor (inclusão na fila por
/// <c>usu_nome</c>/<c>fil_data</c>; agendamento por <c>usuarioAgendamento</c>) e das diferenças
/// entre duas varreduras (reagendamento, prioridade, pendência, saída da fila). Evento de
/// diferença leva a data em que a varredura <i>percebeu</i>, e diz isso na
/// <see cref="Observacao"/>.</para>
///
/// <para><b>Mesmos nomes de coluna do <c>sernit_evento</c></b> (<c>usuario</c>, <c>data_evento</c>,
/// <c>lotacao_evento</c>, <c>tipo_evento</c>) — as estatísticas de operadores leem por SQL com
/// prefixo de tabela. Idempotência: único em <c>(esussg_solicitacao_id, data_evento, evento)</c>.</para>
/// </summary>
public class EsusSgEvento
{
    public Guid Id { get; set; }

    public Guid EsusSgSolicitacaoId { get; set; }
    public EsusSgSolicitacao? EsusSgSolicitacao { get; set; }

    public DateTime DataEvento { get; set; }

    /// <summary>Rótulo do marco ("Inclusão na fila", "Agendamento", "Saiu da fila"…).</summary>
    public string Evento { get; set; } = string.Empty;

    public TipoEventoExterno TipoEvento { get; set; }

    /// <summary>Mantido para paridade de schema com SER/SERNIT; o ESUS não tem FollowUP.</summary>
    public string? FollowUpCategoria { get; set; }
    public string? FollowUpRegrasHash { get; set; }

    public string? EstadoAnterior { get; set; }
    public string? EstadoAtual { get; set; }

    public string? CentralRegulacao { get; set; }
    public string? UnidadeExecutora { get; set; }

    /// <summary>Quem fez, como o ESUS grava (nome livre).</summary>
    public string? Usuario { get; set; }

    /// <summary>Onde o autor está lotado: "MARICÁ" para quem incluiu, "SÃO GONÇALO" para quem agendou.</summary>
    public string? LotacaoEvento { get; set; }

    public string? Ip { get; set; }

    public string? Observacao { get; set; }

    public DateTime CapturadoEm { get; set; }
}
