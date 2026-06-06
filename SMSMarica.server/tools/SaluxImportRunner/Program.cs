using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMarica.Core.Integracoes.Pep.Estrategias;
using SMSMarica.Core.Integracoes.Pep.Estrategias.Salux;
using SMSMarica.Core.Integracoes.Pep.Fhir;
using SMSMarica.Core.Integracoes.Pep.Progresso;
using SMSMarica.Data.Entities.Enums;

// Roda a importação Salux→FHIR ON-PREM (esta máquina alcança o Oracle interno),
// dirigindo o MESMO motor do backend (SaluxImportacaoStrategy). Credenciais do
// SUPERVISOR vêm do Salux/.env (evita HTTP auth e a senha cifrada da IaFonte).
//
// Uso: dotnet run -- <maxMedicos> <maxPacientes> [apagar] [caminho .env]

var maxMed = args.Length > 0 && int.TryParse(args[0], out var a) ? a : 5;
var maxPac = args.Length > 1 && int.TryParse(args[1], out var b) ? b : 5;
var apagar = args.Length > 2 && args[2].Equals("apagar", StringComparison.OrdinalIgnoreCase);
var envPath = args.Length > 3 ? args[3] : @"C:\Projetos GIT\SMSMarica\Salux\.env";

var env = File.ReadAllLines(envPath)
    .Where(l => l.Contains('=') && !l.TrimStart().StartsWith('#'))
    .Select(l => l.Split('=', 2))
    .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

var dsn = env["SALUX_ORACLE_DSN"]; // ex.: 10.50.0.18:1521/ORASX01
var host = dsn.Split(':')[0];
var porta = int.Parse(dsn.Split(':')[1].Split('/')[0]);
var servico = dsn.Split('/')[1];
var usuario = env["SALUX_SUPERVISOR_USER"];
var senha = env["SALUX_SUPERVISOR_PASSWORD"];

var hub = Environment.GetEnvironmentVariable("FHIR_BASE_URL") ?? "http://smsmarica.online:5081/";
var conc = Environment.GetEnvironmentVariable("PEP_MAX_CONCORRENCIA") ?? "8";

// PEP_CDS="20269,13885,..." força importar exatamente esses pacientes (medição com histórico pesado).
var cdsEnv = Environment.GetEnvironmentVariable("PEP_CDS");
IReadOnlyList<long>? cds = string.IsNullOrWhiteSpace(cdsEnv)
    ? null
    : cdsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(long.Parse).ToList();

Console.WriteLine($"Oracle {host}:{porta}/{servico} (usuario={usuario}) | hub={hub} | concorrência={conc}");
Console.WriteLine(cds is null
    ? $"Modo=Completo Escopo=Limitado maxMedicos={maxMed} maxPacientes={maxPac} apagarAntes={apagar}"
    : $"Modo=Completo cds=[{string.Join(",", cds)}] (maxMedicos={maxMed}) apagarAntes={apagar}");
Console.WriteLine("Iniciando…\n");

var handler = new SocketsHttpHandler { MaxConnectionsPerServer = 64, PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
using var http = new HttpClient(handler) { BaseAddress = new Uri(hub), Timeout = TimeSpan.FromSeconds(60) };
var escritor = new HubFhirEscritor(http);
var progresso = new ProgressoImportacao();
var ctx = new ContextoImportacaoPep
{
    Conexao = new ConexaoFonte(host, porta, servico, usuario, senha, 120),
    Opcoes = new OpcoesImportacao(ModoSincronizacao.Completo, EscopoSincronizacao.Limitado, maxMed, maxPac, apagar, cds),
    Marca = new MarcaDagua(),
    Escritor = escritor,
    Progresso = progresso,
};

var strategy = new SaluxImportacaoStrategy(NullLogger<SaluxImportacaoStrategy>.Instance);

var sw = Stopwatch.StartNew();
try
{
    await strategy.ImportarAsync(ctx, default);
}
catch (Exception ex)
{
    Console.WriteLine($"\n!! ERRO: {ex.Message}");
}
sw.Stop();

Console.WriteLine($"\n== Fim em {sw.Elapsed.TotalSeconds:F1}s ==");
Console.WriteLine($"Médicos={progresso.Medicos} Pacientes={progresso.Pacientes} Atendimentos={progresso.Encounters} " +
    $"Diagnósticos={progresso.Conditions} Medicações={progresso.MedicationRequests} " +
    $"Documentos={progresso.DocumentReferences} Sinais/risco={progresso.Observations} Falhas={progresso.Falhas.Count}");
Console.WriteLine("Tempos por fase: " + string.Join(", ", progresso.Tempos.Select(t => $"{t.Key}={t.Value:F1}s")));
if (progresso.Pacientes > 0)
    Console.WriteLine($"Por paciente: {sw.Elapsed.TotalSeconds / progresso.Pacientes:F2}s" +
        (progresso.Encounters > 0 ? $" | por BAA: {sw.Elapsed.TotalSeconds / progresso.Encounters:F3}s" : ""));
foreach (var f in progresso.Falhas.Take(10))
    Console.WriteLine($"  falha cd={f.Cd}: {f.Mensagem}");
