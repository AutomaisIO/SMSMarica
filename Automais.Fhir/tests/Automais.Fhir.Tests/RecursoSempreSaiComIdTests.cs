using Automais.Fhir.Core.Patients;
using Automais.Fhir.Data.Entities;
using Automais.Fhir.Tests.Infraestrutura;
using FluentAssertions;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Automais.Fhir.Tests;

/// <summary>
/// Um Patient nunca sai do hub sem <c>id</c>, mesmo que o documento gravado não o tenha.
///
/// <para><b>Incidente de 08/09/2026.</b> Uma carga do SISREG inseriu 36.257 fichas direto no banco
/// com o id gerado por <c>gen_random_uuid()</c> na COLUNA e sem a chave <c>"id"</c> dentro do
/// <c>content</c>. Do outro lado, o <c>PacienteFhirMapper</c> do SMSMais.server faz
/// <c>Guid.Parse(p.Id)</c> — e <c>Guid.Parse(null)</c> lança
/// <c>ArgumentNullException (Parameter 'input')</c>. Resultado: 500 em <c>/pacientes</c>, na lista
/// de pacientes da conversa, no login por OTP do cidadão e no <b>webhook do WhatsApp</b>, com 219
/// mensagens de paciente não processadas ao longo de quase quatro horas.</para>
///
/// <para><b>Por que a defesa fica no hub e não no consumidor.</b> Quem lê não tem como se
/// defender: recebe um recurso e confia que ele tem identidade. A coluna <c>id</c> é a chave
/// primária e é por ela que o recurso é endereçado — o hub sabe o id mesmo quando o documento
/// esqueceu, e preenchê-lo na leitura fecha a classe inteira de falha, independentemente de quem
/// gravou. Corrigir só o escritor que errou desta vez deixaria o próximo livre para repetir.</para>
/// </summary>
[Collection(nameof(PostgresFhirCollection))]
public class RecursoSempreSaiComIdTests(PostgresFhirFixture fixture)
{
    [Fact]
    public async Task Ficha_gravada_sem_id_no_documento_e_lida_com_o_id_da_coluna()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);

        // Exatamente o que a carga do SISREG produziu: id na coluna, ausente no documento.
        var id = Guid.NewGuid();
        db.Patients.Add(new PatientRow
        {
            Id = id,
            VersionId = 1,
            LastUpdated = DateTimeOffset.UtcNow,
            MetaSource = "https://smsmarica.saude.marica/source/sisreg/implantacao",
            Nome = "PACIENTE SEM ID NO DOCUMENTO",
            Content = """
                {"resourceType":"Patient","active":true,
                 "name":[{"use":"official","text":"PACIENTE SEM ID NO DOCUMENTO"}],
                 "meta":{"source":"https://smsmarica.saude.marica/source/sisreg/implantacao"}}
                """,
        });
        await db.SaveChangesAsync();

        var lido = await service.LerAsync(id);

        lido.Id.Should().Be(id.ToString(),
            "quem consome faz Guid.Parse(p.Id) e um id nulo derruba a leitura de paciente inteira");
    }

    [Fact]
    public async Task A_busca_tambem_devolve_o_id_de_quem_foi_gravado_sem_ele()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);

        var id = Guid.NewGuid();
        var nome = $"BUSCA SEM ID {Guid.NewGuid():N}"[..30];

        // Busca por CPF, e não por nome, de propósito: a busca por nome usa `unaccent()`, que
        // existe na bancada mas NÃO no Postgres do container do CI. Um teste que dependesse dela
        // passaria aqui e derrubaria o deploy — foi exatamente o que aconteceu em 08/09/2026.
        var cpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

        db.Patients.Add(new PatientRow
        {
            Id = id,
            VersionId = 1,
            LastUpdated = DateTimeOffset.UtcNow,
            MetaSource = "https://smsmarica.saude.marica/source/sisreg/implantacao",
            Nome = nome,
            Cpf = cpf,
            Content = """
                {"resourceType":"Patient","active":true,
                 "identifier":[{"system":"https://fhir.saude.gov.br/sid/cpf","value":"CPF"}],
                 "name":[{"use":"official","text":"NOME"}],
                 "meta":{"source":"https://smsmarica.saude.marica/source/sisreg/implantacao"}}
                """.Replace("NOME", nome).Replace("CPF", cpf),
        });
        await db.SaveChangesAsync();

        // A busca é o caminho que quebrou em produção: /pacientes e a lista da conversa passam
        // por aqui, e uma única ficha sem id derrubava a resposta inteira, não só aquela linha.
        var bundle = await service.BuscarAsync(new PatientBusca(Cpf: cpf));

        bundle.Entry.Should().NotBeEmpty();
        bundle.Entry.Select(e => (e.Resource as Patient)?.Id)
            .Should().AllSatisfy(x => x.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task Documento_que_ja_tem_id_nao_e_reescrito()
    {
        await using var db = fixture.CriarContexto();
        var service = new PatientService(db, TimeProvider.System);

        var criado = await service.CriarAsync(new Patient
        {
            Active = true,
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "COM ID PROPRIO" }],
        });

        var lido = await service.LerAsync(Guid.Parse(criado.Id!));

        lido.Id.Should().Be(criado.Id);
    }
}
