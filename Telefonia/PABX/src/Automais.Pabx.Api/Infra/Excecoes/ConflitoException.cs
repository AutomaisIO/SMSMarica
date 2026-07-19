namespace Automais.Pabx.Api.Infra.Excecoes;

/// <summary>Conflito de estado (duplicidade, arquivo alheio, etc.) → 409 (ProblemDetails).</summary>
public sealed class ConflitoException(string codigo, string mensagem)
    : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
}
