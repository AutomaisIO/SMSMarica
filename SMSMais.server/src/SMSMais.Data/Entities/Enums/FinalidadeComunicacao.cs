namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Finalidade (assunto) de uma <see cref="Entities.ComunicacaoPaciente"/>. Cada finalidade tem
/// template, destino no app e gatilho próprios; o ciclo (fila→envio→entrega→leitura→
/// visualização→falha) é o mesmo para todas.
/// </summary>
public enum FinalidadeComunicacao
{
    /// <summary>Pedido de confirmação do agendamento (gatilho: import/criação com data futura).</summary>
    ConfirmacaoAgendamento = 1,

    /// <summary>Aviso de exame disponível (gatilho: solicitação marcada como Realizada).</summary>
    ExameLiberado = 2,

    /// <summary>Aviso de laudo disponível (gatilho: laudo ASSINADO digitalmente).</summary>
    LaudoPronto = 3,

    /// <summary>
    /// Lembrete X dias antes do agendamento ("a sua data está chegando"), com modelo diferente
    /// para quem JÁ confirmou e para quem ainda não respondeu. X é global (menu Confirmações).
    /// </summary>
    LembreteAgendamento = 4,

    /// <summary>
    /// Aviso de que o agendamento foi CANCELADO — pela unidade executante, pela solicitante ou
    /// pela regulação (gatilho: conciliação com a tela de marcações canceladas do SISREG).
    ///
    /// <para><b>O motivo nunca vai na mensagem.</b> A justificativa que o SISREG guarda é interna
    /// ("erro", "desligamento do profissional", "remanejado 12/11") e serve à trilha e à atendente,
    /// não ao paciente — nem por mensagem nem pela boca do robô.</para>
    ///
    /// <para>Quem tem contato verificado recebe o agendamento inteiro na mensagem; quem não tem
    /// recebe só "sua consulta foi cancelada", e o detalhe só depois de se identificar.</para>
    /// </summary>
    CancelamentoAgendamento = 5,
}
