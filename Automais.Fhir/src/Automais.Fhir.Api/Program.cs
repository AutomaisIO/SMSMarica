using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core;
using Automais.Fhir.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<FhirDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("FhirDb"),
        npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", FhirDbContext.Schema)));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddFhirCore();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<FhirDbContext>();

var app = builder.Build();

// Auto-provisionamento: garante o schema fhir e aplica migrations no startup.
// O serviço é dono exclusivo do schema fhir e suas migrations são controladas
// (modelo JSONB, aditivas), então auto-migrate aqui é seguro e torna o deploy
// self-contained/portável. Desligável via AutoMigrate__Enabled=false.
if (app.Configuration.GetValue("AutoMigrate:Enabled", defaultValue: true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<FhirDbContext>();
    try
    {
        app.Logger.LogInformation("Garantindo schema {Schema} e aplicando migrations...", FhirDbContext.Schema);
        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS {FhirDbContext.Schema}");
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Schema/migrations OK.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Falha provisionando o banco no startup ({Tipo}: {Mensagem}).",
            ex.GetType().Name, ex.Message);
    }
}

app.UseMiddleware<OperationOutcomeMiddleware>();

// Decisão de produto (espelha smsmarica): OpenAPI exposto em dev e prod.
app.MapOpenApi();
app.MapScalarApiReference("/docs");

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
