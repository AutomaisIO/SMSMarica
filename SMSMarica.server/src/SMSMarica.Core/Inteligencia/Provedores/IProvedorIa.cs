namespace SMSMarica.Core.Inteligencia.Provedores;

/// <summary>
/// Contexto enviado ao provedor de IA para gerar (ou corrigir) uma consulta SQL a partir de
/// uma pergunta em linguagem natural. O conhecimento recuperado (RAG) e os aprendizados ativos
/// já vêm montados pelo orquestrador.
/// </summary>
public sealed record GeracaoConsultaContexto(
    string Pergunta,
    string Dialeto,
    string ConhecimentoRecuperado,
    string AprendizadosAtivos,
    string? SqlAnterior = null,
    string? ErroAnterior = null);

/// <summary>
/// Saída estruturada do provedor: a consulta SQL + o plano de visualização. Quando a chamada é
/// uma correção, <see cref="InstrucaoAprendizado"/> traz a instrução a ser persistida como
/// aprendizado auto.
/// </summary>
public sealed record GeracaoConsultaResultado(
    string Sql,
    string Visualizacao,
    string Titulo,
    string? Resumo,
    string? EixoX,
    string? EixoY,
    string? InstrucaoAprendizado,
    int TokensEntrada,
    int TokensSaida);

/// <summary>
/// Provedor de IA (independente de marca; configurável). Implementação default fala com a
/// Anthropic Messages API usando saída estruturada e prompt caching.
/// </summary>
public interface IProvedorIa
{
    /// <summary>Gera SQL + plano de visualização para a pergunta (ou corrige, se houver erro anterior).</summary>
    Task<GeracaoConsultaResultado> GerarConsultaAsync(
        GeracaoConsultaContexto contexto, CancellationToken cancellationToken = default);

    /// <summary>Produz o resumo final em linguagem natural a partir do resultado já executado.</summary>
    Task<string> ResumirResultadoAsync(
        string pergunta, IReadOnlyList<string> colunas,
        IReadOnlyList<IReadOnlyList<object?>> linhas, CancellationToken cancellationToken = default);
}
