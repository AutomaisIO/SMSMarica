using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Midias;
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
    ITemplateArteService artes,
    ILogger<TemplatesApiController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var chamador = await tokens.AutenticarAsync(Request.Headers.Authorization.ToString(), ct);
        if (chamador is null) return Unauthorized(new { erro = "Token inválido, revogado ou tenant suspenso." });

        // O token alcança WABAs por via dos números que ele pode usar — não faz sentido
        // enxergar o catálogo de uma linha pela qual ele não pode enviar.
        var wabas = await WabasDoChamadorAsync(chamador, ct);

        var saida = new List<object>();
        foreach (var w in wabas)
        {
            // A arte escolhida para cada modelo mora aqui: o catálogo entrega a URL pronta e a
            // instância do município não precisa manter mapa nenhum.
            var artesDoWaba = await artes.MapaAsync(w.Id, ct);

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
                    // Modelo com foto/vídeo no topo exige o componente de header em CADA envio.
                    // Sem este campo o cliente só descobre isso quando a Meta recusa (132012).
                    cabecalho = t.Cabecalho is null ? null : new
                    {
                        formato = t.Cabecalho.Formato,
                        texto = t.Cabecalho.Texto,
                        parametros = t.Cabecalho.Parametros,
                        exige_midia = t.Cabecalho.ExigeMidia,
                        exemplo = t.Cabecalho.Exemplo,
                        // A arte NOSSA, que vai em cada envio. O "exemplo" acima é o handle da
                        // Meta e não serve para reenviar.
                        arte = artesDoWaba.TryGetValue(t.Nome, out var arte) ? Absoluta(arte.Caminho) : null,
                    },
                    waba_id = w.WabaId,
                });
            }
        }

        return Ok(new { templates = saida });
    }

    public sealed record DefinirArteRequest(Guid? MidiaId);

    /// <summary>
    /// Escolhe a arte de cabeçalho de um modelo, entre as mídias já hospedadas para o cliente.
    /// Corpo sem <c>midiaId</c> (ou nulo) desfaz a escolha.
    ///
    /// <para>É o par do upload em <c>POST /v1/midias</c>: sobe-se o arquivo uma vez e aponta-se
    /// quantos modelos quiser para ele.</para>
    /// </summary>
    [HttpPut("{nome}/arte")]
    public async Task<IActionResult> DefinirArte(
        string nome, [FromBody] DefinirArteRequest corpo, [FromQuery] string? waba, CancellationToken ct)
    {
        var chamador = await tokens.AutenticarAsync(Request.Headers.Authorization.ToString(), ct);
        if (chamador is null) return Unauthorized(new { erro = "Token inválido, revogado ou tenant suspenso." });

        var wabas = await WabasDoChamadorAsync(chamador, ct);
        if (wabas.Count == 0) return BadRequest(new { erro = "Este token não alcança nenhuma conta WhatsApp." });

        // Com um WABA só (o caso normal), não se pede o que não há o que escolher.
        var alvo = waba is { Length: > 0 }
            ? wabas.FirstOrDefault(w => w.WabaId == waba)
            : wabas.Count == 1 ? wabas[0] : null;
        if (alvo is null)
            return BadRequest(new { erro = "Informe waba=<id> — o token alcança mais de uma conta." });

        if (corpo?.MidiaId is not { } midiaId || midiaId == Guid.Empty)
        {
            await artes.RemoverAsync(alvo.Id, nome, ct);
            return Ok(new { template = nome, arte = (string?)null });
        }

        var (ok, erro) = await artes.DefinirAsync(alvo.Id, nome, midiaId, usuarioId: null, ct);
        if (!ok) return BadRequest(new { erro });

        logger.LogInformation("Arte {Midia} escolhida para o modelo {Template}.", midiaId, nome);
        return Ok(new { template = nome, arte = Absoluta($"/midias/{midiaId}") });
    }

    private sealed record WabaAlcancado(Guid Id, string WabaId, string? Nome);

    /// <summary>
    /// WABAs que o token alcança. Vem dos NÚMEROS que ele pode usar: enxergar o catálogo de uma
    /// linha pela qual não se pode enviar não faria sentido.
    /// </summary>
    private async Task<List<WabaAlcancado>> WabasDoChamadorAsync(
        ChamadorAutenticado chamador, CancellationToken ct)
        => chamador.TodosNumeros
            ? await db.Wabas.AsNoTracking()
                .Where(w => w.TenantId == chamador.TenantId)
                .Select(w => new WabaAlcancado(w.Id, w.WabaId, w.Nome))
                .ToListAsync(ct)
            : await db.TenantTokenNumeros.AsNoTracking()
                .Where(x => x.TokenId == chamador.TokenId)
                .Select(x => new WabaAlcancado(x.Numero!.Waba!.Id, x.Numero.Waba.WabaId, x.Numero.Waba.Nome))
                .Distinct()
                .ToListAsync(ct);

    /// <summary>A URL que a Meta vai baixar precisa ser absoluta.</summary>
    private string Absoluta(string caminho) => $"{Request.Scheme}://{Request.Host}{caminho}";
}
