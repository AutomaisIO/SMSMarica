using Automais.Pabx.Api.Infra.Excecoes;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Pabx.Api.Infra;

/// <summary>Mapeia exceções tipadas para ProblemDetails (espelha o padrão do SMSMais.Api).</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NaoEncontradoException ex)
        {
            await EscreverProblema(context, StatusCodes.Status404NotFound, "Recurso não encontrado", ex.Message);
        }
        catch (ConflitoException ex)
        {
            await EscreverProblema(context, StatusCodes.Status409Conflict, "Conflito", ex.Message, ex.Codigo);
        }
        catch (ValidacaoException ex)
        {
            var problema = new ValidationProblemDetails(ex.Erros)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Erro de validação",
            };
            context.Response.StatusCode = problema.Status.Value;
            await context.Response.WriteAsJsonAsync(problema);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado em {Metodo} {Caminho}", context.Request.Method, context.Request.Path);
            await EscreverProblema(context, StatusCodes.Status500InternalServerError,
                "Erro interno", "Ocorreu um erro inesperado no serviço PABX.");
        }
    }

    private static async Task EscreverProblema(HttpContext context, int status, string titulo, string detalhe, string? codigo = null)
    {
        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe };
        if (codigo is not null)
            problema.Extensions["codigo"] = codigo;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problema);
    }
}
