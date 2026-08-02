using Automais.Fhir.Core.Fhir;
using FluentAssertions;
using Hl7.Fhir.Model;

namespace Automais.Fhir.Tests;

/// <summary>
/// Coluna de busca tem de guardar o CÓDIGO do ValueSet, não o nome do enum .NET.
///
/// O bug real: <c>Status.ToString().ToLowerInvariant()</c> gravava <c>"inprogress"</c> para
/// <c>EncounterStatus.InProgress</c>, cujo código FHIR é <c>"in-progress"</c>. Passou
/// despercebido em 1,15 milhão de encounters porque todos eram <c>finished</c> — palavra
/// única, que sobrevive ao ToString por acidente. Quebrou no dia em que a internação
/// (ADR-0025) trouxe o primeiro código com hífen: <c>?status=in-progress</c> não achava nada.
/// </summary>
public class CodigoFhirTests
{
    [Fact]
    public void Codigo_com_hifen_e_preservado()
    {
        CodigoFhir.De(Encounter.EncounterStatus.InProgress).Should().Be("in-progress");
        CodigoFhir.De(Encounter.EncounterStatus.EnteredInError).Should().Be("entered-in-error");
    }

    [Fact]
    public void Codigo_de_palavra_unica_continua_igual()
    {
        CodigoFhir.De(Encounter.EncounterStatus.Finished).Should().Be("finished");
        CodigoFhir.De(Encounter.EncounterStatus.Planned).Should().Be("planned");
        CodigoFhir.De(Encounter.EncounterStatus.Unknown).Should().Be("unknown");
    }

    [Fact]
    public void Nulo_continua_nulo()
    {
        CodigoFhir.De<Encounter.EncounterStatus>(null).Should().BeNull();
        CodigoFhir.De<Location.LocationStatus>(null).Should().BeNull();
    }

    [Fact]
    public void Vale_para_qualquer_ValueSet_nao_so_Encounter()
    {
        CodigoFhir.De(Location.LocationStatus.Active).Should().Be("active");
        CodigoFhir.De(Location.LocationStatus.Suspended).Should().Be("suspended");
        CodigoFhir.De(Location.LocationStatus.Inactive).Should().Be("inactive");
    }

    /// <summary>
    /// A armadilha, travada: o nome do enum e o código FHIR DIVERGEM. Se alguém voltar ao
    /// ToString, este teste é o que quebra.
    /// </summary>
    [Fact]
    public void O_nome_do_enum_nao_serve_como_codigo()
    {
        var nomeDoEnum = Encounter.EncounterStatus.InProgress.ToString().ToLowerInvariant();
        nomeDoEnum.Should().Be("inprogress");
        CodigoFhir.De(Encounter.EncounterStatus.InProgress).Should().NotBe(nomeDoEnum);
    }
}
