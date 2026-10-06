using Automais.Fhir.Core.Patients;
using Automais.Fhir.Tests.Infraestrutura;
using FluentAssertions;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Automais.Fhir.Tests;

/// <summary>
/// Busca de paciente por nome com mais de uma palavra.
///
/// <para>Em 06/10/2026 procurar "neide marins" no cadastro não achava NEIDE DE MARINS GOMES: a
/// frase inteira era um só "contém", e o "de" no meio quebrava. A busca passou a exigir cada
/// palavra, em qualquer posição e ordem.</para>
/// </summary>
[Collection(nameof(PostgresFhirCollection))]
public class BuscaPorNomePalavrasTests(PostgresFhirFixture fixture)
{
    /// <summary>
    /// Sobrenome sintético, único por chamada — a bancada é compartilhada entre execuções.
    /// </summary>
    private static string Unico(string prefixo) =>
        prefixo + new string(Enumerable.Range(0, 8).Select(_ => (char)('A' + Random.Shared.Next(26))).ToArray());

    private static Patient Paciente(string nome) => new()
    {
        Meta = new Meta { Source = "https://smsmarica.saude.marica/source/teste" },
        Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }],
        BirthDate = "1947-08-23",
        Active = true,
    };

    [Fact]
    public async Task Termo_com_palavra_no_meio_do_nome_acha_o_paciente()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        string primeiro = Unico("NEIDE"), sobrenome = Unico("MARINS");

        var criado = await service.CriarAsync(Paciente($"{primeiro} DE {sobrenome} GOMES"));

        var porTermo = await service.BuscarAsync(new PatientBusca(Termo: $"{primeiro.ToLowerInvariant()} {sobrenome.ToLowerInvariant()}"));
        var porName = await service.BuscarAsync(new PatientBusca(Nome: $"{primeiro} {sobrenome}"));

        porTermo.Entry.Select(e => e.Resource!.Id).Should().Contain(criado.Id,
            "o \"de\" entre o nome e o sobrenome não pode esconder a pessoa");
        porName.Entry.Select(e => e.Resource!.Id).Should().Contain(criado.Id);
    }

    [Fact]
    public async Task Palavras_em_outra_ordem_tambem_acham()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        string primeiro = Unico("NEIDE"), sobrenome = Unico("MARINS");

        var criado = await service.CriarAsync(Paciente($"{primeiro} {sobrenome}"));

        var bundle = await service.BuscarAsync(new PatientBusca(Termo: $"{sobrenome} {primeiro}"));

        bundle.Entry.Select(e => e.Resource!.Id).Should().Contain(criado.Id);
    }

    [Fact]
    public async Task Todas_as_palavras_sao_exigidas()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        string primeiro = Unico("NEIDE"), sobrenome = Unico("MARINS");

        await service.CriarAsync(Paciente($"{primeiro} DE {sobrenome}"));

        var bundle = await service.BuscarAsync(new PatientBusca(Termo: $"{primeiro} {Unico("OUTRO")}"));

        bundle.Entry.Should().BeEmpty("uma palavra que não está no nome descarta o paciente");
    }

    [Fact]
    public async Task Quem_comeca_pela_frase_inteira_vem_primeiro()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        string primeiro = Unico("NEIDE"), sobrenome = Unico("MARINS");

        // Alfabeticamente "… DE …" viria antes; a frase exata no início tem de vencer.
        await service.CriarAsync(Paciente($"{primeiro} DE {sobrenome}"));
        var exato = await service.CriarAsync(Paciente($"{primeiro} {sobrenome}"));

        var bundle = await service.BuscarAsync(new PatientBusca(Termo: $"{primeiro} {sobrenome}"));

        bundle.Entry.Should().HaveCount(2);
        bundle.Entry[0].Resource!.Id.Should().Be(exato.Id);
    }
}
