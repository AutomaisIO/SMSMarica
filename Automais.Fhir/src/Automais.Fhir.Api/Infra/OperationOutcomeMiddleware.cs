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
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado no serviço FHIR");
            await EscreverAsync(context, StatusCodes.Status500InternalServerError, "exception",
                "Erro interno no serviço FHIR.");
        }
    }

    private static async Task EscreverAsync(HttpContext context, int status, string code, string diagnostics)
    {
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
