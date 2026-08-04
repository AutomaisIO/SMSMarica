using Automais.Fhir.Core.Patients;
using Automais.Fhir.Tests.Infraestrutura;
using FluentAssertions;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Automais.Fhir.Tests;

/// <summary>
/// Busca de Patient por identifier de system ARBITRÁRIO (chave local de uma base de origem).
///
/// <para>É o contrato que faltou em 04/08/2026: o conector criava o paciente sem CPF com
/// <c>urn:klinikos:paciente|slug:codigo</c>, e na hora de reencontrá-lo a busca caía no filtro
/// de CPF (fallback do <c>SepararIdentifier</c>) e voltava vazia — cada ciclo incremental
/// criava uma CÓPIA nova. Medido em produção antes da correção: 25 pacientes do caminho
/// sem-CPF do Salux tinham virado 260 recursos.</para>
///
/// <para>Estes testes exercitam o PatientService REAL contra Postgres (Testcontainers) — o
/// exato elo que um fake em memória não cobre, provado pelo incidente: o fake do conector
/// "sabia" buscar por identifier genérico, o hub real não.</para>
/// </summary>
[Collection(nameof(PostgresFhirCollection))]
public class BuscaPorIdentifierGenericoTests(PostgresFhirFixture fixture)
{
    private const string SysLocal = "urn:klinikos:paciente";

    private static Patient PacienteLocal(string chaveLocal, string nome) => new()
    {
        Meta = new Meta
        {
            Source = "https://smsmarica.saude.marica/source/klinikos/upa24h-marica-sqlserver",
            Tag = [new Coding("urn:smsmarica:qualidade", "identidade-incompleta")],
        },
        Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }],
        BirthDate = "2024-01-01",
        Identifier = [new Identifier(SysLocal, chaveLocal)],
        Active = true,
    };

    [Fact]
    public async Task Paciente_sem_CPF_e_reencontrado_pela_chave_local_da_base()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        var chave = $"upa24h-marica-sqlserver:{Guid.NewGuid():N}";

        var criado = await service.CriarAsync(PacienteLocal(chave, "BENTO SEM CPF"));

        var bundle = await service.BuscarAsync(new PatientBusca(
            IdentifierSystem: SysLocal, IdentifierValue: chave));

        bundle.Entry.Should().HaveCount(1,
            "o conector PRECISA reencontrar o paciente que ele mesmo criou — senão duplica a cada ciclo");
        bundle.Entry[0].Resource!.Id.Should().Be(criado.Id);
    }

    [Fact]
    public async Task Chave_local_de_outra_base_nao_casa__o_par_system_value_e_exato()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        var chave = $"upa24h-marica-sqlserver:{Guid.NewGuid():N}";
        await service.CriarAsync(PacienteLocal(chave, "BENTO SEM CPF"));

        // Mesmo VALOR, system diferente: não é a mesma chave — não pode casar.
        var outroSystem = await service.BuscarAsync(new PatientBusca(
            IdentifierSystem: "urn:salux:cd_paciente", IdentifierValue: chave));
        outroSystem.Entry.Should().BeEmpty();

        // Mesmo SYSTEM, valor diferente: idem.
        var outroValor = await service.BuscarAsync(new PatientBusca(
            IdentifierSystem: SysLocal, IdentifierValue: chave + "-x"));
        outroValor.Entry.Should().BeEmpty();
    }

    [Fact]
    public async Task Busca_generica_nao_devolve_excluido()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        var chave = $"upa24h-marica-sqlserver:{Guid.NewGuid():N}";
        var criado = await service.CriarAsync(PacienteLocal(chave, "EXCLUIDO"));
        await service.ExcluirAsync(Guid.Parse(criado.Id!));

        var bundle = await service.BuscarAsync(new PatientBusca(
            IdentifierSystem: SysLocal, IdentifierValue: chave));

        bundle.Entry.Should().BeEmpty("soft-delete tem de valer também no caminho de busca genérica");
    }
}
