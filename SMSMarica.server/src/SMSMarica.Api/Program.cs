using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using SMSMarica.Api.Middleware;
using SMSMarica.Core;
using SMSMarica.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Um ou mais erros de validação ocorreram.",
            Instance = context.HttpContext.Request.Path,
        };
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});

builder.Services.AddFluentValidationAutoValidation()
    .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssembly(typeof(SMSMarica.Core.DependencyInjection).Assembly);

builder.Services.AddData(builder.Configuration);
builder.Services.AddCore(builder.Configuration);

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SmsMaricaDbContext>(
        name: "db",
        tags: ["ready"]);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger / OpenAPI sempre ligado (dev e prod) — decisão do produto.
app.MapOpenApi();
app.MapScalarApiReference("/docs", options =>
{
    options.WithTitle("SMS Maricá — API")
        .WithTheme(ScalarTheme.Default);
});

// /health = completo (checa DB). /health/live = raso (só o processo).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = EscreverHealthJson,
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = EscreverHealthJson,
});

app.MapControllers();

var autoMigrate = builder.Configuration.GetValue("AutoMigrate:Enabled", defaultValue: true);
if (autoMigrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
    try
    {
        app.Logger.LogInformation("Aplicando migrations pendentes...");
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Migrations OK.");
    }
    catch (Exception ex)
    {
        // Não derruba o processo — o DbContextCheck vai reportar "Unhealthy" em /health
        // e as requisições que tocam DB vão cair com 500. Systemd pode reiniciar mais tarde
        // quando o banco estiver acessível.
        app.Logger.LogError(ex, "Falha aplicando migrations no startup ({Tipo}: {Mensagem}).",
            ex.GetType().Name, ex.Message);
    }
}

await app.RunAsync();

static Task EscreverHealthJson(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            durationMs = e.Value.Duration.TotalMilliseconds,
            description = e.Value.Description,
            exception = e.Value.Exception is null
                ? null
                : $"{e.Value.Exception.GetType().Name}: {e.Value.Exception.Message}",
        }),
    };
    return ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

public partial class Program;
