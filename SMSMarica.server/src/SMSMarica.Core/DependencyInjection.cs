using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMSMarica.Core.Avaliacoes;
using SMSMarica.Core.EstudoAnotacoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes;
using SMSMarica.Core.Motoristas;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacs;
using SMSMarica.Core.Perfis;
using SMSMarica.Core.Rastreamento;
using SMSMarica.Core.TiposTratamento;
using SMSMarica.Core.Translado;
using SMSMarica.Core.Tratamentos;
using SMSMarica.Core.Unidades;
using SMSMarica.Core.Veiculos;
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
        services.AddScoped<ITransladoService, TransladoService>();
        services.AddScoped<IRastreamentoService, RastreamentoService>();
        services.AddScoped<IAvaliacoesService, AvaliacoesService>();
        services.AddScoped<IEstudoAnotacoesService, EstudoAnotacoesService>();
        services.AddScoped<IIdentidadeService, IdentidadeService>();
        services.AddScoped<IPerfisService, PerfisService>();
        services.AddScoped<ITiposTratamentoService, TiposTratamentoService>();

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

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
