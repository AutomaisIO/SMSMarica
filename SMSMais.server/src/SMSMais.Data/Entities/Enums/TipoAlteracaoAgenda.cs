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
}
