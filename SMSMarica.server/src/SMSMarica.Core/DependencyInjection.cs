using FluentValidation;
using Ganss.Xss;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using SMSMarica.Core.Avaliacoes;
using SMSMarica.Core.EstudoAnotacoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes;
using SMSMarica.Core.Laudos;
using SMSMarica.Core.Laudos.Pdf;
using SMSMarica.Core.LaudoTemplates;
using SMSMarica.Core.Medicos;
using SMSMarica.Core.Motoristas;
using SMSMarica.Core.Notificacoes;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacs;
using SMSMarica.Core.Perfis;
using SMSMarica.Core.Procedimentos;
using SMSMarica.Core.Rastreamento;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Core.TiposExame;
using SMSMarica.Core.TiposTratamento;
using SMSMarica.Core.Translado;
using SMSMarica.Core.Tratamentos;
using SMSMarica.Core.Unidades;
using SMSMarica.Core.Veiculos;
using SMSMarica.Core.Worklist;
using SMSMarica.Core.Worklist.Background;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPacientesService, PacientesService>();
        services.AddScoped<ITratamentosService, TratamentosService>();
        services.AddScoped<IUnidadesService, UnidadesService>();
        services.AddScoped<IVeiculosService, VeiculosService>();
        services.AddScoped<IMotoristasService, MotoristasService>();
        services.AddScoped<IMedicosService, MedicosService>();
        services.AddScoped<ITransladoService, TransladoService>();
        services.AddScoped<IRastreamentoService, RastreamentoService>();
        services.AddScoped<IAvaliacoesService, AvaliacoesService>();
        services.AddScoped<IEstudoAnotacoesService, EstudoAnotacoesService>();
        services.AddScoped<IIdentidadeService, IdentidadeService>();
        services.AddScoped<IPerfisService, PerfisService>();
        services.AddScoped<ITiposTratamentoService, TiposTratamentoService>();
        services.AddScoped<ILaudoTemplatesService, LaudoTemplatesService>();
        services.AddScoped<ILaudosService, LaudosService>();
        services.AddScoped<ILaudoPdfRenderer, LaudoPdfRenderer>();

        // ---- Solicitação de Exames + Worklist + Notificações ----
        services.AddScoped<IProcedimentosSigtapService, ProcedimentosSigtapService>();
        services.AddScoped<ITiposExameService, TiposExameService>();
        services.AddScoped<ISolicitacoesExameService, SolicitacoesExameService>();
        services.AddScoped<IGeradorIdentificadores, GeradorIdentificadores>();
        services.AddScoped<INotificadorExame, NotificadorExameLog>();

        services.Configure<Dcm4cheeUpsOptions>(configuration.GetSection(Dcm4cheeUpsOptions.SecaoConfig));
        var upsBaseUrl = configuration["Pacs:Dcm4chee:UpsBaseUrl"]
            ?? "http://pacs.marica.automais.cloud:8080/dcm4chee-arc/aets/WORKLIST/rs/";
        services
            .AddHttpClient<IDcm4cheeUpsClient, Dcm4cheeUpsClient>(client =>
            {
                client.BaseAddress = new Uri(upsBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });

        services
            .AddHttpClient<IConsultaStudyClient, ConsultaStudyClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Pacs:Dcm4chee:RsBaseUrl"]
                    ?? "http://pacs.marica.automais.cloud:8080/dcm4chee-arc/aets/DCM4CHEE/rs/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });

        services.Configure<SincronizadorExamesOptions>(configuration.GetSection(SincronizadorExamesOptions.SecaoConfig));
        services.AddHostedService<SincronizadorExamesService>();

        // Sanitizador de HTML compartilhado (whitelist explícita das tags TipTap).
        services.AddSingleton<IHtmlSanitizer>(_ =>
        {
            var s = new HtmlSanitizer();
            s.AllowedTags.Clear();
            foreach (var tag in new[] { "p", "br", "h1", "h2", "h3", "ul", "ol", "li",
                                        "strong", "b", "em", "i", "u",
                                        "table", "thead", "tbody", "tr", "th", "td" })
            {
                s.AllowedTags.Add(tag);
            }
            s.AllowedAttributes.Clear();
            s.AllowedAttributes.Add("colspan");
            s.AllowedAttributes.Add("rowspan");
            return s;
        });

        services.Configure<LaudosPdfOptions>(configuration.GetSection(LaudosPdfOptions.SecaoConfig));
        QuestPDF.Settings.License = LicenseType.Community;

        // PasswordHasher do ASP.NET Identity (PBKDF2-HMAC-SHA512 / 100k iterações).
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

        var hubBaseUrl = configuration["Integracoes:HubDoDesenvolvedor:BaseUrl"]
            ?? "https://ws.hubdodesenvolvedor.com.br/v2/";
        services
            .AddHttpClient<IHubConsultaService, HubConsultaService>(client =>
            {
                client.BaseAddress = new Uri(hubBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });

        var pacsBaseUrl = configuration["Pacs:Dcm4chee:RsBaseUrl"]
            ?? "http://pacs.marica.automais.cloud:8080/dcm4chee-arc/aets/DCM4CHEE/rs/";
        services
            .AddHttpClient<IPacsProxyService, PacsProxyService>(client =>
            {
                client.BaseAddress = new Uri(pacsBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        // Cliente do hub FHIR (Automais.Fhir) — paciente vive só no hub.
        var fhirBaseUrl = configuration["Fhir:BaseUrl"] ?? "http://localhost:5081/";
        services
            .AddHttpClient<Pacientes.Fhir.IPacienteFhirClient, Pacientes.Fhir.PacienteFhirClient>(client =>
            {
                client.BaseAddress = new Uri(fhirBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
