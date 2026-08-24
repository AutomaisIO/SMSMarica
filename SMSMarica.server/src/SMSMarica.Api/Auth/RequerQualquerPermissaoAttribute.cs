using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Auth;

/// <summary>
/// Como <see cref="RequerPermissaoAttribute"/>, mas passa se o usuário tiver a
/// ação em <b>qualquer um</b> dos módulos informados (OR). Útil para recursos
/// que são reutilizados por mais de um contexto — ex.: ler templates de laudo é
/// permitido tanto para quem gere templates (<c>LaudosTemplates</c>) quanto para
/// quem emite laudos (<c>Laudos</c>) e precisa apenas escolhê-los, sem que isso
/// exponha o menu de gestão de templates.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequerQualquerPermissaoAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly AcoesPermissao _acao;
    private readonly ModuloPermissao[] _modulos;

    public RequerQualquerPermissaoAttribute(AcoesPermissao acao, params ModuloPermissao[] modulos)
    {
        _acao = acao;
        _modulos = modulos;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Token de serviço (X-API-Key): acesso pleno à API.
        if (user.FindFirstValue(ApiKeyAuthenticationHandler.ClaimTokenType)
            == ApiKeyAuthenticationHandler.ValorServico)
        {
            return;
        }

        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var usuarioId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var service = context.HttpContext.RequestServices.GetRequiredService<IIdentidadeService>();
        try
        {
            var resolvidas = await service.ObterPermissoesResolvidasAsync(
                usuarioId,
                context.HttpContext.RequestAborted);

            var permitido = _modulos.Any(m =>
                resolvidas.Resolvidas.FirstOrDefault(p => p.Modulo == m) is { } e
                && (e.Acoes & _acao) == _acao);

            if (!permitido)
            {
                var lista = string.Join("' ou '", _modulos);
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Permissão negada.",
                    Detail = $"Sem permissão de '{_acao}' em '{lista}'.",
                    Type = "permissao.negada",
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                    ContentTypes = { "application/problem+json" },
                };
            }
        }
        catch (NaoEncontradoException)
        {
            context.Result = new ForbidResult();
        }
    }
}
