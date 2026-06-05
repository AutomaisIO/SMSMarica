using Microsoft.Extensions.DependencyInjection;
using Automais.Fhir.Core.Conditions;
using Automais.Fhir.Core.DocumentReferences;
using Automais.Fhir.Core.Encounters;
using Automais.Fhir.Core.MedicationAdministrations;
using Automais.Fhir.Core.MedicationRequests;
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
        services.AddScoped<IEncounterService, EncounterService>();
        services.AddScoped<IConditionService, ConditionService>();
        services.AddScoped<IDocumentReferenceService, DocumentReferenceService>();
        services.AddScoped<IMedicationRequestService, MedicationRequestService>();
        services.AddScoped<IMedicationAdministrationService, MedicationAdministrationService>();
        return services;
    }
}
