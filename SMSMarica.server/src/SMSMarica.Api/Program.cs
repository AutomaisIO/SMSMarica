using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using SMSMarica.Api.Middleware;
using SMSMarica.Core;
using SMSMarica.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation()
    .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssembly(typeof(SMSMarica.Core.DependencyInjection).Assembly);

builder.Services.AddData(builder.Configuration);
builder.Services.AddCore();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

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

app.MapHealthChecks("/health");
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Não foi possível aplicar migrations no startup (Postgres pode estar indisponível).");
    }
}

await app.RunAsync();

public partial class Program;
