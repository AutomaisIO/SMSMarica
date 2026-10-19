using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Tokens;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Controllers;

/// <summary>
/// Saúde da conta para o sistema do cliente: o <c>health_status</c> da Meta de cada número que o
/// token alcança. É o que deixa a instância saber que a conta está BLOQUEADA (pagamento, política)
/// sem ter de queimar uma mensagem de paciente para descobrir — e saber quando voltou.
///
/// <para>Criado em 19/10/2026: a conta ficou sem pagamento por duas horas e a instância de Maricá só
/// descobriu pelas recusas (131042), uma a uma.</para>
/// </summary>
[ApiController]
[Route("v1/conta")]
[EnableRateLimiting("api-publica")]
public sealed class ContaApiController(
    ZapDbContext db,
    ITokenService tokens,
    IGraphMetaClient graph) : ControllerBase
{
    [HttpGet("saude")]
    public async Task<IActionResult> Saude(CancellationToken ct)
    {
        var chamador = await tokens.AutenticarAsync(Request.Headers.Authorization.ToString(), ct);
        if (chamador is null) return Unauthorized(new { erro = "Token inválido, revogado ou tenant suspenso." });

        var numeros = chamador.TodosNumeros
            ? await db.Numeros.AsNoTracking()
                .Where(n => n.Ativo && n.Waba!.TenantId == chamador.TenantId)
                .Select(n => n.PhoneNumberId)
                .ToListAsync(ct)
            : await db.TenantTokenNumeros.AsNoTracking()
                .Where(x => x.TokenId == chamador.TokenId && x.Numero!.Ativo)
                .Select(x => x.Numero!.PhoneNumberId)
                .ToListAsync(ct);

        var saida = new List<object>();
        foreach (var phoneNumberId in numeros.Distinct())
        {
            var r = await graph.ObterSaudeAsync(phoneNumberId, ct);
            if (!r.Sucesso)
            {
                // "Não sei" é diferente de "está bloqueado": sem veredito, o cliente não muda nada.
                saida.Add(new { phone_number_id = phoneNumberId, pode_enviar = (string?)null, erro = r.Erro });
                continue;
            }

            var s = r.Valor!;
            saida.Add(new
            {
                phone_number_id = s.PhoneNumberId,
                pode_enviar = s.PodeEnviar,
                problemas = s.Entidades.Select(e => new
                {
                    entidade = e.Entidade,
                    id = e.Id,
                    pode_enviar = e.PodeEnviar,
                    codigo = e.Codigo,
                    descricao = e.Descricao,
                    solucao = e.Solucao,
                }),
            });
        }

        return Ok(new { numeros = saida, consultado_em = DateTimeOffset.UtcNow });
    }
}
