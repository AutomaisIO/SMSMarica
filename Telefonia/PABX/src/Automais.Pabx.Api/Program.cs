using Automais.Pabx.Api.Asterisk;
using Automais.Pabx.Api.Asterisk.Ami;
using Automais.Pabx.Api.Cdr;
using Automais.Pabx.Api.Data;
using Automais.Pabx.Api.Data.Seed;
using Automais.Pabx.Api.Infra;
using Automais.Pabx.Api.Provisionamento;
using Automais.Pabx.Api.Ramais;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

var dbPath = builder.Configuration["Pabx:DbPath"] ?? "pabx.db";
builder.Services.AddDbContext<PabxDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));

// Secrets dos ramais cifrados em repouso no SQLite; chaves locais na caixa.
var keysDir = builder.Configuration["Pabx:KeysDir"] ?? "keys";
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("Automais.Pabx");

builder.Services.Configure<AsteriskOptions>(builder.Configuration.GetSection(AsteriskOptions.Secao));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddSingleton<IAmiClientFactory, AmiClientFactory>();
builder.Services.AddScoped<IGeradorConfigSip, GeradorConfigChanSip>();
builder.Services.AddScoped<IProvisionamentoService, ProvisionamentoService>();
builder.Services.AddScoped<IRamalService, RamalService>();
builder.Services.AddScoped<IStatusService, StatusService>();
builder.Services.AddSingleton<ICdrService, CdrService>();

builder.Services.AddControllers()
    .AddJsonOptions(opt => opt.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<PabxDbContext>();

var app = builder.Build();

// Banco local do agente: auto-migrate + sincronização das unidades com o registro-mestre.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PabxDbContext>();
    await db.Database.MigrateAsync();
    await UnidadeSeeder.SeedAsync(db, app.Logger);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<ApiKeyMiddleware>();

// Decisão de produto (espelha smsmarica/fhir): OpenAPI exposto em dev e prod.
app.MapOpenApi();
app.MapScalarApiReference("/docs");

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposto para futuros testes de integração (WebApplicationFactory).</summary>
public partial class Program;
