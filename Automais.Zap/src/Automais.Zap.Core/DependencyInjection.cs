using Automais.Zap.Core.Admin;
using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Relay;
using Automais.Zap.Core.Roteamento;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Automais.Zap.Core;

public static class DependencyInjection
{
    /// <summary>
    /// Registra o Core. O <c>IEntregador</c> fica de fora de propósito: é um typed HttpClient,
    /// registrado na Api junto com o timeout de <see cref="RelayOptions"/>.
    /// </summary>
    public static IServiceCollection AddZapCore(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<MetaOptions>(config.GetSection(MetaOptions.Secao));
        services.Configure<RelayOptions>(config.GetSection(RelayOptions.Secao));

        services.AddScoped<IRoteador, Roteador>();
        services.AddScoped<IRelayService, RelayService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}
