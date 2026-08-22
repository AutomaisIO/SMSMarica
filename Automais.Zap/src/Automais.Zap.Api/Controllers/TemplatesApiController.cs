using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Tokens;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Controllers;

/// <summary>
/// Templates aprovados, para o sistema do cliente montar a tela de envio sem precisar de
/// credencial da Meta.
///
/// É o que permite a instância largar o token do System User de vez: sem isto, ela ainda
/// dependeria do App antigo só para listar modelos, e o ADR-0044 quer esse token num lugar só.
/// </summary>
[ApiController]
[Route("v1/templates")]
[EnableRateLimiting("api-publica")]
public sealed class TemplatesApiController(
    ZapDbContext db,
    ITokenService tokens,
    IGraphMetaClient graph,
    ILogger<TemplatesApiController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var chamador = await tokens.AutenticarAsync(Request.Headers.Authorization.ToString(), ct);
        if (chamador is null) return Unauthorized(new { erro = "Token inválido, revogado ou tenant suspenso." });

        // O token alcança WABAs por via dos números que ele pode usar — não faz sentido
        // enxergar o catálogo de uma linha pela qual ele não pode enviar.
        var wabas = chamador.TodosNumeros
            ? await db.Wabas.AsNoTracking()
                .Where(w => w.TenantId == chamador.TenantId)
                .Select(w => new { w.WabaId, w.Nome })
                .ToListAsync(ct)
            : await db.TenantTokenNumeros.AsNoTracking()
                .Where(x => x.TokenId == chamador.TokenId)
                .Select(x => new { x.Numero!.Waba!.WabaId, x.Numero.Waba.Nome })
                .Distinct()
                .ToListAsync(ct);

        var saida = new List<object>();
        foreach (var w in wabas)
        {
            var r = await graph.ListarTemplatesAsync(w.WabaId, ct);
            if (!r.Sucesso)
            {
                logger.LogWarning("Não consegui listar templates do WABA {Waba}: {Erro}", w.WabaId, r.Erro);
                continue;
            }

            foreach (var t in r.Valor!.Where(t => t.Status == "APPROVED"))
            {
                saida.Add(new
                {
                    nome = t.Nome,
                    idioma = t.Idioma,
                    categoria = t.Categoria,
                    corpo = t.Corpo,
                    parametros = t.Parametros,
                    exemplos = t.Exemplos,
                    waba_id = w.WabaId,
                });
            }
        }

        return Ok(new { templates = saida });
    }
}
