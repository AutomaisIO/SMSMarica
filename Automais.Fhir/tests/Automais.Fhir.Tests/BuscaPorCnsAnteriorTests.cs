using Automais.Fhir.Core.Patients;
using Automais.Fhir.Tests.Infraestrutura;
using FluentAssertions;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Automais.Fhir.Tests;

/// <summary>
/// Busca de Patient por um CNS que já não é o oficial.
///
/// <para><b>Por que uma pessoa tem mais de um CNS.</b> O provisório (faixa 898…) é substituído
/// pelo definitivo quando o cadastro se regulariza, e cadastros feitos em lugares diferentes ao
/// longo dos anos geram outros. Medido na implantação do histórico do SISREG em 07/09/2026: 3.815
/// conversões provisório→definitivo e 577 pessoas com dois CNS definitivos, só entre os pacientes
/// que aquela carga trouxe. Em produção, 1.615 fichas já tinham mais de um.</para>
///
/// <para><b>Por que isso é o mesmo defeito de 04/08/2026</b> (ver
/// <see cref="BuscaPorIdentifierGenericoTests"/>, onde 25 pacientes viraram 260 recursos): quando
/// a busca não encontra pelo identificador que o chamador tem em mãos, o chamador conclui que a
/// pessoa não existe e <b>cria outra</b>. Aqui o gatilho é o número antigo — toda solicitação,
/// exame e laudo já gravados apontam para ele, e a próxima carga do SISREG pode trazê-lo.</para>
/// </summary>
[Collection(nameof(PostgresFhirCollection))]
public class BuscaPorCnsAnteriorTests(PostgresFhirFixture fixture)
{
    private const string SysCns = "https://fhir.saude.gov.br/sid/cns";

    /// <summary>
    /// CNS sintético de 15 dígitos, único por chamada — a bancada é compartilhada entre execuções,
    /// então valor fixo faria um teste enxergar o resíduo do outro.
    /// </summary>
    private static string Cns(string prefixo)
    {
        var corpo = Random.Shared.NextInt64(0, 1_000_000_000_000_000).ToString("D15");
        return prefixo + corpo[prefixo.Length..];
    }

    private static Patient ComDoisCns(string definitivo, string anterior, string nome) => new()
    {
        Meta = new Meta { Source = "https://smsmarica.saude.marica/source/teste" },
        Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }],
        BirthDate = "1980-05-10",
        Identifier =
        [
            new Identifier(SysCns, definitivo) { Use = Identifier.IdentifierUse.Official },
            new Identifier(SysCns, anterior) { Use = Identifier.IdentifierUse.Old },
        ],
        Active = true,
    };

    /// <summary>
    /// O paciente é encontrado pelo CNS ANTERIOR — o caso que criava duplicata.
    /// </summary>
    [Fact]
    public async Task Paciente_e_encontrado_pelo_CNS_que_ja_nao_e_o_oficial()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        string definitivo = Cns("70"), anterior = Cns("898");

        var criado = await service.CriarAsync(ComDoisCns(definitivo, anterior, "MARIA DOIS CNS"));

        var pelaAntiga = await service.BuscarAsync(new PatientBusca(Cns: anterior));

        pelaAntiga.Entry.Should().HaveCount(1,
            "quem procura pelo número antigo — uma solicitação já gravada, uma carga do SISREG com "
            + "o provisório — tem de achar a pessoa; senão cria uma ficha nova");
        pelaAntiga.Entry[0].Resource!.Id.Should().Be(criado.Id);
    }

    /// <summary>
    /// E continua sendo encontrado pelo oficial — a correção não pode custar o caminho normal.
    /// </summary>
    [Fact]
    public async Task Paciente_continua_sendo_encontrado_pelo_CNS_oficial()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        string definitivo = Cns("70"), anterior = Cns("898");

        var criado = await service.CriarAsync(ComDoisCns(definitivo, anterior, "JOAO DOIS CNS"));

        var bundle = await service.BuscarAsync(new PatientBusca(Cns: definitivo));

        bundle.Entry.Should().HaveCount(1);
        bundle.Entry[0].Resource!.Id.Should().Be(criado.Id);
    }

    /// <summary>
    /// A coluna <c>cns</c> guarda o OFICIAL, não o primeiro da lista.
    ///
    /// <para>Sem a preferência por <c>use=official</c>, um identificador antigo gravado antes
    /// ficaria na coluna e a ficha apareceria pelo número que já não vale — inclusive nas telas
    /// que leem a projeção em vez do documento.</para>
    /// </summary>
    [Fact]
    public async Task Coluna_de_busca_guarda_o_oficial_mesmo_quando_o_antigo_vem_primeiro()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        string definitivo = Cns("70"), anterior = Cns("898");

        // O ANTIGO vem primeiro na lista, de propósito.
        var paciente = new Patient
        {
            Meta = new Meta { Source = "https://smsmarica.saude.marica/source/teste" },
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "ANA ORDEM INVERTIDA" }],
            BirthDate = "1975-01-01",
            Identifier =
            [
                new Identifier(SysCns, anterior) { Use = Identifier.IdentifierUse.Old },
                new Identifier(SysCns, definitivo) { Use = Identifier.IdentifierUse.Official },
            ],
            Active = true,
        };
        var criado = await service.CriarAsync(paciente);

        var linha = await db.Patients.FindAsync(Guid.Parse(criado.Id));
        linha!.Cns.Should().Be(definitivo, "a coluna de busca representa a pessoa HOJE");
        linha.CnsTodos.Should().BeEquivalentTo([definitivo, anterior],
            "mas a lista guarda os dois — o antigo continua sendo chave de busca válida");
    }

    /// <summary>Um CNS que não é de ninguém continua não achando nada.</summary>
    [Fact]
    public async Task CNS_desconhecido_nao_devolve_paciente()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        await service.CriarAsync(ComDoisCns(Cns("70"), Cns("898"), "PEDRO DOIS CNS"));

        var bundle = await service.BuscarAsync(new PatientBusca(Cns: Cns("12")));

        bundle.Entry.Should().BeEmpty();
    }
}
