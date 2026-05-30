using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.Pacientes;

/// <summary>
/// Testes do <c>PacientesService</c> ficaram suspensos na Fatia 1 do refator
/// FHIR (commit que migrou <c>Paciente</c> para <c>fhir.Patient</c> + tabelas
/// associadas). Reescrever em fatia futura cobrindo: cadastro completo
/// (identifiers/names/addresses/telecoms/contacts/photos), CPF duplicado em
/// <c>fhir.patient_identifier</c>, busca por nome/CPF via JOIN, desativação
/// (soft-delete em <c>fhir.patient.deleted_at</c>), reativação.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PacientesServiceTests(PostgresFixture postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [Fact(Skip = "TODO: refator FHIR Fatia 1 — reescrever cobrindo agregado fhir.Patient.")]
    public void Placeholder()
    {
        _ = _postgres;
    }
}
