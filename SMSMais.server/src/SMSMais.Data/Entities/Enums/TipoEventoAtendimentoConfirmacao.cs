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

    // ---- O que o cancelamento fez FORA daqui (21/09/2026) ----
    // "Cancelado" diz só que a vaga caiu no nosso banco. Cancelar no SISREG e avisar o paciente
    // são atos à parte, que podem sair ou não — e antes só o log sabia. Entram na trilha para a
    // aba Equipe contar quem de fato executa o cancelamento da vaga lá.

    /// <summary>O SISREG confirmou o cancelamento (ou já estava cancelado). A observação leva o
    /// resultado e a situação que a ficha passou a mostrar.</summary>
    CanceladoNoSisreg = 12,

    /// <summary>A atendente tentou cancelar e o SISREG recusou — nada mudou aqui. Só é gravado
    /// quando a ficha já estava com alguém (evento precisa de atendimento).</summary>
    SisregRecusouCancelamento = 13,

    /// <summary>O aviso de cancelamento saiu para o paciente na hora, pela mão da atendente.</summary>
    PacienteAvisadoCancelamento = 14,
}
