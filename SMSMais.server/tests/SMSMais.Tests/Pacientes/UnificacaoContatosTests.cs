using Hl7.Fhir.Model;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// <see cref="PatientMergeFhir.UnirContatosDaUnificacao"/>: na unificação de cadastros, contato
/// acumula por padrão (regra de 10/08/2026), só sai o que o operador desligou, e o telefone
/// verificado nunca sai nem é sobrescrito (um por pessoa).
/// </summary>
public class UnificacaoContatosTests
{
    private static ContactPoint Fone(string v, int? rank = null) =>
        new() { System = ContactPoint.ContactPointSystem.Phone, Value = v, Rank = rank };

    private static ContactPoint Email(string v) =>
        new() { System = ContactPoint.ContactPointSystem.Email, Value = v };

    private static int Confirmados(Patient p) =>
        p.Telecom.Count(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null);

    [Fact]
    public void Soma_os_telefones_e_o_email_do_absorvido_sem_duplicar_os_que_ja_existem()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21911112222", rank: 1)] };
        var absorvido = new Patient
        {
            Telecom = [Fone("5521911112222", rank: 1), Fone("21933334444"), Email("Fulano@Exemplo.com")],
        };

        var (somados, _) = PatientMergeFhir.UnirContatosDaUnificacao(sobrevivente, absorvido);

        somados.Should().Be(2); // o 21911112222 (com DDI) já estava
        sobrevivente.Telecom.Select(t => t.Value).Should()
            .BeEquivalentTo(["21911112222", "21933334444", "Fulano@Exemplo.com"]);
        // O principal continua o do sobrevivente.
        sobrevivente.Telecom.Single(t => t.Rank == 1).Value.Should().Be("21911112222");
    }

    [Fact]
    public void E_idempotente()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21911112222", rank: 1)] };
        var absorvido = new Patient { Telecom = [Fone("21933334444")] };

        PatientMergeFhir.UnirContatosDaUnificacao(sobrevivente, absorvido);
        var (segunda, _) = PatientMergeFhir.UnirContatosDaUnificacao(sobrevivente, absorvido);

        segunda.Should().Be(0);
        sobrevivente.Telecom.Should().HaveCount(2);
    }

    [Fact]
    public void Numero_negado_continua_negado_depois_de_somado()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21911112222", rank: 1)] };
        var absorvido = new Patient { Telecom = [Fone("21933334444", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneNegado(absorvido, "21933334444", DateTimeOffset.UtcNow);

        PatientMergeFhir.UnirContatosDaUnificacao(sobrevivente, absorvido);

        var somado = sobrevivente.Telecom.Single(t => t.Value == "21933334444");
        somado.GetExtension(PatientMergeFhir.ExtContatoNegado).Should().NotBeNull();
        somado.Rank.Should().BeNull(); // não disputa o principal
    }

    [Fact]
    public void Verificado_do_absorvido_vira_o_verificado_quando_o_sobrevivente_nao_tem()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21911112222", rank: 1)] };
        var absorvido = new Patient { Telecom = [Fone("21933334444", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(
            absorvido, "21933334444", DateTimeOffset.UtcNow, VinculoContatoVerificado.MaeOuPaiOuResponsavel);

        PatientMergeFhir.UnirContatosDaUnificacao(sobrevivente, absorvido);

        PatientMergeFhir.TelefoneConfirmado(sobrevivente)!.Value.Numero.Should().Be("21933334444");
        Confirmados(sobrevivente).Should().Be(1);
        var verificado = sobrevivente.Telecom.Single(t => t.Value == "21933334444");
        verificado.Rank.Should().Be(1); // o verificado é o único principal
        (verificado.GetExtension(PatientMergeFhir.ExtContatoVinculo)?.Value as FhirString)?.Value
            .Should().Be(nameof(VinculoContatoVerificado.MaeOuPaiOuResponsavel));
        sobrevivente.Telecom.Single(t => t.Value == "21911112222").Rank.Should().BeNull();
    }

    [Fact]
    public void Verificado_do_absorvido_marca_o_numero_que_o_sobrevivente_ja_tinha_sem_marca()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21933334444", rank: 1)] };
        var absorvido = new Patient { Telecom = [Fone("21933334444", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(absorvido, "21933334444", DateTimeOffset.UtcNow);

        PatientMergeFhir.UnirContatosDaUnificacao(sobrevivente, absorvido);

        sobrevivente.Telecom.Should().ContainSingle();
        PatientMergeFhir.TelefoneConfirmado(sobrevivente).Should().NotBeNull();
    }

    [Fact]
    public void Sobrevivente_com_verificado_fica_com_o_dele_e_o_do_absorvido_entra_como_numero_comum()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21911112222", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(sobrevivente, "21911112222", DateTimeOffset.UtcNow);
        var absorvido = new Patient { Telecom = [Fone("21933334444", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(absorvido, "21933334444", DateTimeOffset.UtcNow);

        PatientMergeFhir.UnirContatosDaUnificacao(sobrevivente, absorvido);

        Confirmados(sobrevivente).Should().Be(1);
        PatientMergeFhir.TelefoneConfirmado(sobrevivente)!.Value.Numero.Should().Be("21911112222");
        var somado = sobrevivente.Telecom.Single(t => t.Value == "21933334444");
        somado.GetExtension(PatientMergeFhir.ExtContatoVinculo).Should().BeNull();
        somado.Rank.Should().BeNull();
    }

    [Fact]
    public void Numero_desligado_do_outro_cadastro_nao_e_importado()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21911112222", rank: 1)] };
        var absorvido = new Patient { Telecom = [Fone("21933334444"), Fone("21955556666")] };

        var (somados, retirados) = PatientMergeFhir.UnirContatosDaUnificacao(
            sobrevivente, absorvido, ["21933334444"]);

        somados.Should().Be(1);
        retirados.Should().BeEmpty();
        sobrevivente.Telecom.Select(t => t.Value).Should().BeEquivalentTo(["21911112222", "21955556666"]);
    }

    [Fact]
    public void Numero_desligado_do_proprio_cadastro_vira_historico_e_nao_some()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21911112222", rank: 1), Fone("21922223333")] };
        var absorvido = new Patient { Telecom = [Fone("21933334444")] };

        var (_, retirados) = PatientMergeFhir.UnirContatosDaUnificacao(
            sobrevivente, absorvido, ["5521911112222"]); // com DDI: casa pelo mesmo número

        retirados.Should().ContainSingle().Which.Should().Be("21911112222");
        var antigo = sobrevivente.Telecom.Single(t => t.Value == "21911112222");
        PatientMergeFhir.EhAposentado(antigo).Should().BeTrue();
        antigo.Rank.Should().BeNull();
        sobrevivente.Telecom.Where(t => !PatientMergeFhir.EhAposentado(t)).Select(t => t.Value)
            .Should().BeEquivalentTo(["21922223333", "21933334444"]);
    }

    [Fact]
    public void Numero_verificado_nunca_sai_mesmo_desligado()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21911112222", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(sobrevivente, "21911112222", DateTimeOffset.UtcNow);
        var absorvido = new Patient { Telecom = [Fone("21933334444", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(absorvido, "21933334444", DateTimeOffset.UtcNow);

        var (somados, retirados) = PatientMergeFhir.UnirContatosDaUnificacao(
            sobrevivente, absorvido, ["21911112222", "21933334444"]);

        retirados.Should().BeEmpty();
        somados.Should().Be(1); // o verificado do outro entra (como número comum: já há um verificado)
        PatientMergeFhir.TelefoneConfirmado(sobrevivente)!.Value.Numero.Should().Be("21911112222");
        PatientMergeFhir.EhAposentado(sobrevivente.Telecom.Single(t => t.Value == "21911112222")).Should().BeFalse();
    }

    [Fact]
    public void Verificado_do_outro_cadastro_desligado_vem_mesmo_assim_quando_o_unificado_nao_tem()
    {
        var sobrevivente = new Patient { Telecom = [Fone("21911112222", rank: 1)] };
        var absorvido = new Patient { Telecom = [Fone("21933334444", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(absorvido, "21933334444", DateTimeOffset.UtcNow);

        PatientMergeFhir.UnirContatosDaUnificacao(sobrevivente, absorvido, ["21933334444"]);

        PatientMergeFhir.TelefoneConfirmado(sobrevivente)!.Value.Numero.Should().Be("21933334444");
    }
}
