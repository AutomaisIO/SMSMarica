namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Natureza de uma pendência de ajuste de cadastro levantada no atendimento (hoje pelo robô).
/// Valor inteiro estável (persistido) — não renumerar.
/// </summary>
public enum TipoPendenciaCadastro
{
    /// <summary>O número não pertence ao paciente do cadastro ("não sou essa pessoa").</summary>
    NumeroErrado = 1,
}
