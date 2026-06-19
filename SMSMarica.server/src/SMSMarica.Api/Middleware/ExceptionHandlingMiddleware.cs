using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Api.Middleware;

public sealed partial class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;
    private readonly bool _detailedErrors =
        environment.IsDevelopment()
        || configuration.GetValue("DetailedErrors", defaultValue: false);

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
        // Corrida perdida: violação do índice único (ex.: 2ª assinatura concluída do
        // mesmo laudo) ou conflito otimista (xmin) viram 409 limpo, não 500.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            await EscreverProblemDetails(context, StatusCodes.Status409Conflict, "Conflito",
                "O recurso já foi processado por outra requisição concorrente.",
                type: "concorrencia.violacao_unica");
        }
        catch (DbUpdateConcurrencyException)
        {
            await EscreverProblemDetails(context, StatusCodes.Status409Conflict, "Conflito",
                "O recurso foi alterado por outra requisição concorrente. Tente novamente.",
                type: "concorrencia.token");
        }
        catch (UnauthorizedAccessException ex)
        {
            await EscreverProblemDetails(context, StatusCodes.Status403Forbidden, "Acesso negado", ex.Message);
        }
        catch (Exception ex)
        {
            LogErroNaoTratado(_logger, ex, context.Request.Path);
            await EscreverProblemDetails(
                context,
                StatusCodes.Status500InternalServerError,
                "Erro interno",
                "Ocorreu um erro inesperado.",
                excecao: _detailedErrors ? ex : null);
        }
    }

    private static Task EscreverProblemDetails(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string? type = null,
        Exception? excecao = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type,
            Instance = context.Request.Path,
        };

        if (excecao is not null)
        {
            problem.Extensions["exception"] = new
            {
                type = excecao.GetType().FullName,
                message = excecao.Message,
                inner = excecao.InnerException is null
                    ? null
                    : $"{excecao.InnerException.GetType().FullName}: {excecao.InnerException.Message}",
                stackTrace = excecao.StackTrace,
            };
        }

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
