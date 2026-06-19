using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using SMSMarica.Api.Auth;
using SMSMarica.Api.Middleware;
using SMSMarica.Core;
using SMSMarica.Core.Identidade;
using SMSMarica.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddControllers(o =>
{
    // [Authorize] global: tudo exige token, exceto endpoints com [AllowAnonymous].
    // Aceita JWT de usuário OU chave de serviço (X-API-Key) — esta última usada
    // por integrações externas (ex.: CentralIA chamando /integracoes).
    var politica = new AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationHandler.Esquema)
        .RequireAuthenticatedUser()
        .Build();
    o.Filters.Add(new AuthorizeFilter(politica));
})
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

// Tempo real (TFD): SignalR + notificador concreto (sobrescreve o no-op do Core).
builder.Services.AddSignalR();
builder.Services.AddScoped<SMSMarica.Core.Rastreamento.IRastreamentoNotificador, SMSMarica.Api.Realtime.RastreamentoNotificadorSignalR>();

// Token JWT do paciente (login CPF + OTP do PWA).
builder.Services.AddScoped<SMSMarica.Core.Cidadao.IPacienteTokenService, SMSMarica.Api.Auth.PacienteTokenService>();

// Módulo IA: cifragem de segredos (token do provedor, senha das bases) em repouso.
builder.Services.AddDataProtection();
builder.Services.AddScoped<SMSMarica.Core.Inteligencia.Seguranca.IProtetorSegredos, SMSMarica.Api.Auth.ProtetorSegredos>();

// Autenticação JWT (token emitido em /identidade/login).
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Secao));
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtualAccessor, UsuarioAtualAccessor>();

var jwt = builder.Configuration.GetSection(JwtOptions.Secao).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = string.IsNullOrEmpty(jwt.Key)
                ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 32)))
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        // SignalR envia o JWT via query string (access_token) no handshake do WebSocket.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            // Single-device do cidadão: o jti do token = id da sessão; se não bater com
            // a sessão ativa (login em outro device / logout / expirada), rejeita o token.
            OnTokenValidated = async ctx =>
            {
                var principal = ctx.Principal;
                if (principal?.FindFirst("tipo")?.Value != "cidadao") return;

                var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                if (!Guid.TryParse(sub, out var pacienteId) || !Guid.TryParse(jti, out var sessaoId))
                {
                    ctx.Fail("Token de cidadão inválido.");
                    return;
                }

                var sessoes = ctx.HttpContext.RequestServices
                    .GetRequiredService<SMSMarica.Core.Cidadao.ICidadaoSessaoService>();
                if (!await sessoes.SessaoValidaAsync(sessaoId, pacienteId, ctx.HttpContext.RequestAborted))
                {
                    ctx.Fail("Sessão encerrada (login em outro dispositivo).");
                }
            },
        };
    })
    // Chave de serviço (X-API-Key) para integrações externas (ex.: CentralIA).
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.Esquema, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SmsMaricaDbContext>(
        name: "db",
        tags: ["ready"]);

// CORS configurável: se "Cors:Origins" estiver definido, restringe a esses origins
// (recomendado em prod). Sem config, mantém permissivo (compat). O canal do agente
// NÃO é browser, então CORS nunca o bloqueia — isto protege só o painel web.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (corsOrigins.Length > 0)
        p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    else
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();

    // Permite o front ler o nome do arquivo nos downloads (ex.: laudo-...-assinado.pdf).
    p.WithExposedHeaders("Content-Disposition");
}));

// Rate-limit de defesa-em-profundidade nos endpoints anônimos do agente (a chave É a
// autorização). Particiona por IP; bloqueia brute-force/DoS por amplificação (cada
// preparar/concluir renderiza PDF e chama o iText). Aplicado via [EnableRateLimiting].
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("agente-assinatura", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

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
app.MapHub<SMSMarica.Api.Hubs.RastreamentoHub>("/hubs/rastreamento");

// Default false (ADR-0010 / recuperação): evita migration destrutiva acidental no startup.
// Habilitar explicitamente via AutoMigrate__Enabled=true quando for intencional.
var autoMigrate = builder.Configuration.GetValue("AutoMigrate:Enabled", defaultValue: false);
if (autoMigrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
    try
    {
        app.Logger.LogInformation("Aplicando migrations pendentes...");
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Migrations OK.");

        var hasher = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<SMSMarica.Data.Entities.Usuario>>();
        await DbSeeder.SeedAsync(db, hasher);
        app.Logger.LogInformation("Seed do Admin OK.");
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
