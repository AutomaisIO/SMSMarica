using System.Net;
using System.Text.RegularExpressions;

namespace SMSMarica.Core.Integracoes.SisregWeb;

/// <summary>
/// Leitura da home logada do SISREG (<c>/cgi-bin/index</c>) e detecção do anti-bot.
/// </summary>
public static partial class SisregHomeParser
{
    /// <summary>
    /// Extrai "Operador / Perfil / Unidade (CNES)" da barra superior da home logada.
    /// Retorna <c>null</c> se a barra não estiver presente (não é a home logada).
    ///
    /// <para>A barra vem como HTML com entidades e <c>&amp;nbsp;</c> entre rótulo e valor,
    /// por isso normalizamos antes de casar.</para>
    /// </summary>
    public static SisregSessaoInfo? LerBarra(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var texto = WebUtility.HtmlDecode(RegexTags().Replace(html, " "));
        texto = RegexEspacos().Replace(texto.Replace(' ', ' '), " ");

        var m = RegexBarra().Match(texto);
        if (!m.Success) return null;

        var unidadeBruta = m.Groups["unidade"].Value.Trim();
        var mCnes = RegexCnes().Match(unidadeBruta);
        var cnes = mCnes.Success ? mCnes.Groups[1].Value : null;
        var unidadeNome = mCnes.Success
            ? unidadeBruta[..mCnes.Index].Trim()
            : unidadeBruta;

        return new SisregSessaoInfo(
            m.Groups["operador"].Value.Trim(),
            m.Groups["perfil"].Value.Trim(),
            unidadeNome,
            cnes);
    }

    /// <summary>
    /// O SISREG passou a exigir CAPTCHA (anti-bot por volume de requisições). Toda tela
    /// vira um redirect para <c>/cgi-bin/recaptcha</c>.
    ///
    /// <para><b>Relogar não resolve</b> — só um humano resolvendo o reCAPTCHA no navegador
    /// com aquele operador. Detectado aqui para virar erro tratado em vez de "lista vazia".</para>
    /// </summary>
    /// <remarks>
    /// <b>Nunca casar o literal <c>recaptcha</c> solto.</b> A tela de login e a de
    /// <c>sisreg_erro</c> (sessão derrubada) carregam
    /// <c>&lt;script src="https://www.google.com/recaptcha/api.js"&gt;</c> mesmo sem anti-bot
    /// nenhum. Com a regra frouxa, "o operador humano logou e derrubou a sessão do robô" era
    /// reportado como CAPTCHA — que manda esperar 24h e chamar alguém no navegador, quando bastava
    /// relogar. Casar só o que é exclusivo da tela de CAPTCHA: o redirect, o widget e o texto dela.
    /// </remarks>
    public static bool ExigeCaptcha(string html) =>
        !string.IsNullOrEmpty(html)
        && (html.Contains("recaptcha?cod=", StringComparison.OrdinalIgnoreCase)
            || html.Contains("g-recaptcha", StringComparison.OrdinalIgnoreCase)
            || html.Contains("diferencia&ccedil;&atilde;o entre computadores", StringComparison.OrdinalIgnoreCase)
            || html.Contains("diferenciação entre computadores", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex RegexTags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();

    [GeneratedRegex(
        @"Operador:\s*(?<operador>.+?)\s*Perfil:\s*(?<perfil>.+?)\s*Unidade:\s*(?<unidade>.+?)\s*(?:V\s*-|$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex RegexBarra();

    [GeneratedRegex(@"\((\d{2,10})\)\s*$")]
    private static partial Regex RegexCnes();
}
