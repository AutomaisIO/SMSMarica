namespace Automais.Pabx.Api.Infra.Excecoes;

/// <summary>Entrada inválida → 400 (ValidationProblemDetails).</summary>
public sealed class ValidacaoException : Exception
{
    public ValidacaoException(string campo, string mensagem)
        : base(mensagem)
    {
        Erros = new Dictionary<string, string[]> { [campo] = [mensagem] };
    }

    public ValidacaoException(IDictionary<string, string[]> erros)
        : base("Um ou mais erros de validação ocorreram.")
    {
        Erros = erros;
    }

    public IDictionary<string, string[]> Erros { get; }
}
