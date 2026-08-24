namespace SMSMais.Core.Integracoes.SisregWeb;

/// <summary>
/// Detecção do anti-bot nas respostas do SISREG.
/// </summary>
public static class SisregHomeParser
{
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
}
