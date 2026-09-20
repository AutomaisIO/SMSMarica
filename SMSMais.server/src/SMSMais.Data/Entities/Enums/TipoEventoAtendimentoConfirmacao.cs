namespace SMSMais.Data.Entities.Enums;

/// <summary>Trilha append-only do atendimento humano de confirmação (espelha <see cref="TipoEventoConversa"/>).</summary>
public enum TipoEventoAtendimentoConfirmacao
{
    Atendido = 1,
    Assumido = 2,
    Transferido = 3,
    Confirmado = 4,
    Cancelado = 5,
    EnviadoPendente = 6,
    ContatoErrado = 7,
    Retomado = 8,
    Liberado = 9,
    ContatoCorrigido = 10,

    /// <summary>
    /// O pedido de cancelamento era engano: a ficha volta para a fila de confirmação. Existe
    /// porque pedido chega em texto livre e texto livre erra — sem esta saída, um falso positivo
    /// tirava a pessoa da cobrança para sempre, calado.
    /// </summary>
    PedidoCancelamentoDesfeito = 11,
}
