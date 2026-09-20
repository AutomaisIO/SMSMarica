namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Por que o canal não alcança o contato deste cadastro. É o "porquê" que a atendente lê na aba
/// Telefone comprometido de Confirmações — cada valor pede uma ação diferente da recepção.
///
/// <para>Não confundir com <see cref="TipoPendenciaCadastro.NumeroErrado"/>: lá quem atendeu
/// <b>negou ser o paciente</b> (questão de LGPD, o número é de outra pessoa); aqui o número pode
/// muito bem ser do paciente — o que falta é o canal chegar nele.</para>
///
/// Valor inteiro estável (persistido) — não renumerar.
/// </summary>
public enum MotivoContatoComprometido
{
    /// <summary>
    /// O cadastro não tem celular nenhum — ou o que tem não é um celular brasileiro válido.
    /// Ação: pegar o número na próxima passagem pela unidade.
    /// </summary>
    SemCelular = 1,

    /// <summary>
    /// A Meta recusou a entrega dizendo que o número não está no WhatsApp (erro 131026) ou que o
    /// destinatário não pode receber (131030). O número pode existir e atender ligação — só não
    /// tem WhatsApp. Ação: ligar, e perguntar se há outro número com WhatsApp.
    /// </summary>
    NaoEhWhatsApp = 2,
}
