using Microsoft.AspNetCore.Mvc;

namespace Automais.Assinador.Api.Infra;

/// <summary>
/// Converte exceções em <see cref="ProblemDetails"/> JSON. Falhas de assinatura
/// (PDF inválido, cadeia ruim, assinatura crua incompatível) viram 422; o resto, 500.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (FormatException ex)
        {
            await EscreverAsync(context, StatusCodes.Status400BadRequest, "entrada_invalida", ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Assinatura rejeitada ({Tipo}).", ex.GetType().Name);
            await EscreverAsync(context, StatusCodes.Status422UnprocessableEntity, "assinatura_invalida", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado no assinador ({Tipo}).", ex.GetType().Name);
            await EscreverAsync(context, StatusCodes.Status500InternalServerError, "erro_interno",
                "Falha inesperada ao assinar.");
        }
    }

    private static async Task EscreverAsync(HttpContext context, int status, string code, string detalhe)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detalhe,
        };
        await context.Response.WriteAsJsonAsync(problem);
    }
}
