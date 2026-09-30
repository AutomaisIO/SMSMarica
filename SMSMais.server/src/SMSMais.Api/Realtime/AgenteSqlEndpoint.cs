using System.Net.WebSockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Inteligencia.Fontes.Agente;
using SMSMais.Data;

namespace SMSMais.Api.Realtime;

/// <summary>
/// Endpoint WSS onde os agentes proxy de SQL se conectam (reverso: o agente disca para cá).
///
/// Autenticação: o agente vem em <c>?agente=&lt;slug&gt;&amp;token=&lt;token&gt;</c>. Validamos o
/// token contra o hash guardado na fonte (<c>ia_fonte.agente_token_hash</c>). NÃO usa o JWT de
/// usuário — é máquina-a-máquina. As credenciais do banco NÃO trafegam aqui: ficam no .env do
/// destino. Ver ADR-0023.
///
/// Depois de aceito, este handler é o laço de recepção da conexão: fica vivo enquanto o socket
/// existir, lendo frames de resposta do agente e entregando ao <see cref="IAgenteSqlRegistry"/>.
/// Os pedidos vão no sentido contrário (servidor → agente) via a função de envio registrada.
/// </summary>
public static class AgenteSqlEndpoint
{
    public static void MapAgenteSql(this WebApplication app)
    {
        app.MapGet("/agentes/sql", HandleAsync).AllowAnonymous();
    }

    private static async Task HandleAsync(
        HttpContext ctx, IAgenteSqlRegistry registry, SmsMaisDbContext db, ILoggerFactory logs,
        IHostApplicationLifetime ciclo)
    {
        var log = logs.CreateLogger("AgenteSql");

        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("Este endpoint aceita apenas conexões WebSocket.");
            return;
        }

        var agente = ctx.Request.Query["agente"].ToString();
        var token = ctx.Request.Query["token"].ToString();

        if (string.IsNullOrWhiteSpace(agente) || string.IsNullOrWhiteSpace(token))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var fonte = await db.IaFontes
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Slug == agente && f.ViaAgente && f.Ativo && f.ExcluidoEm == null);

        if (fonte is null || !TokenAgente.Confere(token, fonte.AgenteTokenHash))
        {
            log.LogWarning("Agente '{Agente}' recusado (token inválido ou fonte inexistente).", agente);
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        log.LogInformation("Agente '{Agente}' conectado.", agente);

        // Envio serializado: WebSocket.SendAsync não pode ter dois envios concorrentes.
        var envioLock = new SemaphoreSlim(1, 1);
        async Task Enviar(string msg, CancellationToken c)
        {
            await envioLock.WaitAsync(c);
            try
            {
                await ws.SendAsync(Encoding.UTF8.GetBytes(msg), WebSocketMessageType.Text, true, c);
            }
            finally
            {
                envioLock.Release();
            }
        }

        using var registro = registry.Registrar(agente, Enviar);

        // Parar o serviço também encerra o laço. Só com o RequestAborted, o Kestrel esperava a
        // conexão até o teto de desligamento (30s): medido em 30/09/2026, os agentes caíam sempre
        // no 30º segundo do stop, e esse era o maior pedaço dos ~45s de API fora a cada deploy.
        using var fim = CancellationTokenSource.CreateLinkedTokenSource(
            ctx.RequestAborted, ciclo.ApplicationStopping);

        var buffer = new byte[64 * 1024];
        var acumulado = new List<byte>();
        try
        {
            while (ws.State == WebSocketState.Open && !fim.IsCancellationRequested)
            {
                WebSocketReceiveResult recebido;
                try
                {
                    recebido = await ws.ReceiveAsync(buffer, fim.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    break;
                }

                if (recebido.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                acumulado.AddRange(buffer.AsSpan(0, recebido.Count).ToArray());
                if (!recebido.EndOfMessage)
                {
                    continue;
                }

                var texto = Encoding.UTF8.GetString(acumulado.ToArray());
                acumulado.Clear();

                // Frames de controle (ping) só mantêm a conexão viva; resposta vai ao registro.
                if (!texto.Contains("\"ping\"", StringComparison.Ordinal))
                {
                    registry.EntregarResposta(agente, texto);
                }
            }
        }
        finally
        {
            // Na parada, fecha com 1001 (going away) em vez de só largar o socket: o agente lê o
            // fechamento limpo e redisca assim que o serviço novo estiver no ar.
            if (ciclo.ApplicationStopping.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                try
                {
                    using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await ws.CloseOutputAsync(
                        WebSocketCloseStatus.EndpointUnavailable, "servidor reiniciando", prazo.Token);
                }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
                {
                    // Melhor esforço: o socket cai de qualquer jeito quando o processo sair.
                }
            }

            log.LogInformation("Agente '{Agente}' desconectado.", agente);
        }
    }
}
