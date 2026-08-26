using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SMSMais.Core.RoboAtendimento.Comandos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Interno;

/// <summary>
/// Guichê interno do robô: o aiengine (kind atendimento) chama AQUI para executar um comando
/// habilitado por assunto. Mesma segurança em duas camadas do <see cref="ProxySqlEndpoint"/>:
/// porta dedicada de loopback + token. O robô NUNCA toca o banco livremente — só estes comandos
/// tipados, com gate/minimização no handler .NET e trilha em <c>robo_acao</c>.
/// </summary>
public static class RoboComandoEndpoint
{
    public static void MapRoboComando(this WebApplication app)
    {
        app.MapPost("/robo-comando", ExecutarAsync).AllowAnonymous();
    }

    private static async Task<IResult> ExecutarAsync(
        RoboComandoRequisicao requisicao,
        HttpContext ctx,
        IOptions<RoboComandoOpcoes> opcoes,
        IRoboComandoDispatcher dispatcher,
        ILoggerFactory logs,
        CancellationToken ct)
    {
        var cfg = opcoes.Value;
        var log = logs.CreateLogger("RoboComando");

        // 1. Porta interna (loopback). Fora dela o endpoint não existe.
        if (cfg.Porta <= 0 || ctx.Connection.LocalPort != cfg.Porta)
            return Results.NotFound();

        // 2. Token.
        if (string.IsNullOrWhiteSpace(cfg.Token))
        {
            log.LogWarning("Robo-comando chamado mas RoboComando:Token não configurado — recusado.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        if (!TokenConfere(ctx.Request.Headers["X-Robo-Token"].ToString(), cfg.Token))
            return Results.Unauthorized();

        // 3. Requisição.
        if (requisicao.ConversaId == Guid.Empty)
            return Results.BadRequest(new { mensagem = "Informe 'conversaId'." });
        if (!Enum.TryParse<ComandoRobo>(requisicao.Comando, ignoreCase: true, out var comando))
            return Results.BadRequest(new { mensagem = $"Comando '{requisicao.Comando}' desconhecido." });

        var resultado = await dispatcher.ExecutarAsync(
            requisicao.ConversaId, requisicao.PacienteId, requisicao.AssuntoId, comando, requisicao.Args, ct);

        return Results.Ok(new
        {
            sucesso = resultado.Sucesso,
            mensagem = resultado.Mensagem,
            dados = resultado.Dados,
        });
    }

    private static bool TokenConfere(string apresentado, string esperado)
    {
        if (string.IsNullOrEmpty(apresentado)) return false;
        var a = Encoding.UTF8.GetBytes(apresentado);
        var b = Encoding.UTF8.GetBytes(esperado);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

/// <summary>Configuração do guichê de comandos (<c>RoboComando</c>). Sem token/porta, fica desligado.</summary>
public sealed class RoboComandoOpcoes
{
    /// <summary>Porta interna (loopback) — normalmente a mesma do proxy SQL. 0 desliga.</summary>
    public int Porta { get; set; }

    public string Token { get; set; } = string.Empty;
}

public sealed record RoboComandoRequisicao(
    Guid ConversaId,
    Guid? PacienteId,
    Guid? AssuntoId,
    string Comando,
    JsonElement Args);
