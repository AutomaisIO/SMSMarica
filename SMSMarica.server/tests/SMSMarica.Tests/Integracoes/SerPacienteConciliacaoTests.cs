using FluentAssertions;
using Hl7.Fhir.Model;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.Ser.Pacientes;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// Conciliação do paciente do SER com o hub FHIR.
///
/// <para>O que está sob teste é o que dói se quebrar: o SER é fonte PARCIAL, e um mapeamento
/// distraído aqui não gera erro — apaga dado bom no hub em silêncio.</para>
/// </summary>
public class SerPacienteConciliacaoTests
{
    private static SerSolicitacao Solicitacao(Action<SerSolicitacao>? ajuste = null)
    {
        var s = new SerSolicitacao
        {
            IdSer = "2727024",
            PacienteNome = "DAIANA SEABRA DA SILVA",
            Cns = "700300908811839",
            Cpf = "529.982.247-25",   // CPF com DV válido
            Sexo = "F",
            DataNascimento = new DateOnly(1985, 4, 12),
            NomeMae = "MARIA SEABRA",
        };
        ajuste?.Invoke(s);
        return s;
    }

    [Fact]
    public void Whatsapp_do_ser_vai_para_o_slot_de_celular()
    {
        var p = SerPacienteFhirMapper.Construir(Solicitacao(s =>
        {
            s.TelefoneWhatsapp = "(21) 98888-7777";
            s.TelefoneResidencial = "(21) 2621-0000";
            s.TelefoneContato = "(21) 99250-1655";
        }));

        var celular = p.Telecom.Single(t => t.Use == ContactPoint.ContactPointUse.Mobile);
        celular.Value.Should().Be("21988887777", "é o campo que a plataforma usa para o zap");

        p.Telecom.Single(t => t.Rank == 1).Value.Should().Be("21992501655");
        p.Telecom.Single(t => t.Use == ContactPoint.ContactPointUse.Home).Value.Should().Be("2126210000");
    }

    /// <summary>
    /// A regra que o Bernardo pediu — e que aqui é INVARIANTE, não um <c>if</c>: a conciliação
    /// roda como automação, e automação não encosta em telecom com o marcador de confirmado
    /// (ADR-0020 #2). Se alguém trocar para <c>manual: true</c> no mapper, este teste quebra.
    /// </summary>
    [Fact]
    public void Zap_do_ser_nao_substitui_telefone_verificado()
    {
        var noHub = new Patient();
        PatientMergeFhir.MarcarTelefoneConfirmado(noHub, "21999990000", DateTimeOffset.UtcNow);

        // O SER traz OUTRO número de whatsapp para a mesma pessoa.
        var doSer = SerPacienteFhirMapper.Construir(Solicitacao(s => s.TelefoneWhatsapp = "21988887777"));
        PatientMergeFhir.PreservarDoExistente(doSer, noHub);

        PatientMergeFhir.TelefoneConfirmado(doSer)!.Value.Numero
            .Should().Be("21999990000", "verificado é intocável por automação");
        doSer.Telecom.Single(t => t.Rank == 1).Value.Should().Be("21999990000",
            "e continua sendo o principal");
    }

    /// <summary>
    /// O caso que motivou <c>CompletarVazios</c>. O SER não pergunta endereço em toda solicitação
    /// (19.594 de 25.439 têm CEP). Sem o modo parcial, o Patient montado sem Address <b>apagaria</b>
    /// o endereço que o Salux trouxe — sem erro nenhum, e sem ninguém perceber.
    /// </summary>
    [Fact]
    public void Fonte_parcial_nao_apaga_o_que_o_hub_ja_tem()
    {
        var noHub = new Patient
        {
            BirthDate = "1985-04-12",
            Gender = AdministrativeGender.Female,
            Address = [new Address { City = "Maricá", PostalCode = "24900000", Use = Address.AddressUse.Home }],
            MaritalStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/v3-MaritalStatus", "M"),
        };

        // Solicitação sem endereço, sem nascimento e sem sexo — o SER não perguntou.
        var doSer = SerPacienteFhirMapper.Construir(Solicitacao(s =>
        {
            s.DataNascimento = null;
            s.Sexo = null;
            s.Cep = null; s.Logradouro = null; s.Bairro = null; s.MunicipioPaciente = null; s.Uf = null;
        }));
        doSer.Address.Should().BeNullOrEmpty("o mapper não inventa endereço vazio");

        PatientMergeFhir.CompletarVazios(doSer, noHub);

        doSer.Address.Should().ContainSingle().Which.City.Should().Be("Maricá");
        doSer.BirthDate.Should().Be("1985-04-12");
        doSer.Gender.Should().Be(AdministrativeGender.Female);
        doSer.MaritalStatus.Should().NotBeNull("fonte parcial só acrescenta");
    }

    /// <summary>
    /// O outro lado: completar não pode encobrir divergência. Quando o SER DIZ um nascimento
    /// diferente do hub, o valor tem de chegar intacto em ConciliarNascimento para ser congelado
    /// — se CompletarVazios sobrescrevesse, a divergência nunca seria vista.
    /// </summary>
    [Fact]
    public void Completar_nao_encobre_divergencia_de_nascimento()
    {
        var noHub = new Patient { BirthDate = "1985-04-12" };
        var doSer = SerPacienteFhirMapper.Construir(Solicitacao(s => s.DataNascimento = new DateOnly(1985, 4, 13)));

        PatientMergeFhir.CompletarVazios(doSer, noHub);

        doSer.BirthDate.Should().Be("1985-04-13", "o que a origem afirmou não pode ser silenciado");
    }

    /// <summary>
    /// ADR-0041 (adendo 04/08): só CPF VÁLIDO por dígito verificador vira identifier e âncora.
    /// "00000000000" é preenchimento de campo obrigatório — como chave nacional, fundiria pessoas.
    /// </summary>
    [Fact]
    public void Cpf_invalido_nao_vira_identifier_e_marca_identidade_incompleta()
    {
        var p = SerPacienteFhirMapper.Construir(Solicitacao(s => s.Cpf = "000.000.000-00"));

        p.Identifier.Should().NotContain(i => i.System == SerPacienteFhirMapper.SysCpf);
        p.Identifier.Should().ContainSingle(i => i.System == SerPacienteFhirMapper.SysCns);
        p.Meta!.Tag.Should().Contain(t =>
            t.System == "urn:smsmarica:qualidade" && t.Code == "identidade-incompleta");
    }

    [Fact]
    public void Cpf_valido_e_cns_entram_os_dois_como_identifier()
    {
        var p = SerPacienteFhirMapper.Construir(Solicitacao());

        p.Identifier.Should().Contain(i => i.System == SerPacienteFhirMapper.SysCpf && i.Value == "52998224725");
        p.Identifier.Should().Contain(i => i.System == SerPacienteFhirMapper.SysCns && i.Value == "700300908811839");
        p.Meta!.Tag.Should().BeNullOrEmpty("com CPF válido a identidade não é incompleta");
    }

    /// <summary>
    /// Paciente sem CPF entra pelo CNS e SAI MARCADO (ADR-0041). Medido no piloto de 10/08/2026:
    /// ancorar por CNS duplica pessoa em cerca de 4 de cada 10 casos — dos 40 criados assim, 17
    /// tinham no hub alguém de mesmo nome e mesma data de nascimento. O CNS não é uma chave por
    /// pessoa (há provisório da faixa 898… e gente com mais de um número).
    ///
    /// <para>A tag é o que torna essa dívida buscável em vez de invisível. Se ela parar de ser
    /// carimbada, os duplicados viram indistinguíveis do cadastro bom — e aí não há como voltar
    /// atrás depois.</para>
    /// </summary>
    [Fact]
    public void Sem_cpf_entra_pelo_cns_mas_declarado()
    {
        var p = SerPacienteFhirMapper.Construir(Solicitacao(s => s.Cpf = null));

        p.Identifier.Should().ContainSingle(i => i.System == SerPacienteFhirMapper.SysCns);
        p.Identifier.Should().NotContain(i => i.System == SerPacienteFhirMapper.SysCpf);
        p.Meta!.Tag.Should().Contain(t =>
            t.System == "urn:smsmarica:qualidade" && t.Code == "identidade-incompleta",
            "sem a tag a dívida some do radar");
    }

    /// <summary>
    /// Sexo em branco (4.881 das 25.439) é "não perguntaram", não "desconhecido": virar
    /// <c>unknown</c> rebaixaria no hub um sexo que outra base já sabia.
    /// </summary>
    [Fact]
    public void Sexo_em_branco_fica_nulo_para_o_hub_manter_o_que_tem()
    {
        SerPacienteFhirMapper.Construir(Solicitacao(s => s.Sexo = null)).Gender.Should().BeNull();
        SerPacienteFhirMapper.Construir(Solicitacao(s => s.Sexo = "M")).Gender
            .Should().Be(AdministrativeGender.Male);
    }
}
