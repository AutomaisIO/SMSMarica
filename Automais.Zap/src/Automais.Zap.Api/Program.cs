using System.Threading.RateLimiting;
using Automais.Zap.Api.Infra;
using Automais.Zap.Core;
using Automais.Zap.Core.Admin;
using Automais.Zap.Core.Entregas;
using Automais.Zap.Core.Envio;
using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Relay;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Automais.Zap.Api.Infra.EscopoUsuario>();
builder.Services.AddZapCore(builder.Configuration);

var timeoutEntrega = builder.Configuration.GetValue("Relay:TimeoutSegundos", 10);
builder.Services.AddHttpClient<IEntregador, Entregador>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(timeoutEntrega <= 0 ? 10 : timeoutEntrega);
});

// Cliente da Graph API para o console de gestao. Timeout maior que o da entrega: a Meta
// demora mais em criar template do que em aceitar um POST, e nada aqui esta no caminho do
// webhook -- se travar, o relay continua entregando.
builder.Services.AddHttpClient<IGraphMetaClient, GraphMetaClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});

// Cliente de envio para a Meta. Separado do da gestao: timeout curto, porque quem chama
// esta esperando o wamid na resposta.
builder.Services.AddHttpClient<IEnvioService, EnvioService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddHostedService<LimpezaLogService>();

// Chaves do Data Protection (cifram o cookie do admin) FORA de /opt/automais-zap/api: o
// deploy esvazia aquele diretório, e chave nova a cada deploy desloga todo mundo. Caminho
// explícito porque depender do $HOME do usuário de sistema é acidente esperando acontecer.
var caminhoChaves = builder.Configuration["DataProtection:CaminhoChaves"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Automais.Zap");
if (!string.IsNullOrWhiteSpace(caminhoChaves))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(caminhoChaves));
}
else if (builder.Environment.IsProduction())
{
    // Sem anel persistido, cada restart gera chave nova e TODO segredo gravado (App Secret,
    // token do System User, segredos de entrega) vira indecifravel em silencio. Falhar alto.
    throw new InvalidOperationException("DataProtection:CaminhoChaves e obrigatorio em producao.");
}

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

        // O cookie vale 8h — desativar um operador precisa valer antes disso. A cada request
        // (com cache de 60s) confere no banco se o usuário ainda existe e está ativo; claims
        // sozinhas transformariam demissão em "acesso por mais um turno".
        o.Events = new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents
        {
            OnValidatePrincipal = async ctx =>
            {
                var bruto = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var servicos = ctx.HttpContext.RequestServices;
                var valido = Guid.TryParse(bruto, out var usuarioId)
                    && await servicos.GetRequiredService<IMemoryCache>()
                        .GetOrCreateAsync($"admin-ativo:{usuarioId}", async entrada =>
                        {
                            entrada.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                            var db = servicos.GetRequiredService<ZapDbContext>();
                            return await db.UsuariosAdmin.AsNoTracking()
                                .AnyAsync(u => u.Id == usuarioId && u.Ativo);
                        });

                if (!valido)
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            },
        };
    });

// Politica "Global": so quem e da casa. Aplicada por PASTA logo abaixo, para que a protecao
// venha de ONDE o arquivo esta e nao de o desenvolvedor lembrar de checar. Foi assim que
// /admin/meta nasceu so com o link escondido e a pagina aberta a quem soubesse a URL.
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(EscopoUsuario.PoliticaGlobal, p =>
        p.RequireAuthenticatedUser().RequireClaim(EscopoUsuario.ClaimGlobal, "1"));
});

builder.Services.AddRazorPages(o =>
{
    // Tudo em /Pages/Admin exige login, menos a própria tela de entrar.
    o.Conventions.AuthorizeFolder("/Admin");
    o.Conventions.AllowAnonymousToPage("/Admin/Entrar");

    // /Pages/Admin/Plataforma é a area da casa: credenciais do App, criacao de tenant.
    // Pagina nova ali dentro ja nasce protegida, sem depender de checagem manual. (A pasta
    // nao pode se chamar "Automais": colidiria com o namespace raiz Automais.Zap.)
    o.Conventions.AuthorizeFolder("/Admin/Plataforma", EscopoUsuario.PoliticaGlobal);
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<ZapDbContext>();

builder.Services.AddRateLimiter(o =>
{
    // Duas politicas, de proposito. Uma so e global permitiria a qualquer anonimo esgotar a cota
    // do webhook da Meta e derrubar o inbound de TODOS os tenants com 600 POSTs.
    //
    // Webhook da Meta: so a Meta chama e a origem e autenticada por HMAC; teto alto, global.
    o.AddFixedWindowLimiter("meta-webhook", opt =>
    {
        opt.PermitLimit = 6000;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    // API publica (/v1/*): particionada por IP de origem, para um cliente nao afetar o outro.
    o.AddPolicy("api-publica", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // Tela de login: 10 tentativas/min por IP. Sem isto, forca bruta online e ilimitada.
    o.AddPolicy("entrar", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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

// wwwroot: logo do grupo, favicon e nada mais. O painel continua server-rendered.
app.UseStaticFiles();

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
        await admin.SemearPrimeiroUsuarioAsync();

        app.Logger.LogInformation("Schema/migrations OK.");
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Falha provisionando o banco no startup ({Tipo}: {Mensagem}). Abortando.",
            ex.GetType().Name, ex.Message);
        throw;
    }
}

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
