namespace SMSMais.Core.Common.Excecoes;

/// <summary>
/// Lançada quando uma operação viola um invariante de negócio (ex.: CPF duplicado,
/// estado inválido para a operação). Mapeada para HTTP 409 pelo middleware da API.
/// </summary>
public sealed class ConflitoException(string codigo, string mensagem)
    : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
}
