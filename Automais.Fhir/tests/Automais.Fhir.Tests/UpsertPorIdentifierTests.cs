using FluentAssertions;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Encounters;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Core.Locations;
using Automais.Fhir.Tests.Infraestrutura;
using Task = System.Threading.Tasks.Task;

namespace Automais.Fhir.Tests;

/// <summary>
/// Conditional update por identifier (ADR-0024): é o que torna o importador idempotente —
/// reimportar o mesmo BAA/FIA atualiza a mesma linha (id lógico estável) em vez de duplicar.
/// </summary>
[Collection(nameof(PostgresFhirCollection))]
public class UpsertPorIdentifierTests(PostgresFhirFixture fixture)
{
    private const string Source = "https://smsmarica.saude.marica/source/salux/salux-hcml";

    private static Encounter NovoEncounter(string chave, Encounter.EncounterStatus status) => new()
    {
        Meta = new Meta { Source = Source },
        Status = status,
        Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "IMP", "inpatient encounter"),
        Identifier = [new Identifier(FhirSystems.SaluxFia, chave)],
        Period = new Period { Start = "2026-07-20T10:00:00-03:00" },
    };

    [Fact]
    public async Task Upsert_cria_na_primeira_vez_e_atualiza_na_segunda_preservando_o_id()
    {
        await using var db = fixture.CriarContexto();
        var service = new EncounterService(db, TimeProvider.System);
        var chave = $"salux-hcml:1-2026-{Guid.NewGuid():N}";

        var criado = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave,
            NovoEncounter(chave, Encounter.EncounterStatus.InProgress));

        // Alta: o mesmo identifier vira finished SEM trocar o id lógico (timeline estável).
        var alta = NovoEncounter(chave, Encounter.EncounterStatus.Finished);
        alta.Period!.End = "2026-07-25T09:00:00-03:00";
        var atualizado = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, alta);

        atualizado.Id.Should().Be(criado.Id);
        atualizado.Meta!.VersionId.Should().Be("2");

        var linhas = await db.Encounters.AsNoTracking()
            .Where(e => e.IdentifierValue == chave && !e.IsDeleted).ToListAsync();
        linhas.Should().ContainSingle();
        linhas[0].Status.Should().Be("finished");
        linhas[0].PeriodEnd.Should().NotBeNull("altas do período dependem da coluna period_end");
    }

    [Fact]
    public async Task Identifiers_diferentes_geram_linhas_diferentes()
    {
        await using var db = fixture.CriarContexto();
        var service = new EncounterService(db, TimeProvider.System);
        var a = $"salux-hcml:1-2026-{Guid.NewGuid():N}";
        var b = $"salux-hcml:1-2026-{Guid.NewGuid():N}";

        var encA = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, a, NovoEncounter(a, Encounter.EncounterStatus.InProgress));
        var encB = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, b, NovoEncounter(b, Encounter.EncounterStatus.InProgress));

        encA.Id.Should().NotBe(encB.Id);
    }

    [Fact]
    public async Task Linha_excluida_nao_colide_no_indice_e_o_upsert_recria()
    {
        await using var db = fixture.CriarContexto();
        var service = new EncounterService(db, TimeProvider.System);
        var chave = $"salux-hcml:1-2026-{Guid.NewGuid():N}";

        var criado = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, NovoEncounter(chave, Encounter.EncounterStatus.InProgress));
        await service.ExcluirAsync(Guid.Parse(criado.Id!));

        var recriado = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, NovoEncounter(chave, Encounter.EncounterStatus.Finished));

        recriado.Id.Should().NotBe(criado.Id, "a linha viva é nova; a excluída fica como tombstone");
    }

    [Fact]
    public async Task Busca_por_identifier_devolve_no_maximo_um()
    {
        await using var db = fixture.CriarContexto();
        var service = new EncounterService(db, TimeProvider.System);
        var chave = $"salux-hcml:1-2026-{Guid.NewGuid():N}";
        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, NovoEncounter(chave, Encounter.EncounterStatus.InProgress));

        var bundle = await service.BuscarAsync(new EncounterBusca(IdentifierSystem: FhirSystems.SaluxFia, IdentifierValue: chave));

        bundle.Entry.Should().ContainSingle();
    }

    [Fact]
    public async Task Location_upsert_monta_hierarquia_por_part_of()
    {
        await using var db = fixture.CriarContexto();
        var service = new LocationService(db, TimeProvider.System);
        var sufixo = Guid.NewGuid().ToString("N");

        var setor = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxUnidade, $"salux-hcml:1-7-{sufixo}", new Location
        {
            Meta = new Meta { Source = Source },
            Name = "CLINICA MEDICA",
            Status = Location.LocationStatus.Active,
            Mode = Location.LocationMode.Instance,
            PhysicalType = new CodeableConcept("http://terminology.hl7.org/CodeSystem/location-physical-type", "wa"),
        });

        var leito = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxLeito, $"salux-hcml:1-7-2-B-{sufixo}", new Location
        {
            Meta = new Meta { Source = Source },
            Name = "Leito B",
            Status = Location.LocationStatus.Active,
            Mode = Location.LocationMode.Instance,
            PhysicalType = new CodeableConcept("http://terminology.hl7.org/CodeSystem/location-physical-type", "bd"),
            PartOf = new ResourceReference($"Location/{setor.Id}"),
        });

        var filhos = await service.BuscarAsync(new LocationBusca(PartOfId: Guid.Parse(setor.Id!)));
        filhos.Entry.Should().ContainSingle(e => e.Resource!.Id == leito.Id);

        // Upsert repetido do mesmo leito não duplica.
        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxLeito, $"salux-hcml:1-7-2-B-{sufixo}", new Location
        {
            Meta = new Meta { Source = Source },
            Name = "Leito B",
            Status = Location.LocationStatus.Suspended,
            Mode = Location.LocationMode.Instance,
            PhysicalType = new CodeableConcept("http://terminology.hl7.org/CodeSystem/location-physical-type", "bd"),
            PartOf = new ResourceReference($"Location/{setor.Id}"),
        });
        var linhas = await db.Locations.AsNoTracking()
            .Where(l => l.IdentifierValue == $"salux-hcml:1-7-2-B-{sufixo}" && !l.IsDeleted).ToListAsync();
        linhas.Should().ContainSingle();
        linhas[0].Status.Should().Be("suspended");
    }
}
