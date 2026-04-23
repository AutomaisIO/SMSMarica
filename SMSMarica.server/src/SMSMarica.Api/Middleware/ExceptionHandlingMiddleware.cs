using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Api.Middleware;

public sealed partial class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NaoEncontradoException ex)
        {
            await EscreverProblemDetails(context, StatusCodes.Status404NotFound, "Não encontrado", ex.Message);
        }
        catch (ConflitoException ex)
        {
            await EscreverProblemDetails(context, StatusCodes.Status409Conflict, "Conflito", ex.Message, type: ex.Codigo);
        }
        catch (ValidacaoException ex)
        {
            await EscreverValidationProblem(context, ex.Erros);
        }
        catch (Exception ex)
        {
            LogErroNaoTratado(_logger, ex, context.Request.Path);
            await EscreverProblemDetails(
                context,
                StatusCodes.Status500InternalServerError,
                "Erro interno",
                "Ocorreu um erro inesperado.");
        }
    }

    private static Task EscreverProblemDetails(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string? type = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type,
            Instance = context.Request.Path,
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    private static Task EscreverValidationProblem(HttpContext context, IDictionary<string, string[]> erros)
    {
        var problem = new ValidationProblemDetails(erros)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validação inválida",
            Instance = context.Request.Path,
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado em {Path}")]
    private static partial void LogErroNaoTratado(ILogger logger, Exception ex, string path);
}
