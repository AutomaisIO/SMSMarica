using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Laudos.Assinatura;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Retorno da autorização em nuvem (IntegraICP → VIDaaS, ADR-0061). É para cá que o navegador
/// do médico volta depois de ele aprovar no app. Anônimo por natureza (é um redirect do
/// provedor); quem autoriza é o <c>state</c> opaco que só o nosso "iniciar" conhece.
/// Responde HTML curto: o painel, que está fazendo polling, abre a conferência sozinho.
/// </summary>
[ApiController]
[Route("assinatura/nuvem")]
[AllowAnonymous]
[EnableRateLimiting("agente-assinatura")]
public sealed partial class AssinaturaNuvemController(
    ILaudoAssinaturaService assinatura,
    ILogger<AssinaturaNuvemController> logger) : ControllerBase
{
    /// <summary>
    /// Nomes em que a credencial pode chegar. O nome real não está documentado (spike de
    /// 04/08/2026); sem casar por nome, cai no primeiro valor com formato de ULID.
    /// </summary>
    private static readonly string[] NomesCredencial = ["credentialId", "credential_id", "credential", "id"];

    [HttpGet("retorno")]
    [Produces("text/html")]
    public async Task<IActionResult> Retorno(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var consulta = Request.Query;
        var state = consulta["state"].ToString();
        var credencial = AcharCredencial(consulta);
        // Só nomes: o contrato do retorno não é documentado e é aqui que se descobre.
        logger.LogInformation(
            "Assinatura em nuvem: retorno recebido (parâmetros: {Parametros}; credencial pelo parâmetro {ParametroCredencial}).",
            string.Join(", ", consulta.Keys),
            credencial is null ? "(nenhum)" : consulta.FirstOrDefault(kv => kv.Value.ToString() == credencial).Key);

        if (consulta.ContainsKey("error"))
        {
            logger.LogWarning("Assinatura em nuvem: provedor devolveu erro no retorno ({Erro}).", consulta["error"].ToString());
            return Pagina(false, "A autorização não foi concluída no aplicativo. Volte ao painel e clique em Assinar de novo.");
        }

        try
        {
            await assinatura.ConcluirNuvemAsync(state, credencial ?? string.Empty, cancellationToken);
            return Pagina(true, "Assinatura feita. Volte ao painel: o laudo assinado já está aberto para a sua conferência.");
        }
        catch (Exception ex) when (ex is ConflitoException or ValidacaoException)
        {
            return Pagina(false, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Assinatura em nuvem: falha inesperada no retorno.");
            return Pagina(false, "Não foi possível concluir a assinatura. Volte ao painel e tente de novo; se repetir, avise o suporte.");
        }
    }

    private static string? AcharCredencial(IQueryCollection consulta)
    {
        foreach (var nome in NomesCredencial)
        {
            var v = consulta[nome].ToString();
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return consulta
            .Where(kv => kv.Key != "state")
            .Select(kv => kv.Value.ToString())
            .FirstOrDefault(v => Ulid().IsMatch(v));
    }

    private ContentResult Pagina(bool ok, string mensagem)
    {
        var cor = ok ? "#137333" : "#b3261e";
        var fundo = ok ? "#e8f6ec" : "#fdecec";
        var titulo = ok ? "✓ Assinatura concluída" : "Assinatura não concluída";
        var html = $$"""
            <!doctype html>
            <html lang="pt-BR"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1"><meta name="robots" content="noindex">
            <title>Assinatura do laudo</title>
            <style>
              body { margin:0; font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif; background:#f5f5f7; color:#2b2b2b; padding:24px; }
              .wrap { max-width:460px; margin:40px auto; text-align:center; }
              .selo { font-weight:700; font-size:18px; padding:14px; border-radius:12px; background:{{fundo}}; color:{{cor}}; }
              p { font-size:14px; line-height:1.5; color:#555; margin-top:16px; }
            </style></head>
            <body><div class="wrap">
              <div class="selo">{{titulo}}</div>
              <p>{{HtmlEncoder.Default.Encode(mensagem)}}</p>
              <p>Você pode fechar esta aba.</p>
            </div></body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Za-hjkmnp-tv-z]{26}$")]
    private static partial Regex Ulid();
}
