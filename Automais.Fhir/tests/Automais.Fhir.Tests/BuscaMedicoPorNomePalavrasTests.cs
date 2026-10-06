using Automais.Fhir.Core.Practitioners;
using Automais.Fhir.Tests.Infraestrutura;
using FluentAssertions;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Automais.Fhir.Tests;

/// <summary>
/// Busca de profissional por nome com mais de uma palavra — o mesmo defeito do paciente
/// (<see cref="BuscaPorNomePalavrasTests"/>): a frase inteira era um só "contém", e "jose silva"
/// não achava JOSÉ DA SILVA. Aqui também passou a ignorar acento.
/// </summary>
[Collection(nameof(PostgresFhirCollection))]
public class BuscaMedicoPorNomePalavrasTests(PostgresFhirFixture fixture)
{
    /// <summary>Sobrenome sintético, único por chamada — a bancada é compartilhada.</summary>
    private static string Unico(string prefixo) =>
        prefixo + new string(Enumerable.Range(0, 8).Select(_ => (char)('A' + Random.Shared.Next(26))).ToArray());

    private static Practitioner Medico(string nome) => new()
    {
        Meta = new Meta { Source = "https://smsmarica.saude.marica/source/teste" },
        Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }],
        Active = true,
    };

    [Fact]
    public async Task Palavras_soltas_sem_acento_acham_o_medico()
    {
        await using var db = fixture.CriarContexto();
        var service = new PractitionerService(db, TimeProvider.System);
        var sobrenome = Unico("SILVA");

        var criado = await service.CriarAsync(Medico($"JOSÉ DA {sobrenome}"));

        var bundle = await service.BuscarAsync(new PractitionerBusca(Nome: $"{sobrenome.ToLowerInvariant()} jose"));

        bundle.Entry.Select(e => e.Resource!.Id).Should().Contain(criado.Id,
            "o \"da\" no meio, a ordem invertida e o acento não podem esconder o médico");
    }

    [Fact]
    public async Task Todas_as_palavras_sao_exigidas()
    {
        await using var db = fixture.CriarContexto();
        var service = new PractitionerService(db, TimeProvider.System);
        var sobrenome = Unico("SILVA");

        await service.CriarAsync(Medico($"JOSE DA {sobrenome}"));

        var bundle = await service.BuscarAsync(new PractitionerBusca(Nome: $"{sobrenome} {Unico("OUTRO")}"));

        bundle.Entry.Should().BeEmpty();
    }
}
