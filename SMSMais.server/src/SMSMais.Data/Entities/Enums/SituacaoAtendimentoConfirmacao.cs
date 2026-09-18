namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Situação do atendimento HUMANO de uma confirmação de agendamento (menu Confirmações).
///
/// <para>Ativo (<c>encerrado_em IS NULL</c>): <see cref="EmAtendimento"/>, <see cref="Pendente"/> e
/// <see cref="ContatoErrado"/> — a solicitação está "com alguém" ou estacionada numa fila humana.
/// Encerrado: <see cref="Confirmado"/>, <see cref="Cancelado"/>, <see cref="Liberado"/>,
/// <see cref="ContatoCorrigido"/>.</para>
/// </summary>
public enum SituacaoAtendimentoConfirmacao
{
    /// <summary>Um atendente está tratando a solicitação agora (card esmaecido para os outros).</summary>
    EmAtendimento = 1,

    /// <summary>Desfecho: o atendente confirmou a presença pela mão (canal "atendente").</summary>
    Confirmado = 2,

    /// <summary>Desfecho: o atendente cancelou o agendamento (fase 1: só no SMSMais).</summary>
    Cancelado = 3,

    /// <summary>Estacionada com motivo ("não consegui contato", "vai confirmar depois"...).</summary>
    Pendente = 4,

    /// <summary>Estacionada: quem atendeu disse que não é o paciente (abre pendência de cadastro).</summary>
    ContatoErrado = 5,

    /// <summary>Encerrado sem desfecho: o atendente devolveu a solicitação à fila.</summary>
    Liberado = 6,

    /// <summary>Encerrado: o contato foi corrigido e a comunicação automática foi rearmada.</summary>
    ContatoCorrigido = 7,
}
