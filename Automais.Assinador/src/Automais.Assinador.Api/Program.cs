using Automais.Assinador.Api.Infra;
using Automais.Assinador.Core.Pades;
using Microsoft.Extensions.Hosting.WindowsServices;
using Scalar.AspNetCore;
using Serilog;

// ContentRoot = pasta do próprio executável (não o diretório de trabalho de quem lançou) —
// essencial para rodar como serviço/exe portátil e ler o appsettings.json publicado ao lado.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Permite rodar como Serviço do Windows (no-op fora desse contexto — Linux/systemd segue normal).
builder.Host.UseWindowsService(o => o.ServiceName = "Automais.Assinador");

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddSingleton<IPadesSigner, PadesSigner>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
// Fronteira de confiança: exige X-Assinador-Token em /pades/* quando configurado.
app.UseMiddleware<TokenAutenticacaoMiddleware>();

// Decisão de produto (espelha smsmarica/fhir): OpenAPI exposto em dev e prod.
app.MapOpenApi();
app.MapScalarApiReference("/docs");

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// AGPL-3.0 §13: o serviço linka iText (AGPL) in-process, então oferece a
// Corresponding Source a quem interage pela rede. Aponta para o repositório público.
var fonteUrl = app.Configuration["Assinador:FonteUrl"]
    ?? "https://github.com/AutomaisIO/Automais.Assinador";
app.MapGet("/source", () => Results.Json(new
{
    licenca = "AGPL-3.0-or-later",
    componente = "Automais.Assinador (iText 9.x)",
    fonte = fonteUrl,
    aviso = "Este serviço usa iText sob AGPL-3.0. O código-fonte correspondente está disponível no endereço acima.",
}));

app.Run();

/// <summary>Exposto para testes de integração (WebApplicationFactory).</summary>
public partial class Program;
