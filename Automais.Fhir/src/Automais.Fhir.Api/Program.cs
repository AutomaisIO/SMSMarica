using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core;
using Automais.Fhir.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

// Teto do pool de conexões: o banco gerenciado tem poucas conexões compartilhadas entre
// apps; sem teto, o default do Npgsql (100) deixa o hub sozinho estourar o limite sob a
// carga da importação. Defina Db:MaxPoolSize (ou Maximum Pool Size na própria connection string).
var fhirConn = builder.Configuration.GetConnectionString("FhirDb");
var fhirMaxPool = builder.Configuration.GetValue<int?>("Db:MaxPoolSize");
if (!string.IsNullOrWhiteSpace(fhirConn))
{
    // search_path inclui smsmarica para que a função unaccent() (extensão instalada no schema
    // smsmarica pela migration do SMSMarica.server, no mesmo banco) resolva na busca de paciente
    // por nome. As tabelas do hub são qualificadas (schema fhir via HasDefaultSchema), então
    // manter fhir/public no path preserva o comportamento atual.
    var csb = new Npgsql.NpgsqlConnectionStringBuilder(fhirConn) { SearchPath = "fhir, smsmarica, public" };
    if (fhirMaxPool is > 0) csb.MaxPoolSize = fhirMaxPool.Value;
    fhirConn = csb.ConnectionString;
}

builder.Services.AddDbContext<FhirDbContext>(opt =>
    opt.UseNpgsql(
        fhirConn,
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
