using FluentAssertions;
using Hl7.Fhir.Model;
using Automais.Fhir.Core.Fhir;

namespace Automais.Fhir.Tests;

public class FhirJsonTests
{
    [Fact]
    public void Serializa_e_faz_parse_preservando_identifiers_e_nome()
    {
        var original = new Patient
        {
            Identifier =
            [
                new Identifier(FhirSystems.Cpf, "12345678900"),
                new Identifier(FhirSystems.Cns, "700000000000000"),
            ],
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "Maria da Silva" }],
            BirthDate = "1980-05-01",
            Gender = AdministrativeGender.Female,
        };

        var json = FhirJson.Serialize(original);
        var roundtrip = FhirJson.Parse<Patient>(json);

        json.Should().Contain("\"resourceType\":\"Patient\"");
        roundtrip.Identifier.Should().ContainSingle(i => i.System == FhirSystems.Cpf && i.Value == "12345678900");
        roundtrip.Identifier.Should().ContainSingle(i => i.System == FhirSystems.Cns && i.Value == "700000000000000");
        roundtrip.Name[0].Text.Should().Be("Maria da Silva");
        roundtrip.BirthDate.Should().Be("1980-05-01");
        roundtrip.Gender.Should().Be(AdministrativeGender.Female);
    }
}
