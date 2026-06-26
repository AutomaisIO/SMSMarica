namespace SMSMarica.Core.Integracoes.Proxy;

/// <summary>Serviços que admitem motores de proxy com fallback.</summary>
public static class ServicosProxy
{
    public const string Cpf = "cpf";
    public const string Cep = "cep";

    public static readonly IReadOnlyList<string> Todos = [Cpf, Cep];

    public static bool EhValido(string? servico) =>
        servico is not null && Todos.Contains(servico, StringComparer.OrdinalIgnoreCase);

    public static string Rotulo(string servico) => servico switch
    {
        Cpf => "Proxy CPF",
        Cep => "Proxy CEP",
        _ => servico,
    };
}

/// <summary>
/// Catálogo dos motores suportados por serviço (chave estável + rótulo). Adicionar um motor
/// novo = registrar aqui + criar a classe <c>IMotorCpf</c>/<c>IMotorCep</c> correspondente.
/// </summary>
public static class MotoresProxy
{
    public const string HubDoDesenvolvedor = "hubdodesenvolvedor";

    public static string Rotulo(string motor) => motor switch
    {
        HubDoDesenvolvedor => "Hub do Desenvolvedor",
        _ => motor,
    };

    /// <summary>Indica se o motor exige token/chave de API para funcionar.</summary>
    public static bool ExigeToken(string motor) => motor switch
    {
        HubDoDesenvolvedor => true,
        _ => false,
    };

    private static readonly IReadOnlyList<string> SuportadosCpf = [HubDoDesenvolvedor];
    private static readonly IReadOnlyList<string> SuportadosCep = [HubDoDesenvolvedor];

    public static IReadOnlyList<string> Suportados(string servico) => servico switch
    {
        ServicosProxy.Cpf => SuportadosCpf,
        ServicosProxy.Cep => SuportadosCep,
        _ => [],
    };

    public static bool EhSuportado(string servico, string motor) => Suportados(servico).Contains(motor);
}

/// <summary>Parâmetros de execução de uma chamada a um motor (já com token revelado).</summary>
public sealed record MotorExecucao(
    string? Token,
    int TimeoutSegundos,
    int Tentativas,
    string? ParametrosJson);

/// <summary>
/// Falha transitória de um motor (timeout, indisponível, resposta inválida): o orquestrador
/// retenta neste motor e, esgotadas as tentativas, cai para o próximo.
/// </summary>
public sealed class MotorIndisponivelException(string motor, string? detalhe = null, Exception? innerException = null)
    : Exception($"Motor '{motor}' indisponível{(detalhe is null ? "." : $": {detalhe}")}", innerException)
{
    public string Motor { get; } = motor;
}

/// <summary>
/// Resposta autoritativa de "não encontrado/negado" de um motor (ex.: a Receita não validou o
/// CPF). Não retenta o mesmo motor, mas tenta o próximo — se todos negarem, vira 422.
/// </summary>
public sealed class MotorNaoEncontrouException(string motor, string mensagemUsuario) : Exception(mensagemUsuario)
{
    public string Motor { get; } = motor;
}
