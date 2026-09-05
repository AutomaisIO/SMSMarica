namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// O que mudou numa solicitação já importada, entre duas leituras do SISREG. Valor persistido —
/// não renumerar.
/// </summary>
public enum TipoAlteracaoAgenda
{
    /// <summary>Remarcação. É a que muda a vida do paciente: ele tem na mão um dia que não vale mais.</summary>
    DataHora = 1,

    /// <summary>Trocou o profissional que vai atender.</summary>
    Executante = 2,

    /// <summary>Trocou o procedimento (código ou nome).</summary>
    Procedimento = 3,

    /// <summary>
    /// Estava marcado aqui e NÃO veio no arquivo do SISREG — provável cancelamento lá.
    ///
    /// <para>Só é detectável depois que a varredura passou a cobrir <b>todo</b> o futuro da unidade:
    /// com janela curta, um agendamento remarcado para longe sumia e pareceria cancelado.</para>
    /// </summary>
    Ausente = 4,
}
