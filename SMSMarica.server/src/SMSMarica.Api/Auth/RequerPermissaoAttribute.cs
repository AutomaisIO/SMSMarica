using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Auth;

/// <summary>
/// Bloqueia a requisição com 403 se o usuário autenticado não tiver a ação
/// solicitada no módulo informado. As permissões são resolvidas via
/// <see cref="IIdentidadeService.ObterPermissoesResolvidasAsync"/>, que já une
/// herdadas (perfis) com overrides individuais.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequerPermissaoAttribute(ModuloPermissao modulo, AcoesPermissao acao)
    : Attribute, IAsyncAuthorizationFilter
{
    private readonly ModuloPermissao _modulo = modulo;
    private readonly AcoesPermissao _acao = acao;

    /// <summary>
    /// TTL do cache de permissões resolvidas por usuário. Curto de propósito: uma
    /// permissão revogada pode sobreviver até isso; em troca, rajadas (ex.: viewer
    /// PACS pedindo dezenas de frames de uma vez) fazem 1 query em vez de dezenas.
    /// </summary>
    private static readonly TimeSpan TtlPermissoes = TimeSpan.FromSeconds(30);

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Token de serviço (X-API-Key): acesso pleno à API — não tem perfil de
        // usuário, então não passa pela resolução de permissões por módulo.
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
        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        try
        {
            var resolvidas = (await cache.GetOrCreateAsync(
                $"permissoes-resolvidas:{usuarioId}",
                entrada =>
                {
                    entrada.AbsoluteExpirationRelativeToNow = TtlPermissoes;
                    return service.ObterPermissoesResolvidasAsync(
                        usuarioId,
                        context.HttpContext.RequestAborted);
                }))!;

            var entrada = resolvidas.Resolvidas.FirstOrDefault(p => p.Modulo == _modulo);
            if (entrada is null || (entrada.Acoes & _acao) != _acao)
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Permissão negada.",
                    Detail = $"Sem permissão de '{_acao}' no módulo '{_modulo}'.",
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
