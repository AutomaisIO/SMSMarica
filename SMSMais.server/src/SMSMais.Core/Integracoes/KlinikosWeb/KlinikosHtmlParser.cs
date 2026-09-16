using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>
/// Helpers de leitura de HTML do Klinikos (ASP.NET WebForms). Espelha o
/// <c>SerHtmlParser</c>, mas o alvo é WebForms (form <c>form1</c> no login, <c>aspnetForm</c>
/// nas telas), não JSF: os hidden que importam são <c>__VIEWSTATE</c>,
/// <c>__VIEWSTATEGENERATOR</c>, <c>__EVENTVALIDATION</c>.
/// </summary>
internal static class KlinikosHtmlParser
{
    private static readonly HtmlParser Parser = new();

    public static IHtmlDocument Documento(string html) => Parser.ParseDocument(html);

    /// <summary>Action do form (para montar a URL do POST), resolvido contra a URL da página.</summary>
    public static string? ActionDoForm(IHtmlDocument doc, string formId) =>
        (doc.GetElementById(formId) as IHtmlFormElement)?.GetAttribute("action");

    /// <summary>Primeiro form do documento (o Klinikos tem um form por página).</summary>
    public static IHtmlFormElement? PrimeiroForm(IHtmlDocument doc) =>
        doc.QuerySelector("form") as IHtmlFormElement;

    /// <summary>
    /// Campos submissíveis de um form (hidden/texto/select/checkbox marcado), sem os botões
    /// (submit/image/button). <c>&lt;select&gt;</c> SEM opção é omitido de propósito: mandá-lo
    /// dispara "Invalid postback or callback argument" na validação de eventos (medido 16/09).
    /// </summary>
    public static Dictionary<string, string> CamposDoForm(IHtmlFormElement form)
    {
        var dados = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var el in form.QuerySelectorAll("input"))
        {
            if (el is not IHtmlInputElement input || string.IsNullOrEmpty(input.Name)) continue;
            var tipo = (input.Type ?? "text").ToLowerInvariant();
            if (tipo is "submit" or "button" or "image" or "reset" or "file") continue;
            if (tipo is "checkbox" or "radio" && !input.IsChecked) continue;
            dados[input.Name] = input.Value ?? string.Empty;
        }

        foreach (var el in form.QuerySelectorAll("select"))
        {
            if (el is not IHtmlSelectElement select || string.IsNullOrEmpty(select.Name)) continue;
            var opcao = select.QuerySelector("option[selected]") as IHtmlOptionElement
                        ?? select.QuerySelector("option") as IHtmlOptionElement;
            if (opcao is null) continue; // select sem opção não vai no POST
            dados[select.Name] = opcao.Value ?? string.Empty;
        }

        foreach (var el in form.QuerySelectorAll("textarea"))
        {
            if (el is IHtmlTextAreaElement area && !string.IsNullOrEmpty(area.Name))
            {
                dados[area.Name] = area.Value ?? string.Empty;
            }
        }

        return dados;
    }

    /// <summary>Nome do primeiro input cujo id/nome termine no sufixo dado (ex.: um botão-imagem).</summary>
    public static string? NomePorSufixo(IHtmlFormElement form, string sufixo)
    {
        foreach (var el in form.QuerySelectorAll("input"))
        {
            if (el is IHtmlInputElement i && i.Name is { } n && n.EndsWith(sufixo, StringComparison.Ordinal))
            {
                return n;
            }
        }
        return null;
    }

    /// <summary>A página é a tela de login (sessão morta / não autenticado)?</summary>
    public static bool EhTelaDeLogin(string html) =>
        html.Contains("LoginView1_lgAcesso_UserName", StringComparison.Ordinal)
        || html.Contains("LoginView1$lgAcesso$UserName", StringComparison.Ordinal);

    /// <summary>A resposta do login pede confirmação de sessão em outra estação?</summary>
    public static bool PedeConfirmacaoDeSessao(string html) =>
        html.Contains("btnConfirmarLogin", StringComparison.Ordinal);
}
