namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Finalidade de uma agenda, que define o tipo de recurso agendado. Valor inteiro
/// estável (persistido). Ver ADR-0013.
/// </summary>
public enum FinalidadeAgenda
{
    /// <summary>Consulta — agenda de uma especialidade (pool, qualquer médico) ou de um médico específico (retorno).</summary>
    Consulta = 1,

    /// <summary>Exame — agenda de um equipamento (imagem).</summary>
    Exame = 2,
}
