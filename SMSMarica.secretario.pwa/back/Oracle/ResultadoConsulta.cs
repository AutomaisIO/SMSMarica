namespace SMSMarica.Secretario.Api.Oracle;

/// <summary>Resultado tabular de uma consulta Oracle (mesmo shape do SMSMarica.server).</summary>
public sealed record ResultadoConsulta(
    bool Ok,
    IReadOnlyList<string> Colunas,
    IReadOnlyList<IReadOnlyList<object?>> Linhas,
    string? Erro = null)
{
    public static ResultadoConsulta ComErro(string erro) => new(false, [], [], erro);
}
