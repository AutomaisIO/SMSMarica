using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Encounters;
using Automais.Fhir.Core.Patients;
using Automais.Fhir.Tests.Infraestrutura;
using FluentAssertions;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace Automais.Fhir.Tests;

/// <summary>
/// Fusão de dois Patients que são a MESMA pessoa (<c>$merge</c>).
///
/// <para><b>Por que isto existe.</b> Medido em 24/09/2026: o hub tem <b>1.195 grupos</b> de
/// paciente duplicado (510 pelo mesmo CPF, 685 pelo mesmo CNS), 2.477 registros, com 8.929 linhas
/// clínicas apontando para os que seriam absorvidos. E a causa dominante não é conflito entre
/// PEPs — é <b>reimportação cega</b>: 457 grupos são a carga do SISREG duplicando a si mesma, e
/// ela é justamente a fonte com <b>0%</b> de chave de origem.</para>
///
/// <para><b>Por isso o teste mais importante aqui é o da chave absorvida.</b> Fundir sem levar os
/// identifiers do absorvido resolve o sintoma e mantém a doença: a próxima carga procuraria pela
/// chave antiga, não acharia ninguém e criaria a duplicata de novo.</para>
///
/// <para><b>E o absorvido não é apagado.</b> Fusão junta prontuário de duas pessoas; se o par
/// estiver errado é preciso poder desfazer. Além disso `smsmarica.laudo` aponta para paciente, e
/// quem chegar por um id antigo tem de encontrar o ponteiro em vez de 404.</para>
/// </summary>
[Collection(nameof(PostgresFhirCollection))]
public class FusaoDePacienteTests(PostgresFhirFixture fixture)
{
    private const string SysCns = "https://fhir.saude.gov.br/sid/cns";
    private const string SysOrigem = "urn:prime:paciente";

    /// <summary>CNS sintético único por chamada — a bancada é compartilhada entre execuções.</summary>
    private static string Cns(string prefixo)
    {
        var corpo = Random.Shared.NextInt64(0, 1_000_000_000_000_000).ToString("D15");
        return prefixo + corpo[prefixo.Length..];
    }

    private static Patient Ficha(string nome, string cns, string? chaveOrigem = null)
    {
        var p = new Patient
        {
            Meta = new Meta { Source = "https://smsmarica.saude.marica/source/teste" },
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }],
            BirthDate = "1975-03-08",
            Identifier =
            [
                new Identifier(SysCns, cns) { Use = Identifier.IdentifierUse.Official },
            ],
        };
        if (chaveOrigem is not null)
            p.Identifier.Add(new Identifier(SysOrigem, chaveOrigem));
        return p;
    }

    private static Encounter Atendimento(string patientId) => new()
    {
        Meta = new Meta { Source = "https://smsmarica.saude.marica/source/teste" },
        Status = Encounter.EncounterStatus.Finished,
        Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "AMB"),
        Subject = new ResourceReference($"Patient/{patientId}"),
    };

    /// <summary>O clínico do absorvido passa a apontar para o sobrevivente.</summary>
    [Fact]
    public async Task Atendimento_do_absorvido_passa_para_o_sobrevivente()
    {
        await using var db = fixture.CriarContexto();
        var pacientes = new PatientService(db, TimeProvider.System);
        var encontros = new EncounterService(db, TimeProvider.System);

        var fica = await pacientes.CriarAsync(Ficha("ANA FUSAO FICA", Cns("70")));
        var vai = await pacientes.CriarAsync(Ficha("ANA FUSAO VAI", Cns("898")));
        var atendimento = await encontros.CriarAsync(Atendimento(vai.Id!));

        var r = await pacientes.FundirAsync(Guid.Parse(fica.Id!), Guid.Parse(vai.Id!));

        r.Encounters.Should().Be(1);
        var lido = await encontros.LerAsync(Guid.Parse(atendimento.Id!));
        lido.Subject!.Reference.Should().Be($"Patient/{fica.Id}",
            "o atendimento é da pessoa, e a pessoa agora é o sobrevivente");
    }

    /// <summary>
    /// O sobrevivente passa a ser encontrado pelo CNS do absorvido.
    ///
    /// <para>É o teste que impede a duplicata de voltar: sem absorver a chave, a próxima carga
    /// procura pelo número antigo, não acha ninguém e cria outra ficha.</para>
    /// </summary>
    [Fact]
    public async Task Sobrevivente_passa_a_ser_encontrado_pela_chave_do_absorvido()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        string cnsFica = Cns("70"), cnsVai = Cns("898");
        var chaveOrigem = Guid.NewGuid().ToString();

        var fica = await service.CriarAsync(Ficha("BRUNO FUSAO FICA", cnsFica));
        var vai = await service.CriarAsync(Ficha("BRUNO FUSAO VAI", cnsVai, chaveOrigem));

        await service.FundirAsync(Guid.Parse(fica.Id!), Guid.Parse(vai.Id!));

        var porCnsAntigo = await service.BuscarAsync(new PatientBusca(Cns: cnsVai));
        porCnsAntigo.Entry.Should().HaveCount(1, "o CNS do absorvido tem de levar ao sobrevivente");
        porCnsAntigo.Entry[0].Resource!.Id.Should().Be(fica.Id);

        var porChaveOrigem = await service.BuscarAsync(
            new PatientBusca(IdentifierSystem: SysOrigem, IdentifierValue: chaveOrigem));
        porChaveOrigem.Entry.Should().HaveCount(1,
            "a chave da ORIGEM é o que a próxima carga usa para se reconhecer — perdê-la na fusão "
            + "é recriar a duplicata no ciclo seguinte");
        porChaveOrigem.Entry[0].Resource!.Id.Should().Be(fica.Id);

        var oficial = await service.LerAsync(Guid.Parse(fica.Id!));
        oficial.Identifier.Single(i => i.System == SysCns && i.Value == cnsFica)
            .Use.Should().Be(Identifier.IdentifierUse.Official,
                "o CNS absorvido não pode destronar o oficial do sobrevivente");
        oficial.Identifier.Single(i => i.System == SysCns && i.Value == cnsVai)
            .Use.Should().Be(Identifier.IdentifierUse.Old);
    }

    /// <summary>O absorvido continua legível, inativo, apontando para quem ficou.</summary>
    [Fact]
    public async Task Absorvido_continua_legivel_apontando_para_o_sobrevivente()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);

        var fica = await service.CriarAsync(Ficha("CARLA FUSAO FICA", Cns("70")));
        var vai = await service.CriarAsync(Ficha("CARLA FUSAO VAI", Cns("898")));

        await service.FundirAsync(Guid.Parse(fica.Id!), Guid.Parse(vai.Id!));

        var lido = await service.LerAsync(Guid.Parse(vai.Id!));
        lido.Active.Should().BeFalse();
        lido.Link.Should().ContainSingle(l => l.Type == Patient.LinkType.ReplacedBy
                                              && l.Other.Reference == $"Patient/{fica.Id}",
            "quem chegar pelo id antigo — link salvo, integração, app do cidadão — tem de achar o "
            + "ponteiro para o novo, não um 404");

        var sobrevivente = await service.LerAsync(Guid.Parse(fica.Id!));
        sobrevivente.Link.Should().ContainSingle(l => l.Type == Patient.LinkType.Replaces
                                                      && l.Other.Reference == $"Patient/{vai.Id}");

        (await db.Patients.AsNoTracking().FirstAsync(p => p.Id == Guid.Parse(vai.Id!)))
            .IsDeleted.Should().BeFalse("excluir logicamente devolveria 404 e quebraria o ponteiro");
    }

    /// <summary>Fundir duas vezes é recusado — empilhar links tornaria o desfazer ambíguo.</summary>
    [Fact]
    public async Task Fundir_o_mesmo_paciente_duas_vezes_e_recusado()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);

        var fica = await service.CriarAsync(Ficha("DIEGO FUSAO FICA", Cns("70")));
        var vai = await service.CriarAsync(Ficha("DIEGO FUSAO VAI", Cns("898")));
        await service.FundirAsync(Guid.Parse(fica.Id!), Guid.Parse(vai.Id!));

        var denovo = async () => await service.FundirAsync(Guid.Parse(fica.Id!), Guid.Parse(vai.Id!));
        await denovo.Should().ThrowAsync<RecursoInvalidoException>();
    }

    /// <summary>Fundir um paciente consigo mesmo é recusado.</summary>
    [Fact]
    public async Task Fundir_consigo_mesmo_e_recusado()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);
        var p = await service.CriarAsync(Ficha("ELENA FUSAO", Cns("70")));

        var acao = async () => await service.FundirAsync(Guid.Parse(p.Id!), Guid.Parse(p.Id!));
        await acao.Should().ThrowAsync<RecursoInvalidoException>();
    }
}
