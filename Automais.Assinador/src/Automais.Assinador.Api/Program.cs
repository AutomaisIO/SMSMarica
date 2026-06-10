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

// Decisão de produto (espelha smsmarica/fhir): OpenAPI exposto em dev e prod.
app.MapOpenApi();
app.MapScalarApiReference("/docs");

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

/// <summary>Exposto para testes de integração (WebApplicationFactory).</summary>
public partial class Program;
