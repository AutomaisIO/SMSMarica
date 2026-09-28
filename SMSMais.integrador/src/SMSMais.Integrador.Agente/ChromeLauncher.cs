using Microsoft.Win32;
using PuppeteerSharp;

namespace SMSMais.Integrador.Agente;

/// <summary>
/// Acha o Chrome (ou Edge) instalado e o lança com PERFIL PRÓPRIO e a porta de depuração ligada.
/// Perfil próprio é obrigatório: desde o Chrome 136 ele recusa depurar o perfil padrão do usuário.
/// Sem <c>--enable-automation</c>, para não aparecer a barra "controlado por software de teste".
/// </summary>
public static class ChromeLauncher
{
    public static string AcharExecutavel(string navegador)
    {
        var exe = navegador == "edge" ? "msedge.exe" : "chrome.exe";

        // 1) App Paths (é como o Windows abre pelo nome). HKLM e HKCU (instalação por usuário).
        foreach (var raiz in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var chave = raiz.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe);
                if (chave?.GetValue(null) is string caminho && File.Exists(caminho)) return caminho;
            }
            catch
            {
                // sem acesso a esta raiz — segue para a próxima
            }
        }

        // 2) Pastas conhecidas.
        var candidatos = navegador == "edge"
            ? new[]
            {
                @"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe",
                @"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe",
            }
            : new[]
            {
                @"%ProgramFiles%\Google\Chrome\Application\chrome.exe",
                @"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe",
                @"%LocalAppData%\Google\Chrome\Application\chrome.exe",
            };
        foreach (var c in candidatos)
        {
            var p = Environment.ExpandEnvironmentVariables(c);
            if (File.Exists(p)) return p;
        }

        throw new FileNotFoundException($"Não achei o {exe} instalado. Instale o Chrome (ou rode com --edge).");
    }

    public static async Task<IBrowser> LancarAsync(Config cfg, Action<string> log)
    {
        var exe = AcharExecutavel(cfg.Navegador);
        Directory.CreateDirectory(cfg.PastaPerfil);
        log($"Navegador: {exe}");
        log($"Perfil:    {cfg.PastaPerfil}");

        var opcoes = new LaunchOptions
        {
            ExecutablePath = exe,
            Headless = false,
            UserDataDir = cfg.PastaPerfil,
            // A barra amarela "controlado por software de teste automatizado" vem desta flag.
            IgnoredDefaultArgs = ["--enable-automation"],
            // null = a página usa o tamanho da janela (senão o Puppeteer fixa 800x600).
            DefaultViewport = null,
            Args =
            [
                "--no-first-run",
                "--no-default-browser-check",
                "--window-size=1280,900",
                "--disable-features=Translate",
                cfg.UrlInicial, // primeira aba; sem isto o Puppeteer abriria about:blank
            ],
        };

        return await Puppeteer.LaunchAsync(opcoes);
    }
}
