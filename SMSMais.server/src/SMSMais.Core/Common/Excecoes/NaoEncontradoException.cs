namespace SMSMais.Core.Common.Excecoes;

/// <summary>
/// Lançada quando um recurso não existe no banco. Mapeada para HTTP 404 pelo middleware da API.
/// </summary>
public sealed class NaoEncontradoException(string recurso, object identificador)
    : Exception($"{recurso} com identificador '{identificador}' não foi encontrado.")
{
    public string Recurso { get; } = recurso;
    public object Identificador { get; } = identificador;
}
