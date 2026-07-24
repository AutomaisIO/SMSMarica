using System.Text.Json;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using SMSMarica.Secretario.Api.Painel;

var builder = WebApplication.CreateBuilder(args);

// Credencial local SEM entrar no git: appsettings.Local.json (gitignored) ou env Salux__Usuario/Salux__Senha.
// O JSON local é registrado ANTES dos providers de variáveis de ambiente, para env vars
// vencerem o arquivo (precedência padrão do ASP.NET: appsettings < Local.json < env vars).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
var fontes = builder.Configuration.Sources;
var indicePrimeiroEnv = -1;
for (var i = 0; i < fontes.Count; i++)
{
    if (fontes[i] is EnvironmentVariablesConfigurationSource)
    {
        indicePrimeiroEnv = i;
        break;
    }
}

if (indicePrimeiroEnv >= 0)
{
    var fonteLocal = fontes[^1];
    fontes.RemoveAt(fontes.Count - 1);
    fontes.Insert(indicePrimeiroEnv, fonteLocal);
}

builder.Services.Configure<SaluxOpcoes>(builder.Configuration.GetSection("Salux"));
builder.Services.Configure<PainelOpcoes>(builder.Configuration.GetSection("Painel"));

builder.Services.AddSingleton<SnapshotStore>();
builder.Services.AddHostedService<PainelAtualizadorService>();

// Dados públicos agregados: GET liberado para qualquer origem.
builder.Services.AddCors(opcoes => opcoes.AddDefaultPolicy(politica =>
    politica.AllowAnyOrigin().WithMethods("GET").AllowAnyHeader()));

var app = builder.Build();

// Restart não serve tela vazia: carrega o último snapshot persistido antes de aceitar tráfego.
app.Services.GetRequiredService<SnapshotStore>().CarregarDeDisco();

app.UseCors();

// Serialização camelCase (JsonSerializerDefaults.Web) — mesmo shape do contrato-painel.json.
var opcoesJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapGet("/health", () => Results.Text("ok"));

app.MapGet("/api/painel", (SnapshotStore store, HttpResponse resposta) =>
{
    resposta.Headers.CacheControl = "no-store";

    var snapshot = store.Atual;
    return snapshot is null
        ? Results.Json(new { mensagem = "aguardando primeira carga do Salux" }, opcoesJson, statusCode: StatusCodes.Status503ServiceUnavailable)
        : Results.Json(snapshot, opcoesJson);
});

app.Run();
