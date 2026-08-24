#nullable disable
using Hl7.Fhir.Model;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Data.Entities.Enums;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// Unit do PacienteFhirMapper (native-first + carimbo por-mudança). Trava o shadowing
/// corrigido, preservação de identificadores, nome nunca vazio e o diff de campos editados.
/// </summary>
public class PacienteFhirMapperTests
{
    private static Patient Importado() => new()
    {
        Id = Guid.NewGuid().ToString(),
        Active = true,
        Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "MARIA IMPORTADA" }],
        Identifier =
        [
            new Identifier("https://fhir.saude.gov.br/sid/cpf", "12345678900"),
            new Identifier("urn:salux:cd_paciente", "salux-hcml:1"),
        ],
        Telecom =
        [
            new ContactPoint { System = ContactPoint.ContactPointSystem.Phone, Value = "21970000000", Use = ContactPoint.ContactPointUse.Home },
            new ContactPoint { System = ContactPoint.ContactPointSystem.Email, Value = "maria@x.com" },
        ],
        BirthDate = "1980-05-01",
        Gender = AdministrativeGender.Female,
    };

    [Fact]
    public void Editar_email_de_importado_reflete_no_ParaDto()
    {
        // Shadowing corrigido: sem escrita nativa, ParaDto lia o e-mail antigo.
        var p = Importado();
        PacienteFhirMapper.AplicarAtualizacao(p, new AtualizarPacienteRequest(null, null, Email: "novo@x.com"));
        PacienteFhirMapper.ParaDto(p).Email.Should().Be("novo@x.com");
    }

    [Fact]
    public void Editar_importado_preserva_nome_e_identificadores_secundarios()
    {
        var p = Importado();
        PacienteFhirMapper.AplicarAtualizacao(p, new AtualizarPacienteRequest(null, null, Email: "novo@x.com"));
        PacienteFhirMapper.ParaDto(p).NomeCompleto.Should().Be("MARIA IMPORTADA"); // nunca vazio
        p.Identifier.Should().Contain(i => i.System == "urn:salux:cd_paciente"); // cd_paciente preservado
    }

    [Fact]
    public void Editar_so_o_telefone_marca_apenas_telefone_como_editado()
    {
        // Regressão da revisão (ALTA #1): antes marcava tudo (congelava o demográfico vs Oracle).
        var p = Importado();
        PacienteFhirMapper.AplicarAtualizacao(p, new AtualizarPacienteRequest(null, null,
            TelefonePrincipal: "21988887777", Email: "maria@x.com")); // e-mail igual ao nativo
        PatientMergeFhir.CamposEditados(p).Should().BeEquivalentTo(["telefone"]);
    }

    [Fact]
    public void AplicarNome_corrige_e_marca_nome_como_editado()
    {
        var p = Importado();
        PacienteFhirMapper.AplicarNome(p, "MARIA CORRIGIDA");
        PacienteFhirMapper.ParaDto(p).NomeCompleto.Should().Be("MARIA CORRIGIDA");
        PatientMergeFhir.CamposEditados(p).Should().Contain("nome");
    }

    [Fact]
    public void PromoverBlobParaNativo_sem_blob_e_no_op()
    {
        // Paciente importado (sem blob) — nada a promover.
        PacienteFhirMapper.PromoverBlobParaNativo(Importado()).Should().BeFalse();
    }

    [Fact]
    public void EstadoCivil_texto_pt_br_do_importado_e_normalizado()
    {
        var p = Importado();
        p.MaritalStatus = new CodeableConcept { Text = "CASADO(A)" };
        PacienteFhirMapper.ParaDto(p).EstadoCivil.Should().Be(EstadoCivil.Casado);
    }
}
