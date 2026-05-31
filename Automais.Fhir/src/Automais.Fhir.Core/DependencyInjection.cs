using Microsoft.Extensions.DependencyInjection;
using Automais.Fhir.Core.Patients;
using Automais.Fhir.Core.Practitioners;

namespace Automais.Fhir.Core;

public static class DependencyInjection
{
    /// <summary>Registra os serviços de domínio FHIR (camada Core).</summary>
    public static IServiceCollection AddFhirCore(this IServiceCollection services)
    {
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IPractitionerService, PractitionerService>();
        return services;
    }
}
