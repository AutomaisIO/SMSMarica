using SMSMarica.Core.Integracoes.SisregWeb;

namespace SMSMarica.Tests.Integracoes.Sisreg;

/// <summary>
/// Distinguir "sessão derrubada" de "CAPTCHA anti-robô" no SISREG.
///
/// <para>As duas coisas exigem reações opostas: sessão derrubada se resolve relogando, sozinho;
/// CAPTCHA não se resolve relogando e precisa de um humano no navegador. Confundir uma com a
/// outra manda o operador esperar 24h por um problema que se resolveria em 1 segundo.</para>
///
/// <para><b>Regressão real (03/08/2026):</b> o operador humano logou no SISREG, derrubou a sessão
/// do robô (sessão única por operador), e a varredura reportou CAPTCHA — porque a tela de erro
/// carrega o script do reCAPTCHA como qualquer outra, e o detector casava o literal solto.</para>
/// </summary>
public class DeteccaoSessaoECaptchaTests
{
    /// <summary>O script que TODA tela do SISREG carrega — inclusive as que não têm CAPTCHA nenhum.</summary>
    private const string ScriptRecaptcha =
        "<script src=\"https://www.google.com/recaptcha/api.js\" async defer></script>";

    /// <summary>Tela de erro do SISREG quando a sessão do operador foi derrubada em outra estação.</summary>
    private const string TelaSessaoDerrubada = $"""
        <html><head>{ScriptRecaptcha}</head>
        <body><table><tr><td>Erro de Sistema</td></tr>
        <tr><td>Erro ao carregar a sess&atilde;o do usu&aacute;rio.</td></tr></table>
        <a href="/cgi-bin/sisreg_erro">voltar</a></body></html>
        """;

    private const string TelaLogin = $"""
        <html><head>{ScriptRecaptcha}</head>
        <body><form name="formLogin"><input name="usuario"><input name="senha_256" type="hidden">
        </form></body></html>
        """;

    private const string TelaCaptcha = """
        <html><body>
        <SCRIPT type="text/javascript">window.location="./recaptcha?cod=0"</SCRIPT>
        </body></html>
        """;

    private const string TelaCaptchaWidget = $"""
        <html><head>{ScriptRecaptcha}</head>
        <body><div class="g-recaptcha" data-sitekey="6LeMZwgsAAAAAK85NcnUrumUNjhKA2qR2iWXklw4"></div>
        </body></html>
        """;

    private const string HomeLogada = $"""
        <html><head>{ScriptRecaptcha}</head>
        <body>Operador: 000-TESTE Perfil: EXECUTANTE Unidade: UNIDADE DE TESTE (1234567)</body></html>
        """;

    // ------------------------------------------------------- sessão derrubada NÃO é captcha

    [Fact]
    public void Tela_de_sessao_derrubada_e_reconhecida_como_sessao_invalida()
    {
        // É o teste que faltava: sem isto, a sessão morta não relogava.
        Assert.True(CadsusHtmlParser.SessaoInvalida(TelaSessaoDerrubada));
    }

    [Fact]
    public void Tela_de_sessao_derrubada_NAO_e_captcha()
    {
        Assert.False(SisregHomeParser.ExigeCaptcha(TelaSessaoDerrubada));
    }

    [Fact]
    public void Tela_de_login_e_sessao_invalida_e_NAO_e_captcha()
    {
        Assert.True(CadsusHtmlParser.SessaoInvalida(TelaLogin));
        Assert.False(SisregHomeParser.ExigeCaptcha(TelaLogin));
    }

    [Fact]
    public void Home_logada_normal_NAO_e_captcha_nem_sessao_invalida()
    {
        // Carrega o mesmo script do reCAPTCHA e está tudo bem.
        Assert.False(SisregHomeParser.ExigeCaptcha(HomeLogada));
        Assert.False(CadsusHtmlParser.SessaoInvalida(HomeLogada));
    }

    // ------------------------------------------------------- captcha de verdade

    [Fact]
    public void Redirect_para_recaptcha_e_captcha()
    {
        Assert.True(SisregHomeParser.ExigeCaptcha(TelaCaptcha));
    }

    [Fact]
    public void Widget_do_recaptcha_e_captcha()
    {
        Assert.True(SisregHomeParser.ExigeCaptcha(TelaCaptchaWidget));
    }

    [Theory]
    [InlineData("<html><body>diferencia&ccedil;&atilde;o entre computadores</body></html>")]
    [InlineData("<html><body>diferenciação entre computadores</body></html>")]
    public void Texto_da_tela_de_captcha_e_captcha(string html)
    {
        Assert.True(SisregHomeParser.ExigeCaptcha(html));
    }

    // ------------------------------------------------------- bordas

    [Fact]
    public void Html_vazio_nao_e_captcha()
    {
        Assert.False(SisregHomeParser.ExigeCaptcha(""));
        Assert.False(SisregHomeParser.ExigeCaptcha(null!));
    }

    [Fact]
    public void Marcadores_antigos_de_sessao_continuam_valendo()
    {
        Assert.True(CadsusHtmlParser.SessaoInvalida("<html>efetuou logon em outra esta&ccedil;&atilde;o</html>"));
        Assert.True(CadsusHtmlParser.SessaoInvalida("<html>sua sess&atilde;o foi finalizada</html>"));
    }
}
