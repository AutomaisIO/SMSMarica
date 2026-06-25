using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SMSMarica.Api.Auth;

/// <summary>
/// Marca uma ação do cidadão como acessível <b>sem consentimento</b> LGPD vigente
/// (ex.: ver/aceitar o termo, logout). Sem esta marca, <see cref="ExigeConsentimentoAttribute"/>
/// bloqueia a ação enquanto o aceite não existir.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PermiteSemConsentimentoAttribute : Attribute;

/// <summary>
/// Gate de consentimento LGPD para as ações do cidadão. Aplicado no controller; lê o claim
/// <c>consentido</c> (preenchido no <c>OnTokenValidated</c> a partir do banco) e bloqueia
/// (403 <c>consentimento_pendente</c>) toda ação não marcada com <see cref="PermiteSemConsentimentoAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ExigeConsentimentoAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isento = context.HttpContext.GetEndpoint()?.Metadata
            .GetMetadata<PermiteSemConsentimentoAttribute>() is not null;

        // Só vale para token de cidadão; staff/integração não é afetado.
        var ehCidadao = context.HttpContext.User.FindFirst("tipo")?.Value == "cidadao";

        if (!isento && ehCidadao &&
            context.HttpContext.User.FindFirst("consentido")?.Value != "true")
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Consentimento pendente.",
                Detail = "É necessário aceitar o termo de consentimento para usar o app.",
                Extensions = { ["code"] = "consentimento_pendente" },
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}
