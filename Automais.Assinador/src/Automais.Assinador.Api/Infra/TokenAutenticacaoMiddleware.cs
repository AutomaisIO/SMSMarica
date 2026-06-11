using System.Security.Cryptography;
using System.Text;

namespace Automais.Assinador.Api.Infra;

/// <summary>
/// Fronteira de confiança simples: se um segredo estiver configurado
/// (<c>Assinador:Token</c> / env <c>Assinador__Token</c>), exige o header
/// <c>X-Assinador-Token</c> nos endpoints <c>/pades/*</c>. Sem segredo configurado,
/// não exige nada (dev/loopback). O serviço escuta em 127.0.0.1, mas isso impede
/// que qualquer processo local co-residente force a montagem de um PAdES.
/// </summary>
public sealed class TokenAutenticacaoMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string? _token = configuration["Assinador:Token"];

    public async Task InvokeAsync(HttpContext context)
    {
        // Só protege a superfície de assinatura; /health e /docs ficam livres.
        var protegido = context.Request.Path.StartsWithSegments("/pades");
        if (protegido && !string.IsNullOrWhiteSpace(_token))
        {
            var recebido = context.Request.Headers["X-Assinador-Token"].ToString();
            if (!TokensIguais(recebido, _token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { title = "nao_autorizado", detail = "Token do assinador ausente ou inválido." });
                return;
            }
        }

        await next(context);
    }

    /// <summary>Comparação em tempo constante para não vazar o token por timing.</summary>
    private static bool TokensIguais(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
