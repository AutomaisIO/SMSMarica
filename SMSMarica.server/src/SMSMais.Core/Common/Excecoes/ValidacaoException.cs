namespace SMSMais.Core.Common.Excecoes;

/// <summary>
/// Lançada quando entradas falham em validação que não foi capturada pelo pipeline FluentValidation
/// (ex.: regra que depende do estado do banco). Mapeada para HTTP 400 pelo middleware.
/// </summary>
public sealed class ValidacaoException : Exception
{
    public ValidacaoException(string campo, string mensagem)
        : base(mensagem)
    {
        Erros = new Dictionary<string, string[]> { [campo] = [mensagem] };
    }

    public ValidacaoException(IDictionary<string, string[]> erros)
        : base("Validação falhou.")
    {
        Erros = erros;
    }

    public IDictionary<string, string[]> Erros { get; }
}
