using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core;
using Automais.Fhir.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<FhirDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("FhirDb")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddFhirCore();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<FhirDbContext>();

var app = builder.Build();

app.UseMiddleware<OperationOutcomeMiddleware>();

// Decisão de produto (espelha smsmarica): OpenAPI exposto em dev e prod.
app.MapOpenApi();
app.MapScalarApiReference("/docs");

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
