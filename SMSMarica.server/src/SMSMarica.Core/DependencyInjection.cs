using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMSMarica.Core.Avaliacoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes;
using SMSMarica.Core.Motoristas;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Rastreamento;
using SMSMarica.Core.Translado;
using SMSMarica.Core.Tratamentos;
using SMSMarica.Core.Unidades;
using SMSMarica.Core.Veiculos;

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
        services.AddScoped<IIdentidadeService, IdentidadeService>();

        var hubBaseUrl = configuration["Integracoes:HubDoDesenvolvedor:BaseUrl"]
            ?? "https://ws.hubdodesenvolvedor.com.br/v2/";
        services
            .AddHttpClient<IHubConsultaService, HubConsultaService>(client =>
            {
                client.BaseAddress = new Uri(hubBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
