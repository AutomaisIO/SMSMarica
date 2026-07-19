namespace Automais.Pabx.Api.Infra.Excecoes;

/// <summary>Recurso inexistente → 404 (ProblemDetails).</summary>
public sealed class NaoEncontradoException(string recurso, object identificador)
    : Exception($"{recurso} com identificador '{identificador}' não foi encontrado.")
{
    public string Recurso { get; } = recurso;
    public object Identificador { get; } = identificador;
}
