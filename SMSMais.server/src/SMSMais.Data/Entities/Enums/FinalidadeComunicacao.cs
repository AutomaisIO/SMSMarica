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

    /// <summary>
    /// Reforço para quem recebeu a PRIMEIRA mensagem da confirmação (a principal, finalidade
    /// <see cref="ConfirmacaoAgendamento"/> retida em
    /// <see cref="StatusComunicacao.AguardandoVerificacaoCadastral"/>) e não se identificou — o
    /// "toque 2" da régua de 24/09/2026. Dois modelos, escolhidos NO ENVIO: quem não leu recebe o
    /// "aviso pendente" (quando aprovado na Meta); os demais, o "aguardando resposta".
    ///
    /// <para>Como a principal, não revela nada do agendamento: nem procedimento, nem data, nem
    /// unidade — o número ainda não foi provado (ADR-0057). O que muda de um paciente para outro
    /// é só o tratamento e se é exame ou consulta.</para>
    ///
    /// <para>Quem enfileira é a régua (<c>ReforcoConfirmacaoService</c>), não um gatilho de
    /// domínio; o envio passa pela mesma fila, janela e vazão das outras.</para>
    /// </summary>
    ReforcoConfirmacao = 6,

    /// <summary>
    /// Orientação ao posto — o "toque 3", TERMINAL da régua: "não vamos mais insistir por
    /// mensagem; a guia está no posto onde o paciente tem cadastro". Depois dela nenhum automático
    /// sobre esse agendamento sai para o número (nem o lembrete).
    ///
    /// <para>Existe porque insistir sem fim com quem não responde custa a nota da conta na Meta e
    /// não leva ninguém ao exame; dizer onde está a guia leva.</para>
    /// </summary>
    OrientacaoPosto = 7,
}
