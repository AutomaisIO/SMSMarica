namespace SMSMais.Data.Entities.EsusSg;

/// <summary>O que disparou o gatilho. Números alinhados com SER/SERNIT onde o sentido é o mesmo.</summary>
public enum TipoGatilhoEsusSg
{
    /// <summary>A situação mudou entre duas varreduras (entrou em agendados, saiu da fila…).</summary>
    MudancaSituacao = 1,

    /// <summary>Pedido que não existia na base apareceu na varredura.</summary>
    NovaSolicitacao = 3,

    /// <summary>A data/hora do agendamento mudou sem mudar a situação (remarcação).</summary>
    MudancaAgendamento = 4,

    /// <summary>A prioridade do pedido na fila mudou (ex.: A REGULAR → URGENTE).</summary>
    MudancaPrioridade = 5,
}

/// <summary>
/// Fila de gatilhos da integração com o ESUS de São Gonçalo (ADR-0063; irmã de <c>sernit_gatilho</c>).
/// Registra o que muda no instante da varredura. Idempotência: único em
/// <c>(esussg_solicitacao_id, tipo, chave_evento)</c>.
/// </summary>
public class EsusSgGatilho
{
    public Guid Id { get; set; }

    public Guid EsusSgSolicitacaoId { get; set; }
    public EsusSgSolicitacao? EsusSgSolicitacao { get; set; }

    public string IdEsusSg { get; set; } = string.Empty;

    public TipoGatilhoEsusSg Tipo { get; set; }

    /// <summary>Discriminador da ocorrência dentro do tipo (situação de destino, data agendada…).</summary>
    public string ChaveEvento { get; set; } = string.Empty;

    public SituacaoEsusSg? SituacaoAnterior { get; set; }
    public SituacaoEsusSg? SituacaoAtual { get; set; }

    public string? PayloadJson { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? ProcessadoEm { get; set; }
    public string? ProcessadoPor { get; set; }
}
