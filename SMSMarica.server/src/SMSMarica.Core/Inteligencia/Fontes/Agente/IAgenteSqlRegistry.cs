using SMSMarica.Core.Inteligencia.Fontes;

namespace SMSMarica.Core.Inteligencia.Fontes.Agente;

/// <summary>
/// Registro em memória dos agentes proxy conectados por WSS. É singleton — o servidor roda em
/// processo único (systemd), então a lista de conexões vive na memória.
///
/// A camada de transporte (o WebSocket em si) fica na Api; este registro não conhece WebSocket.
/// A Api registra a conexão passando uma função de <c>enviar</c> (escreve um frame de texto no
/// socket) e alimenta as respostas via <see cref="EntregarResposta"/>. Ver ADR-0023.
/// </summary>
public interface IAgenteSqlRegistry
{
    /// <summary>
    /// Registra um agente recém-conectado. <paramref name="enviar"/> escreve um frame de texto no
    /// socket (a Api serializa os envios). Devolve um handle que, ao ser descartado, remove o
    /// agente e falha as consultas pendentes. Se já havia uma conexão para esse agente, ela é
    /// substituída (última conexão vence).
    /// </summary>
    IDisposable Registrar(string agenteId, Func<string, CancellationToken, Task> enviar);

    /// <summary>Entrega ao registro um frame recebido do agente (uma resposta de consulta).</summary>
    void EntregarResposta(string agenteId, string json);

    bool EstaConectado(string agenteId);

    /// <summary>
    /// Envia o SQL ao agente e aguarda o resultado (com timeout e teto de linhas). Devolve
    /// <see cref="ResultadoConsulta.ComErro"/> se o agente estiver offline, estourar o timeout,
    /// ou o agente reportar erro.
    /// </summary>
    Task<ResultadoConsulta> ExecutarAsync(
        string agenteId, string sql, int timeoutSegundos, int maxLinhas, CancellationToken ct = default);
}
