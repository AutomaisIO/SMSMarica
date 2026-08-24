namespace SMSMais.Tests.Pacientes;

// Os testes antigos exercitavam o PacientesService sobre a tabela local
// smsmarica.paciente. Após a integração com o hub FHIR (ADR-0010), o serviço
// virou proxy de IPacienteFhirClient e paciente vive só no hub. Testes novos
// devem mockar IPacienteFhirClient (ou usar WebApplicationFactory contra um
// hub de teste). Placeholder até essa fatia.
public class PacientesServiceTests
{
    [Fact(Skip = "Reescrever sobre IPacienteFhirClient (paciente migrou para o hub FHIR — ADR-0010).")]
    public void Placeholder() { }
}
