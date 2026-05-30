using Microsoft.Extensions.DependencyInjection;
using Automais.Fhir.Core.Patients;

namespace Automais.Fhir.Core;

public static class DependencyInjection
{
    /// <summary>Registra os serviços de domínio FHIR (camada Core).</summary>
    public static IServiceCollection AddFhirCore(this IServiceCollection services)
    {
        services.AddScoped<IPatientService, PatientService>();
        return services;
    }
}
