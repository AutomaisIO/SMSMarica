using System.Text;
using SMSMais.Integrador.Agente;

// Integrador SMSMais — agente Windows (PoC). Lança o Chrome instalado com perfil PRÓPRIO e a porta
// de depuração ligada (CDP) e faz, sem extensão, o que a extensão SMSMais.chrome faz: observa o
// operador nos sistemas configurados (SISREG, Ecossistemas), desenha o selo/blur/janela sobre a
// página e manda as capturas em lote para a MESMA rota da extensão (POST /extensao/sisreg/capturas).
//
// Só observa: nunca dispara requisição para o SISREG (o orçamento anti-robô é do operador).
// Sem admin: tudo fica em %LOCALAPPDATA%\SMSMais\integrador.

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("smsmais-integrador [--api URL] [--painel URL] [--url URL] [--edge]");
    Console.WriteLine("  --api URL     backend que recebe as capturas (padrão http://localhost:5080)");
    Console.WriteLine("  --painel URL  painel de onde a sessão é lida (padrão https://smsmarica.online)");
    Console.WriteLine("  --url URL     primeira aba (padrão https://sisregiii.saude.gov.br/)");
    Console.WriteLine("  --edge        usa o Edge em vez do Chrome");
    Console.WriteLine("  --verboso     loga cada captura (kind + caminho)");
    return 0;
}

var cfg = Config.DeArgs(args);
Directory.CreateDirectory(cfg.PastaDados);
var logPath = Path.Combine(cfg.PastaDados, "agente.log");

void Log(string msg)
{
    var linha = $"[{DateTime.Now:HH:mm:ss}] {msg}";
    Console.WriteLine(linha);
    try { File.AppendAllText(logPath, linha + Environment.NewLine); }
    catch { /* best-effort */ }
}

Log($"Integrador SMSMais {cfg.Versao}");
Log($"API:       {cfg.ApiBase}");
Log($"Painel:    {cfg.PainelOrigin}");
Log($"Sítios:    {string.Join(", ", cfg.Sitios.Select(s => $"{s.Label} ({s.Modo})"))}");
if (!cfg.ApiBase.Contains("localhost", StringComparison.OrdinalIgnoreCase) && !cfg.ApiBase.Contains("127.0.0.1"))
    Log("ATENÇÃO: as capturas vão para um backend que NÃO é local.");

using var enviador = new Enviador(cfg, Log);
_ = enviador.BuscarMarcaAsync();

var browser = await ChromeLauncher.LancarAsync(cfg, Log);
Log("Chrome no ar (perfil próprio, sem barra de automação).");

var observador = new Observador(cfg, enviador, Log);
await observador.IniciarAsync(browser);

var fim = new TaskCompletionSource();
browser.Disconnected += (_, _) => fim.TrySetResult();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Log("Ctrl+C: o agente sai; o Chrome continua aberto (sem observação).");
    try { browser.Disconnect(); }
    catch { /* já caiu */ }
    fim.TrySetResult();
};

Log("Observando. Feche o Chrome (ou Ctrl+C) para encerrar.");
await fim.Task;
Log("Fim.");
return 0;
