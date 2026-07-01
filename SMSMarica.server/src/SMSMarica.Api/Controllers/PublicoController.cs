using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Downloads;
using SMSMarica.Core.SolicitacoesExame.Declaracao;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Páginas/endpoints públicos (sem autenticação): verificação do selo da Declaração
/// de Comparecimento e o download por token de uso único (link enviado ao paciente).
/// </summary>
[ApiController]
[Route("publico")]
[AllowAnonymous]
public sealed class PublicoController(
    IDeclaracaoComparecimentoService declaracao,
    IDownloadTokenService downloads) : ControllerBase
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>Estado do link de download (não consome) — a página decide baixar vs "expirou".</summary>
    [HttpGet("download/{token:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadStatus(Guid token, CancellationToken cancellationToken)
    {
        var s = await downloads.ObterStatusAsync(token, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { estado = s.Estado.ToString().ToLowerInvariant(), descricao = s.Descricao });
    }

    /// <summary>Baixa o arquivo do token (uso único). 410 se já usado/expirado/inexistente.</summary>
    [HttpGet("download/{token:guid}")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Download(Guid token, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var arquivo = await downloads.ConsumirAsync(token, ip, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        if (arquivo is null) return StatusCode(StatusCodes.Status410Gone);
        return File(arquivo.Bytes, arquivo.ContentType, arquivo.NomeArquivo);
    }

    /// <summary>Página HTML que confirma a validade de uma Declaração de Comparecimento.</summary>
    [HttpGet("declaracoes/{codigo:guid}")]
    [Produces("text/html")]
    public async Task<IActionResult> VerificarDeclaracao(Guid codigo, CancellationToken cancellationToken)
    {
        var dados = await declaracao.VerificarAsync(codigo, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        var html = dados is null ? PaginaInvalida() : PaginaValida(dados, codigo);
        return Content(html, "text/html; charset=utf-8");
    }

    private static string PaginaValida(DeclaracaoVerificacaoDto d, Guid codigo)
    {
        var nome = Html(d.Nome);
        var descricao = Html(d.Descricao);
        var unidade = string.IsNullOrWhiteSpace(d.Unidade) ? null : Html(d.Unidade!);
        var dataHora = Html(d.DataHora.ToString("dd/MM/yyyy 'às' HH'h'mm", PtBr));

        var linhaUnidade = unidade is null
            ? string.Empty
            : $"""<div class="row"><span class="rotulo">Unidade</span><span class="valor">{unidade}</span></div>""";

        return Pagina($"""
            <div class="selo selo-ok">✓ Documento autêntico</div>
            <p class="sub">Esta Declaração de Comparecimento foi emitida pela Secretaria Municipal de Saúde de Maricá.</p>
            <div class="card">
              <div class="row"><span class="rotulo">Nome</span><span class="valor">{nome}</span></div>
              <div class="row"><span class="rotulo">Data e hora</span><span class="valor">{dataHora}</span></div>
              <div class="row"><span class="rotulo">Exame</span><span class="valor">{descricao}</span></div>
              {linhaUnidade}
            </div>
            <p class="codigo">Código de autenticidade<br><b>{Html(codigo.ToString())}</b></p>
            """);
    }

    private static string PaginaInvalida() =>
        Pagina("""
            <div class="selo selo-erro">Documento não encontrado</div>
            <p class="sub">O código informado não corresponde a nenhuma Declaração de Comparecimento emitida.
            Verifique se o QR Code foi lido corretamente.</p>
            """);

    private static string Pagina(string conteudo) =>
        $$"""
        <!doctype html>
        <html lang="pt-BR">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="robots" content="noindex">
          <title>Verificação de Documento — SMS Maricá</title>
          <style>
            :root { --marica:#C4122F; --vinho:#7A0C24; --tinta:#2B2B2B; }
            * { box-sizing: border-box; }
            body { margin:0; font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
                   background:#f5f5f7; color:var(--tinta); padding:24px; }
            .wrap { max-width:480px; margin:0 auto; }
            .top { text-align:center; margin-bottom:16px; }
            .top h1 { color:var(--marica); font-size:18px; margin:0; }
            .top p { color:#666; font-size:13px; margin:4px 0 0; }
            .selo { text-align:center; font-weight:700; font-size:18px; padding:14px; border-radius:12px; }
            .selo-ok { background:#e8f6ec; color:#137333; border:1px solid #b7e1c3; }
            .selo-erro { background:#fdecec; color:#b3261e; border:1px solid #f3c0bd; }
            .sub { color:#555; font-size:13px; text-align:center; margin:12px 4px 18px; line-height:1.5; }
            .card { background:#fff; border:1px solid #eee; border-radius:12px; padding:4px 16px; box-shadow:0 1px 3px rgba(0,0,0,.05); }
            .row { display:flex; justify-content:space-between; gap:12px; padding:12px 0; border-bottom:1px solid #f0f0f0; }
            .row:last-child { border-bottom:0; }
            .rotulo { color:#888; font-size:13px; }
            .valor { font-weight:600; text-align:right; }
            .codigo { text-align:center; color:#999; font-size:11px; margin-top:18px; word-break:break-all; }
            .rodape { text-align:center; color:#aaa; font-size:11px; margin-top:24px; }
          </style>
        </head>
        <body>
          <div class="wrap">
            <div class="top">
              <h1>Saúde Maricá</h1>
              <p>Verificação de autenticidade</p>
            </div>
            {{conteudo}}
            <p class="rodape">Secretaria Municipal de Saúde de Maricá</p>
          </div>
        </body>
        </html>
        """;

    private static string Html(string valor) => HtmlEncoder.Default.Encode(valor);
}
