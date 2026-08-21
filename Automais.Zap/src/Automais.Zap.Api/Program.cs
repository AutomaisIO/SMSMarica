using System.Threading.RateLimiting;
using Automais.Zap.Api.Infra;
using Automais.Zap.Core;
using Automais.Zap.Core.Admin;
using Automais.Zap.Core.Entregas;
using Automais.Zap.Core.Relay;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

// Banco LOCAL do droplet do relay — não o cluster gerenciado das instâncias. Pool pequeno:
// este serviço faz uma consulta indexada por webhook e nada mais.
var conexao = builder.Configuration.GetConnectionString("ZapDb");
if (!string.IsNullOrWhiteSpace(conexao))
{
    var csb = new Npgsql.NpgsqlConnectionStringBuilder(conexao) { SearchPath = ZapDbContext.Schema };
    var maxPool = builder.Configuration.GetValue<int?>("Db:MaxPoolSize");
    if (maxPool is > 0) csb.MaxPoolSize = maxPool.Value;
    conexao = csb.ConnectionString;
}

builder.Services.AddDbContext<ZapDbContext>(opt =>
    opt.UseNpgsql(conexao, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", ZapDbContext.Schema)));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddZapCore(builder.Configuration);

var timeoutEntrega = builder.Configuration.GetValue("Relay:TimeoutSegundos", 10);
builder.Services.AddHttpClient<IEntregador, Entregador>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(timeoutEntrega <= 0 ? 10 : timeoutEntrega);
});

builder.Services.AddHostedService<LimpezaLogService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/admin/entrar";
        o.LogoutPath = "/admin/sair";
        o.AccessDeniedPath = "/admin/entrar";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.Name = "automais.zap.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization();

builder.Services.AddRazorPages(o =>
{
    // Tudo em /Pages/Admin exige login, menos a própria tela de entrar.
    o.Conventions.AuthorizeFolder("/Admin");
    o.Conventions.AllowAnonymousToPage("/Admin/Entrar");
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<ZapDbContext>();

builder.Services.AddRateLimiter(o =>
{
    // Teto de abuso, não de vazão: folgado o bastante para uma rajada legítima da Meta.
    o.AddFixedWindowLimiter("webhook", opt =>
    {
        opt.PermitLimit = 600;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Roda atrás do nginx no droplet: sem isto, o IP do cliente e o esquema chegam errados.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// Auto-provisionamento com FAIL-FAST: migration que falha derruba o boot. O deploy falha
// alto e o serviço antigo continua no ar — melhor do que subir "saudável" com schema velho.
if (app.Configuration.GetValue("AutoMigrate:Enabled", defaultValue: true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ZapDbContext>();
    try
    {
        app.Logger.LogInformation("Garantindo schema {Schema} e aplicando migrations...", ZapDbContext.Schema);
        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS {ZapDbContext.Schema}");
        await db.Database.MigrateAsync();

        var admin = scope.ServiceProvider.GetRequiredService<IAdminService>();
        await admin.SemearPrimeiroUsuarioAsync(
            app.Configuration["Admin:Email"],
            app.Configuration["Admin:SenhaInicial"]);

        app.Logger.LogInformation("Schema/migrations OK.");
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Falha provisionando o banco no startup ({Tipo}: {Mensagem}). Abortando.",
            ex.GetType().Name, ex.Message);
        throw;
    }
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference("/docs");
app.MapControllers();
app.MapRazorPages();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/admin"));

app.Run();

public partial class Program;
