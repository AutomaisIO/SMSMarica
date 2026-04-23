using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SMSMarica.Core.Avaliacoes;
using SMSMarica.Core.Identidade;
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
    public static IServiceCollection AddCore(this IServiceCollection services)
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

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
