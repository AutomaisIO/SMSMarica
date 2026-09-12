using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SMSMais.Api.Interno;
using SMSMais.Api.Realtime;
using Serilog;
using SMSMais.Api.Auth;
using SMSMais.Api.Middleware;
using SMSMais.Core;
using SMSMais.Core.Identidade;
using SMSMais.Data;

var builder = WebApplication.CreateBuilder(args);

// O sink de alerta leva todo LogError da plataforma ao celular de quem cuida dela (tela
// Sistema → Avisos no celular). Só enfileira: o log não espera o WhatsApp.
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Sink(new SMSMais.Api.Alertas.AlertaSerilogSink(
        services.GetRequiredService<SMSMais.Core.Alertas.IAlertaPlataforma>())));

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
builder.Services.AddValidatorsFromAssembly(typeof(SMSMais.Core.DependencyInjection).Assembly);

builder.Services.AddData(builder.Configuration);
builder.Services.AddCore(builder.Configuration);

// fo-dicom: registra os codecs nativos (JPEG-LS Lossless) usados pela transcodificação
// do proxy PACS. Idempotente; a flag Pacs:Compressao:Habilitado controla o uso efetivo.
SMSMais.Core.Pacs.PacsDicomSetup.Inicializar();

// Tempo real (TFD): SignalR + notificador concreto (sobrescreve o no-op do Core).
builder.Services.AddSignalR();
builder.Services.AddScoped<SMSMais.Core.Rastreamento.IRastreamentoNotificador, SMSMais.Api.Realtime.RastreamentoNotificadorSignalR>();

// Tempo real (Conversas/chat): notificador concreto (sobrescreve o no-op do Core).
builder.Services.AddScoped<SMSMais.Core.Conversas.IConversaNotificador, SMSMais.Api.Realtime.ConversaNotificadorSignalR>();

// Token JWT do paciente (login CPF + OTP do PWA).
builder.Services.AddScoped<SMSMais.Core.Cidadao.IPacienteTokenService, SMSMais.Api.Auth.PacienteTokenService>();

// Módulo IA: cifragem de segredos (token do provedor, senha das bases) em repouso.
//
// O anel de chaves e o discriminador da aplicação são EXPLÍCITOS por configuração. Sem isso o
// ASP.NET deriva os dois de caminho: o discriminador vem do content root (`/opt/smsmarica/server`)
// e o anel vai para o `$HOME` do usuário do serviço. Mover qualquer um dos dois — que é
// exatamente o que o rename para SMSMais faz — torna TODAS as credenciais de integração gravadas
// no banco indecifráveis, e falha em runtime, não no build.
//
// Ambas as chaves são opcionais: com as duas ausentes o comportamento é idêntico ao implícito,
// para a virada poder ser feita por ambiente, sem redeploy.
var protecaoDados = builder.Services.AddDataProtection();

var chavesProtecao = builder.Configuration["DataProtection:CaminhoChaves"];
if (!string.IsNullOrWhiteSpace(chavesProtecao))
{
    protecaoDados.PersistKeysToFileSystem(new DirectoryInfo(chavesProtecao));
}

var nomeAplicacaoProtecao = builder.Configuration["DataProtection:NomeAplicacao"];
if (!string.IsNullOrWhiteSpace(nomeAplicacaoProtecao))
{
    protecaoDados.SetApplicationName(nomeAplicacaoProtecao);
}
builder.Services.AddScoped<SMSMais.Core.Inteligencia.Seguranca.IProtetorSegredos, SMSMais.Api.Auth.ProtetorSegredos>();

// Agente IA — proxy para o motor Python em 127.0.0.1:5085. Cliente nomeado porque o
// controller repassa JSON cru (o formato é contrato entre o motor e o painel). Timeout
// generoso: criar sessão sobe um processo do Claude Code; o turno em si é assíncrono.
builder.Services.AddHttpClient("agente-ia", c => c.Timeout = TimeSpan.FromSeconds(120));

// Autenticação JWT (token emitido em /identidade/login).
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Secao));

// Proxy SQL interno: porta de loopback + token. Sem token configurado, fica desligado.
builder.Services.Configure<ProxySqlOpcoes>(builder.Configuration.GetSection("ProxySql"));
// Guichê de comandos do robô: mesma porta de loopback do ProxySql, e por padrão o MESMO token —
// se "RoboComando:Porta"/"Token" não forem configurados, cai no "ProxySql:*". Assim não é preciso
// criar porta nem segredo novos: o guichê vive no mesmo endpoint interno.
builder.Services.Configure<RoboComandoOpcoes>(o =>
{
    var proxy = builder.Configuration.GetSection("ProxySql");
    var robo = builder.Configuration.GetSection("RoboComando");
    var porta = robo.GetValue("Porta", 0);
    o.Porta = porta != 0 ? porta : proxy.GetValue("Porta", 0);
    var token = robo.GetValue("Token", string.Empty);
    o.Token = string.IsNullOrWhiteSpace(token) ? proxy.GetValue("Token", string.Empty) : token;
});
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
                    .GetRequiredService<SMSMais.Core.Cidadao.ICidadaoSessaoService>();
                var acesso = await sessoes.ValidarAcessoAsync(sessaoId, pacienteId, ctx.HttpContext.RequestAborted);
                if (!acesso.SessaoValida)
                {
                    ctx.Fail("Sessão encerrada (login em outro dispositivo).");
                    return;
                }

                // Consentimento LGPD: claim lido pelo filtro que bloqueia os endpoints do
                // cidadão enquanto não houver aceite vigente (exceto os marcados como isentos).
                if (ctx.Principal!.Identity is ClaimsIdentity ident)
                {
                    ident.AddClaim(new Claim("consentido", acesso.Consentido ? "true" : "false"));
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
    .AddDbContextCheck<SmsMaisDbContext>(
        name: "db",
        tags: ["ready"]);

// CORS configurável: se "Cors:Origins" estiver definido, restringe a esses origins
// (recomendado em prod). Sem config, mantém permissivo (compat). O canal do agente
// NÃO é browser, então CORS nunca o bloqueia — isto protege só o painel web.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];

// Origens do PWA "Arquivos Saúde Maricá" (endpoints anônimos /anexos/sessao/*).
// Default cobre prod + dev; sobrescrever via Cors__ArquivosPwaOrigins__0..N.
var arquivosPwaOrigins = builder.Configuration.GetSection("Cors:ArquivosPwaOrigins").Get<string[]>()
    ?? ["https://arquivos.smsmarica.online", "http://localhost:5175"];

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
    {
        if (corsOrigins.Length > 0)
            p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();

        // Permite o front ler o nome do arquivo nos downloads (ex.: laudo-...-assinado.pdf).
        p.WithExposedHeaders("Content-Disposition");
    });

    // Política dedicada aos endpoints anônimos do PWA (aplicada via [EnableCors("arquivos-pwa")]).
    o.AddPolicy("arquivos-pwa", p => p
        .WithOrigins(arquivosPwaOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

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

    // Login do cidadão (anônimo por natureza). Barra a varredura de CPF/nº de solicitação e o
    // abuso do envio de OTP — cada tentativa custa um WhatsApp e uma consulta à Receita.
    options.AddPolicy("login-cidadao", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Defesa em profundidade nos endpoints anônimos do PWA de anexos (o token É a
    // autorização). Particiona por IP; barra brute-force de token / abuso de upload.
    options.AddPolicy("anexos-sessao", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// Atrás do nginx (proxy no mesmo host): sem isto, RemoteIpAddress é o loopback do proxy
// (127.0.0.1) e o histórico de acesso do cidadão grava o IP errado. Honra X-Forwarded-For /
// X-Forwarded-Proto vindos SOMENTE do proxy local confiável, resolvendo o IP real do cliente.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Confia só no nginx local (loopback v4/v6); não confiar em qualquer proxy evita spoof do XFF.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);      // 127.0.0.1
    options.KnownProxies.Add(IPAddress.IPv6Loopback);  // ::1
});

var app = builder.Build();

// Discriminador efetivo do Data Protection. É ele que entra na derivação da chave: se mudar,
// nada do que já foi cifrado decifra. Logado no startup para poder ser fixado em configuração
// com o valor exato que a instância usa hoje, sem adivinhação.
{
    var opcoesProtecao = app.Services
        .GetRequiredService<IOptions<DataProtectionOptions>>().Value;
    Log.Information(
        "Data Protection: discriminador={Discriminador} caminhoChaves={CaminhoChaves} contentRoot={ContentRoot}",
        opcoesProtecao.ApplicationDiscriminator,
        string.IsNullOrWhiteSpace(chavesProtecao) ? "(implícito: $HOME do serviço)" : chavesProtecao,
        app.Environment.ContentRootPath);
}

// Primeiro middleware: reescreve RemoteIpAddress a partir do X-Forwarded-For antes de qualquer
// coisa que use o IP (log do Serilog, rate limiter por IP, gravação do acesso do cidadão).
app.UseForwardedHeaders();

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
    // Título neutro de propósito: o Scalar é montado no startup e a identidade da instituição
    // vive no banco (ADR-0043), que pode nem estar preenchido ainda. Página de desenvolvedor
    // não precisa da marca do município — as que o cidadão vê é que precisam.
    options.WithTitle("Automais Saúde — API")
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

// WebSocket para os agentes proxy de SQL (ADR-0023). O handshake do agente é máquina-a-máquina
// (token por fonte), então o endpoint valida por conta própria e é anônimo ao JWT.
app.UseWebSockets();
app.MapAgenteSql();

// Proxy SQL interno (porta de loopback + token) — serviços da própria máquina consultam as
// bases cadastradas sem guardar credencial nem driver. Ver Interno/ProxySqlEndpoint.cs.
app.MapProxySql();
// Guichê de comandos do robô de atendimento (porta de loopback + token). Ver Interno/RoboComandoEndpoint.cs.
app.MapRoboComando();

app.MapControllers();
app.MapHub<SMSMais.Api.Hubs.RastreamentoHub>("/hubs/rastreamento");
app.MapHub<SMSMais.Api.Hubs.ConversasHub>("/hubs/conversas");

// Default false (ADR-0010 / recuperação): evita migration destrutiva acidental no startup.
// Habilitar explicitamente via AutoMigrate__Enabled=true quando for intencional.
var autoMigrate = builder.Configuration.GetValue("AutoMigrate:Enabled", defaultValue: false);
if (autoMigrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
    try
    {
        app.Logger.LogInformation("Aplicando migrations pendentes...");
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Migrations OK.");

        var hasher = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<SMSMais.Data.Entities.Usuario>>();
        var conteudoMarica = builder.Configuration.GetValue("Seeds:ConteudoMarica", false);
        await DbSeeder.SeedAsync(db, hasher, incluirConteudoMarica: conteudoMarica);
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
