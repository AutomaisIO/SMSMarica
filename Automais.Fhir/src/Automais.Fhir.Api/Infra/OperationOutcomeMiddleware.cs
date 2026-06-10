using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;

namespace Automais.Fhir.Api.Infra;

/// <summary>
/// Converte exceções em <c>OperationOutcome</c> FHIR + status HTTP, em vez de
/// ProblemDetails. É a borda de erro do serviço FHIR (equivalente ao
/// ExceptionHandlingMiddleware do smsmarica, mas falando FHIR).
/// </summary>
public sealed class OperationOutcomeMiddleware(RequestDelegate next, ILogger<OperationOutcomeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (FhirException ex)
        {
            await EscreverAsync(context, ex.StatusHttp, ex.IssueCode, ex.Message);
        }
        catch (DeserializationFailedException ex)
        {
            await EscreverAsync(context, StatusCodes.Status400BadRequest, "invalid",
                "JSON FHIR inválido ou não-conforme: " + ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cliente desistiu (ex.: importação parada). Não é erro do serviço; nada a responder.
            logger.LogDebug("Requisição FHIR cancelada pelo cliente.");
        }
        catch (Exception ex) when (EhTransitorio(ex))
        {
            // Saturação de conexão / indisponibilidade temporária do banco. 503 = "retentável":
            // o cliente reenvia com backoff em vez de tratar como falha definitiva.
            logger.LogWarning(ex, "Saturação/transitório no serviço FHIR — devolvendo 503 (retryable)");
            await EscreverAsync(context, StatusCodes.Status503ServiceUnavailable, "transient",
                "Serviço FHIR temporariamente indisponível (saturação de conexão). Tente novamente.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado no serviço FHIR");
            await EscreverAsync(context, StatusCodes.Status500InternalServerError, "exception",
                "Erro interno no serviço FHIR.");
        }
    }

    /// <summary>
    /// Erro transitório de conexão/banco (deve virar 503, não 500): timeout/cancelamento
    /// interno ao abrir conexão, esgotamento de slots do Postgres (53300/53400), falhas de
    /// conexão (classe 08) e os transitórios que o próprio Npgsql sinaliza.
    /// </summary>
    private static bool EhTransitorio(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case OperationCanceledException:
                case TimeoutException:
                    return true;
                case Npgsql.PostgresException pg when pg.SqlState is "53300" or "53400"
                    || pg.SqlState.StartsWith("08", StringComparison.Ordinal):
                    return true;
                case Npgsql.NpgsqlException npg when npg.IsTransient:
                    return true;
            }
        }
        return false;
    }

    private static async Task EscreverAsync(HttpContext context, int status, string code, string diagnostics)
    {
        if (context.Response.HasStarted) return; // resposta já começou — não dá pra reescrever o status

        var outcome = new OperationOutcome
        {
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = Enum.TryParse<OperationOutcome.IssueType>(code.Replace("-", ""), true, out var t)
                        ? t
                        : OperationOutcome.IssueType.Processing,
                    Diagnostics = diagnostics,
                },
            ],
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = FhirResponse.MediaType;
        await context.Response.WriteAsync(FhirJson.Serialize(outcome));
    }
}
