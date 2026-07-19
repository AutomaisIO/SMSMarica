using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Pabx.Api.Infra;

/// <summary>
/// Autenticação por API key (header X-Api-Key) para as rotas /api.
/// Página estática, /health, /docs e /openapi ficam anônimos — a página local
/// pede a key ao operador e a envia em cada chamada.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public const string Header = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var esperada = configuration["Pabx:ApiKey"];
        if (string.IsNullOrWhiteSpace(esperada))
        {
            await Rejeitar(context, StatusCodes.Status503ServiceUnavailable,
                "API key não configurada no serviço (Pabx:ApiKey).");
            return;
        }

        var recebida = context.Request.Headers[Header].ToString();
        if (string.IsNullOrEmpty(recebida) || !ComparaConstante(recebida, esperada))
        {
            await Rejeitar(context, StatusCodes.Status401Unauthorized, "API key ausente ou inválida.");
            return;
        }

        await next(context);
    }

    private static bool ComparaConstante(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static async Task Rejeitar(HttpContext context, int status, string detalhe)
    {
        var problema = new ProblemDetails { Status = status, Title = "Não autorizado", Detail = detalhe };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problema);
    }
}
