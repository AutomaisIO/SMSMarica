#nullable disable
using Hl7.Fhir.Model;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Tests.Pacientes;

/// <summary>
/// Unit do motor de merge FHIR nativo (ADR-0020). Trava as regras delicadas de
/// merge/preserve, telefone confirmado e "painel vence no que editou".
/// </summary>
public class PatientMergeFhirTests
{
    private const string Phone = "Phone";
    private static string Digitos(string v) => new((v ?? "").Where(char.IsDigit).ToArray());
    private static ContactPoint Fone(string v, ContactPoint.ContactPointUse? use = null, int? rank = null) =>
        new() { System = ContactPoint.ContactPointSystem.Phone, Value = v, Use = use, Rank = rank };

    // ---------------- Identifiers ----------------

    [Fact]
    public void UpsertIdentifier_preserva_os_demais_e_atualiza_por_system()
    {
        var p = new Patient { Identifier = [new Identifier("urn:salux:cd_paciente", "salux-hcml:361843"), new Identifier(PatientMergeFhir.SystemCpf, "111")] };

        PatientMergeFhir.UpsertIdentifier(p, PatientMergeFhir.SystemCpf, "222"); // atualiza CPF
        PatientMergeFhir.UpsertIdentifier(p, PatientMergeFhir.SystemRg, "");     // vazio = no-op

        p.Identifier.Should().Contain(i => i.System == "urn:salux:cd_paciente" && i.Value == "salux-hcml:361843");
        p.Identifier.Should().Contain(i => i.System == PatientMergeFhir.SystemCpf && i.Value == "222");
        p.Identifier.Where(i => i.System == PatientMergeFhir.SystemCpf).Should().HaveCount(1);
        p.Identifier.Should().NotContain(i => i.System == PatientMergeFhir.SystemRg);
    }

    // ---------------- Telecom ----------------

    [Fact]
    public void AplicarContatos_importado_um_home_phone_vira_principal_sem_duplicar()
    {
        var p = new Patient { Telecom = [Fone("21999990000", ContactPoint.ContactPointUse.Home)] };

        // O painel leu principal = esse número; residencial/celular vazios.
        PatientMergeFhir.AplicarContatos(p, "21999990000", null, null, null);

        p.Telecom.Where(t => t.System == ContactPoint.ContactPointSystem.Phone).Should().HaveCount(1);
        p.Telecom.Single(t => t.System == ContactPoint.ContactPointSystem.Phone).Rank.Should().Be(1);
    }

    [Fact]
    public void AplicarContatos_e_idempotente()
    {
        var p = new Patient();
        PatientMergeFhir.AplicarContatos(p, "21999990000", "21988887777", null, "a@b.com");
        var antes = p.Telecom.Count;
        PatientMergeFhir.AplicarContatos(p, "21999990000", "21988887777", null, "a@b.com");
        p.Telecom.Count.Should().Be(antes);
    }

    [Fact]
    public void Vazio_e_no_op_nunca_remove_telefone()
    {
        var p = new Patient { Telecom = [Fone("21999990000", ContactPoint.ContactPointUse.Home)] };
        PatientMergeFhir.AplicarContatos(p, null, null, null, null); // tudo vazio
        p.Telecom.Should().ContainSingle(t => Digitos(t.Value) == "21999990000");
    }

    // ---------------- Telefone confirmado (intocável) ----------------

    [Fact]
    public void Confirmado_nunca_e_alterado_pela_automacao()
    {
        var p = new Patient { Telecom = [Fone("21999990000", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(p, "5521999990000", DateTimeOffset.UtcNow);

        // Painel tenta trocar o principal para outro número.
        PatientMergeFhir.AplicarContatos(p, "21988887777", null, null, null);

        var confirmado = p.Telecom.Single(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null);
        Digitos(confirmado.Value).Should().Be("21999990000");   // inalterado
        p.Telecom.Should().NotContain(t => Digitos(t.Value) == "21988887777"); // não moveu/adicionou
    }

    [Fact]
    public void Edicao_manual_troca_o_principal_confirmado_e_remove_o_marcador()
    {
        var p = new Patient { Telecom = [Fone("21999990000", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(p, "5521999990000", DateTimeOffset.UtcNow);

        // Operador troca o principal de propósito: o novo número nasce NÃO verificado.
        PatientMergeFhir.AplicarContatos(p, "21988887777", null, null, null, manual: true);

        var principal = p.Telecom.Single(t => t.System == ContactPoint.ContactPointSystem.Phone && t.Rank == 1);
        Digitos(principal.Value).Should().Be("21988887777");
        p.Telecom.Should().NotContain(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) != null);
    }

    [Fact]
    public void Edicao_manual_com_o_mesmo_numero_preserva_o_marcador()
    {
        var p = new Patient { Telecom = [Fone("21999990000", rank: 1)] };
        PatientMergeFhir.MarcarTelefoneConfirmado(p, "5521999990000", DateTimeOffset.UtcNow);

        // Reenvio do form com o mesmo número (round-trip) não derruba o verificado.
        PatientMergeFhir.AplicarContatos(p, "21999990000", null, null, null, manual: true);

        var confirmado = p.Telecom.Single(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null);
        Digitos(confirmado.Value).Should().Be("21999990000");
        confirmado.Rank.Should().Be(1);
    }

    [Fact]
    public void TelefoneConfirmado_expoe_numero_e_instante_e_tolera_ddi()
    {
        var p = new Patient { Telecom = [Fone("21999990000", rank: 1)] };
        var em = DateTimeOffset.UtcNow;
        PatientMergeFhir.MarcarTelefoneConfirmado(p, "5521999990000", em);

        var conf = PatientMergeFhir.TelefoneConfirmado(p);
        conf.Should().NotBeNull();
        conf!.Value.Numero.Should().Be("21999990000");
        conf.Value.Em.Should().NotBeNull();

        PatientMergeFhir.TelefoneEstaConfirmado(p, "5521999990000").Should().BeTrue();  // com DDI
        PatientMergeFhir.TelefoneEstaConfirmado(p, "21999990000").Should().BeTrue();    // nacional
        PatientMergeFhir.TelefoneEstaConfirmado(p, "21988887777").Should().BeFalse();
    }

    [Fact]
    public void MarcarTelefoneConfirmado_e_idempotente_e_um_por_pessoa()
    {
        var p = new Patient { Telecom = [Fone("21999990000")] };
        PatientMergeFhir.MarcarTelefoneConfirmado(p, "5521999990000", DateTimeOffset.UtcNow);
        PatientMergeFhir.MarcarTelefoneConfirmado(p, "5521999990000", DateTimeOffset.UtcNow); // 2x

        p.Telecom.Where(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null).Should().HaveCount(1);

        // Troca do número confirmado: só o novo fica marcado.
        PatientMergeFhir.MarcarTelefoneConfirmado(p, "5521988887777", DateTimeOffset.UtcNow);
        var marcados = p.Telecom.Where(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null).ToList();
        marcados.Should().ContainSingle();
        Digitos(marcados[0].Value).Should().Be("21988887777");
    }

    // ---------------- Endereço (houseNumber) + maritalStatus ----------------

    [Fact]
    public void UpsertEndereco_grava_numero_em_extension_houseNumber()
    {
        var p = new Patient();
        PatientMergeFhir.UpsertEndereco(p, new EnderecoDto("24900000", "Rua A", "123", "ap 2", "Centro", "Maricá", "RJ", "perto da praça"));

        var a = p.Address.Single();
        a.Use.Should().Be(Address.AddressUse.Home);
        a.LineElement[0].GetExtension("http://hl7.org/fhir/StructureDefinition/iso21090-ADXP-houseNumber")
            .Should().NotBeNull();
        a.District.Should().Be("Centro");
        a.PostalCode.Should().Be("24900000");
    }

    [Fact]
    public void SetGeolocation_grava_extension_no_endereco_home()
    {
        var p = new Patient { Address = [new Address { Use = Address.AddressUse.Home, City = "Maricá" }] };
        PatientMergeFhir.SetGeolocation(p, -22.9, -42.8);

        var geo = p.Address.Single().GetExtension(PatientMergeFhir.ExtGeolocation);
        geo.Should().NotBeNull();
        ((FhirDecimal)geo.GetExtension("latitude").Value).Value.Should().Be(-22.9m);
        ((FhirDecimal)geo.GetExtension("longitude").Value).Value.Should().Be(-42.8m);
    }

    [Fact]
    public void SetGeolocation_sem_endereco_e_no_op()
    {
        var p = new Patient();
        var act = () => PatientMergeFhir.SetGeolocation(p, -22.9, -42.8);
        act.Should().NotThrow();
        (p.Address is null || p.Address.Count == 0).Should().BeTrue();
    }

    [Fact]
    public void SetMaritalStatus_usa_coding_v3()
    {
        var p = new Patient();
        PatientMergeFhir.SetMaritalStatus(p, EstadoCivil.Casado);
        p.MaritalStatus.Coding.Single().System.Should().Be("http://terminology.hl7.org/CodeSystem/v3-MaritalStatus");
        p.MaritalStatus.Coding.Single().Code.Should().Be("M");
    }

    // ---------------- Campos editados ----------------

    [Fact]
    public void MarcarEditados_e_CamposEditados_roundtrip_uniao()
    {
        var p = new Patient();
        PatientMergeFhir.MarcarEditados(p, ["telefone", "email"]);
        PatientMergeFhir.MarcarEditados(p, ["telefone", "endereco"]);
        PatientMergeFhir.CamposEditados(p).Should().BeEquivalentTo(["telefone", "email", "endereco"]);
    }

    // ---------------- Import: PreservarDoExistente ----------------

    private static Patient Importado(string cpf = "111", string blob = null)
    {
        var p = new Patient
        {
            Id = Guid.NewGuid().ToString(),
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "MARIA DO ORACLE" }],
            Identifier = [new Identifier(PatientMergeFhir.SystemCpf, cpf), new Identifier("urn:salux:cd_paciente", "salux-hcml:1")],
            Telecom = [Fone("21970000000", ContactPoint.ContactPointUse.Home)],
        };
        if (blob is not null) p.AddExtension(PatientMergeFhir.ExtPayloadBlob, new FhirString(blob));
        return p;
    }

    [Fact]
    public void PreservarDoExistente_preserva_blob_e_edicoes_do_painel()
    {
        var atual = Importado(blob: "{\"NomeCompleto\":\"x\"}");
        PatientMergeFhir.MarcarEditados(atual, ["telefone", "endereco"]);
        atual.Telecom = [Fone("21988887777", ContactPoint.ContactPointUse.Home, rank: 1)]; // telefone do painel
        atual.Address = [new Address { Use = Address.AddressUse.Home, City = "MARICA-PAINEL" }];

        // 'novo' vem do Oracle com telefone/endereço/identidade próprios.
        var novo = new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "MARIA ORACLE NOVA" }],
            Telecom = [Fone("21911112222", ContactPoint.ContactPointUse.Home)],
            Address = [new Address { City = "MARICA-ORACLE" }],
        };

        PatientMergeFhir.PreservarDoExistente(novo, atual);

        novo.GetExtension(PatientMergeFhir.ExtPayloadBlob).Should().NotBeNull();           // blob preservado
        novo.Telecom.Should().Contain(t => Digitos(t.Value) == "21988887777");             // telefone do painel venceu
        novo.Address.Single().City.Should().Be("MARICA-PAINEL");                           // endereço do painel venceu
        novo.Name.Single(n => n.Use == HumanName.NameUse.Official).Text.Should().Be("MARIA ORACLE NOVA"); // nome do Oracle (não editado)
    }

    [Fact]
    public void PreservarDoExistente_nome_editado_vence_o_oracle()
    {
        var atual = Importado();
        atual.Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "MARIA CORRIGIDA" }];
        PatientMergeFhir.MarcarEditados(atual, ["nome"]);
        var novo = new Patient { Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "MARIA ERRADA ORACLE" }] };

        PatientMergeFhir.PreservarDoExistente(novo, atual);

        novo.Name.Single(n => n.Use == HumanName.NameUse.Official).Text.Should().Be("MARIA CORRIGIDA");
    }

    [Fact]
    public void PreservarDoExistente_nao_crasha_com_telefone_editado_e_confirmado()
    {
        // Regressão da revisão (ALTA #2): coleção modificada durante enumeração.
        var atual = Importado();
        atual.Telecom = [Fone("21988887777", ContactPoint.ContactPointUse.Home, rank: 1)];
        PatientMergeFhir.MarcarTelefoneConfirmado(atual, "5521988887777", DateTimeOffset.UtcNow);
        PatientMergeFhir.MarcarEditados(atual, ["telefone"]);

        var novo = new Patient { Telecom = [Fone("21911112222")] };

        var act = () => PatientMergeFhir.PreservarDoExistente(novo, atual);
        act.Should().NotThrow();
        novo.Telecom.Where(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null).Should().HaveCount(1);
    }

    [Fact]
    public void PreservarDoExistente_email_editado_nao_congela_telefone_do_oracle()
    {
        var atual = Importado();
        atual.Telecom = [new ContactPoint { System = ContactPoint.ContactPointSystem.Email, Value = "painel@x.com" }];
        PatientMergeFhir.MarcarEditados(atual, ["email"]);

        var novo = new Patient { Telecom = [Fone("21955554444"), new ContactPoint { System = ContactPoint.ContactPointSystem.Email, Value = "oracle@x.com" }] };

        PatientMergeFhir.PreservarDoExistente(novo, atual);

        novo.Telecom.Should().Contain(t => t.System == ContactPoint.ContactPointSystem.Email && t.Value == "painel@x.com"); // email do painel
        novo.Telecom.Should().Contain(t => Digitos(t.Value) == "21955554444"); // telefone do Oracle NÃO congelado
    }

    [Fact]
    public void PreservarDoExistente_demove_rank1_do_oracle_quando_ha_confirmado()
    {
        var atual = Importado();
        atual.Telecom = [Fone("21988887777", rank: 1)];
        PatientMergeFhir.MarcarTelefoneConfirmado(atual, "5521988887777", DateTimeOffset.UtcNow);

        // Prontuário externo chega com telefone reivindicando o principal.
        var novo = new Patient { Telecom = [Fone("21911112222", rank: 1)] };

        PatientMergeFhir.PreservarDoExistente(novo, atual);

        var principais = novo.Telecom.Where(t => t.System == ContactPoint.ContactPointSystem.Phone && t.Rank == 1).ToList();
        principais.Should().ContainSingle();
        Digitos(principais[0].Value).Should().Be("21988887777"); // o confirmado é o único principal
        novo.Telecom.Should().Contain(t => Digitos(t.Value) == "21911112222"); // o do Oracle fica em slot secundário
    }

    /// <summary>
    /// A invariante vale para prontuário COMPLETO também, não só para fonte parcial: o Oracle
    /// deixar de trazer um telefone não é motivo para o hub perdê-lo. Um número a menos é um
    /// cidadão que a Secretaria não consegue avisar; quem remove contato é o painel.
    /// </summary>
    [Fact]
    public void PreservarDoExistente_nao_apaga_telefone_que_a_origem_deixou_de_trazer()
    {
        var atual = Importado();
        atual.Telecom = [Fone("21988887777", rank: 1), Fone("2126210000", ContactPoint.ContactPointUse.Home)];

        // Oracle veio sem telefone nenhum nesta leitura.
        var novo = new Patient { Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "MARIA" }] };

        PatientMergeFhir.PreservarDoExistente(novo, atual);

        novo.Telecom.Select(t => Digitos(t.Value)).Should().BeEquivalentTo(["21988887777", "2126210000"]);
        novo.Telecom.Single(t => t.Rank == 1).Value.Should().Be("21988887777");
    }

    [Fact]
    public void PreservarDoExistente_e_idempotente()
    {
        var atual = Importado();
        atual.Telecom = [Fone("21988887777", rank: 1)];
        PatientMergeFhir.MarcarTelefoneConfirmado(atual, "5521988887777", DateTimeOffset.UtcNow);

        var novo1 = new Patient { Telecom = [Fone("21911112222")] };
        var novo2 = new Patient { Telecom = [Fone("21911112222")] };
        PatientMergeFhir.PreservarDoExistente(novo1, atual);
        PatientMergeFhir.PreservarDoExistente(novo2, atual);

        novo1.Telecom.Count(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null)
            .Should().Be(novo2.Telecom.Count(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null))
            .And.Be(1);
    }
}
