using Automais.Fhir.Core.Encounters;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Tests.Infraestrutura;
using FluentAssertions;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace Automais.Fhir.Tests;

/// <summary>
/// A guarda de no-op (<see cref="EscritaFhir.SemMudanca{T}"/>): PUT que não muda nada não vira
/// versão nova.
///
/// <para>Existe porque os conectores de PEP releem um bloco fixo de registros a cada ciclo <b>de
/// propósito</b> — internação em curso é re-lida todo poll (ADR-0025) e o cadastro de médicos é
/// re-scan integral. Medido em produção 06/08/2026: pacientes internados no HMCML em
/// <c>version_id</c> 226, reescritos idênticos a cada 11 minutos.</para>
/// </summary>
public class EscritaSemMudancaComparacaoTests
{
    private const string Source = "https://smsmarica.saude.marica/source/salux/salux-hcml";

    private static Encounter Exemplo() => new()
    {
        Id = "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
        Meta = new Meta { Source = Source, VersionId = "7", LastUpdated = DateTimeOffset.UtcNow },
        Status = Encounter.EncounterStatus.InProgress,
        Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "IMP", "inpatient encounter"),
        Identifier = [new Identifier(FhirSystems.SaluxFia, "salux-hcml:1-2026-4242")],
        Subject = new ResourceReference("Patient/9c858901-8a57-4791-81fe-4c455b099bc9"),
        Period = new Period { Start = "2026-07-20T10:00:00-03:00" },
    };

    [Fact]
    public void Recurso_igual_ao_guardado_e_no_op__mesmo_com_versao_e_lastUpdated_diferentes()
    {
        var guardado = FhirJson.Serialize(Exemplo());

        var novo = Exemplo();
        novo.Meta!.VersionId = "226";                                  // o hub carimba a cada escrita
        novo.Meta.LastUpdated = DateTimeOffset.UtcNow.AddHours(3);     // idem

        EscritaFhir.SemMudanca(novo, guardado).Should().BeTrue();
    }

    [Fact]
    public void Comparar_nao_pode_estragar_o_recurso__versionId_e_lastUpdated_voltam()
    {
        var guardado = FhirJson.Serialize(Exemplo());
        var novo = Exemplo();
        var versao = novo.Meta!.VersionId;
        var atualizado = novo.Meta.LastUpdated;

        EscritaFhir.SemMudanca(novo, guardado);

        // O versionId é o If-Match de quem chamou: zerá-lo em definitivo desligaria a
        // concorrência otimista em silêncio.
        novo.Meta.VersionId.Should().Be(versao);
        novo.Meta.LastUpdated.Should().Be(atualizado);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("source")]
    [InlineData("periodo")]
    [InlineData("identifier")]
    public void Qualquer_mudanca_de_conteudo_derruba_a_guarda(string campo)
    {
        var guardado = FhirJson.Serialize(Exemplo());
        var novo = Exemplo();

        switch (campo)
        {
            case "status": novo.Status = Encounter.EncounterStatus.Finished; break;
            // Recurso que passou a ser visto por OUTRA base mudou — meta.source participa.
            case "source": novo.Meta!.Source = "https://smsmarica.saude.marica/source/klinikos"; break;
            case "periodo": novo.Period!.End = "2026-07-25T09:00:00-03:00"; break;
            case "identifier": novo.Identifier.Add(new Identifier(FhirSystems.SaluxBaa, "salux-hcml:1-2026-9")); break;
        }

        EscritaFhir.SemMudanca(novo, guardado).Should().BeFalse();
    }

    [Fact]
    public void Conteudo_ilegivel_no_banco_manda_escrever__nunca_engole_a_atualizacao()
    {
        EscritaFhir.SemMudanca(Exemplo(), "{ isto não é FHIR").Should().BeFalse();
        EscritaFhir.SemMudanca(Exemplo(), "{}").Should().BeFalse();
    }
}

/// <summary>O efeito da guarda onde ele importa: no banco.</summary>
[Collection(nameof(PostgresFhirCollection))]
public class EscritaSemMudancaNoBancoTests(PostgresFhirFixture fixture)
{
    private const string Source = "https://smsmarica.saude.marica/source/salux/salux-hcml";

    private static Encounter Internacao(string chave, Encounter.EncounterStatus status) => new()
    {
        Meta = new Meta { Source = Source },
        Status = status,
        Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "IMP", "inpatient encounter"),
        Identifier = [new Identifier(FhirSystems.SaluxFia, chave)],
        Period = new Period { Start = "2026-07-20T10:00:00-03:00" },
    };

    /// <summary>
    /// O caso real: a mesma internação em curso re-lida ciclo após ciclo. Antes da guarda, cada
    /// releitura era uma versão nova e um <c>lastUpdated</c> novo.
    /// </summary>
    [Fact]
    public async Task Releitura_identica_nao_bumpa_versao_nem_lastUpdated()
    {
        await using var db = fixture.CriarContexto();
        var service = new EncounterService(db, TimeProvider.System);
        var chave = $"salux-hcml:1-2026-{Guid.NewGuid():N}";

        var criado = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave,
            Internacao(chave, Encounter.EncounterStatus.InProgress));
        var linhaInicial = await db.Encounters.AsNoTracking().SingleAsync(e => e.IdentifierValue == chave);

        // Dez ciclos relendo exatamente a mesma internação.
        for (var ciclo = 0; ciclo < 10; ciclo++)
            await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave,
                Internacao(chave, Encounter.EncounterStatus.InProgress));

        var linhaFinal = await db.Encounters.AsNoTracking().SingleAsync(e => e.IdentifierValue == chave);
        linhaFinal.VersionId.Should().Be(1, "dez releituras idênticas não são dez versões");
        linhaFinal.LastUpdated.Should().Be(linhaInicial.LastUpdated, "nada mudou — o carimbo de tempo mentiria");
        linhaFinal.Id.Should().Be(Guid.Parse(criado.Id!));
    }

    /// <summary>
    /// A guarda devolve a versão VIGENTE. Se devolvesse a incrementada (que não existe no banco),
    /// o If-Match seguinte do chamador daria 409 para sempre — o conector entraria em loop de
    /// re-leitura e o recurso nunca mais seria atualizado.
    /// </summary>
    [Fact]
    public async Task No_op_devolve_a_versao_vigente__o_If_Match_seguinte_continua_valendo()
    {
        await using var db = fixture.CriarContexto();
        var service = new EncounterService(db, TimeProvider.System);
        var chave = $"salux-hcml:1-2026-{Guid.NewGuid():N}";

        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave,
            Internacao(chave, Encounter.EncounterStatus.InProgress));
        var noOp = await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave,
            Internacao(chave, Encounter.EncounterStatus.InProgress));

        noOp.Meta!.VersionId.Should().Be("1");

        // E o If-Match com essa versão é aceito: a alta passa.
        var alta = Internacao(chave, Encounter.EncounterStatus.Finished);
        alta.Period!.End = "2026-07-25T09:00:00-03:00";
        var atualizado = await service.AtualizarAsync(Guid.Parse(noOp.Id!), alta, versaoEsperada: 1);

        atualizado.Meta!.VersionId.Should().Be("2");
    }

    /// <summary>A guarda não pode virar cegueira: a alta continua entrando, uma vez só.</summary>
    [Fact]
    public async Task Mudanca_real_ainda_escreve_e_conta_UMA_versao()
    {
        await using var db = fixture.CriarContexto();
        var service = new EncounterService(db, TimeProvider.System);
        var chave = $"salux-hcml:1-2026-{Guid.NewGuid():N}";

        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, Internacao(chave, Encounter.EncounterStatus.InProgress));
        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, Internacao(chave, Encounter.EncounterStatus.InProgress));

        var alta = Internacao(chave, Encounter.EncounterStatus.Finished);
        alta.Period!.End = "2026-07-25T09:00:00-03:00";
        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, alta);

        // …e mais ciclos depois da alta, todos no-op.
        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, alta);
        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, alta);

        var linha = await db.Encounters.AsNoTracking().SingleAsync(e => e.IdentifierValue == chave);
        linha.VersionId.Should().Be(2, "só a alta mudou o recurso");
        linha.Status.Should().Be("finished");
        linha.PeriodEnd.Should().NotBeNull("a coluna de busca acompanha a alta");
    }

    /// <summary>
    /// Coluna de busca dessincronizada do content (backfill parcial) é consertada mesmo quando o
    /// content não muda — era a reescrita que vinha fazendo isso em silêncio, e a guarda não pode
    /// congelar a correção junto.
    /// </summary>
    [Fact]
    public async Task Coluna_de_busca_dessincronizada_e_corrigida_sem_criar_versao_nova()
    {
        await using var db = fixture.CriarContexto();
        var service = new EncounterService(db, TimeProvider.System);
        var chave = $"salux-hcml:1-2026-{Guid.NewGuid():N}";

        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, Internacao(chave, Encounter.EncounterStatus.InProgress));

        // Simula a deriva: a coluna de busca mente, o content está certo.
        var linha = await db.Encounters.SingleAsync(e => e.IdentifierValue == chave);
        linha.Status = "entered-in-error";
        await db.SaveChangesAsync();
        var versaoAntes = linha.VersionId;

        await service.UpsertPorIdentifierAsync(FhirSystems.SaluxFia, chave, Internacao(chave, Encounter.EncounterStatus.InProgress));

        db.ChangeTracker.Clear();
        var depois = await db.Encounters.AsNoTracking().SingleAsync(e => e.IdentifierValue == chave);
        depois.Status.Should().Be("in-progress", "a coluna de busca voltou a refletir o content");
        depois.VersionId.Should().Be(versaoAntes, "corrigir coluna derivada não é uma versão nova do recurso");
    }
}
