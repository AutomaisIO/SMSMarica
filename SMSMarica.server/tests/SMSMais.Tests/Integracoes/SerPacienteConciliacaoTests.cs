using FluentAssertions;
using Hl7.Fhir.Model;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.Ser.Pacientes;
using SMSMarica.Data.Entities.Ser;

namespace SMSMais.Tests.Integracoes;

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
    /// <b>O incidente de 10/08/2026, virado teste.</b> A paciente tinha telefone no hub — um vindo
    /// do Salux, três trazidos por uma solicitação anterior do próprio SER. Uma OUTRA solicitação
    /// da mesma pessoa, com o campo telefone em branco, conciliou por cima e levou todos embora:
    /// 2.771 pacientes ficaram sem número nenhum. Formulário em branco é "não perguntaram", nunca
    /// "não tem".
    /// </summary>
    [Fact]
    public void Solicitacao_sem_telefone_nao_apaga_o_telefone_do_hub()
    {
        var noHub = new Patient();
        PatientMergeFhir.AplicarContatos(noHub,
            principal: "21997150212", celular: "21967156518", residencial: "2197173488",
            email: "ester@exemplo.com", manual: false);

        // A solicitação que o SER trouxe depois não perguntou telefone nenhum.
        var doSer = SerPacienteFhirMapper.Construir(Solicitacao(s =>
        {
            s.TelefoneContato = null; s.TelefoneWhatsapp = null; s.TelefoneResidencial = null;
        }));
        doSer.Telecom.Should().BeNullOrEmpty("o mapper não inventa telefone vazio");

        PatientMergeFhir.PreservarDoExistente(doSer, noHub);

        doSer.Telecom.Where(t => t.System == ContactPoint.ContactPointSystem.Phone)
            .Select(t => t.Value).Should()
            .BeEquivalentTo(["21997150212", "21967156518", "2197173488"]);
        doSer.Telecom.Single(t => t.Rank == 1).Value.Should().Be("21997150212",
            "sem principal na origem, o do hub continua principal");
        doSer.Telecom.Should().Contain(t => t.System == ContactPoint.ContactPointSystem.Email,
            "e-mail é contato pela mesma régua");
    }

    /// <summary>
    /// A marca de "sem CPF" fala sobre a LEITURA daquela base, não sobre a pessoa. Quando a união
    /// de identifiers traz um CPF válido — de outra base ou do próprio hub — ela tem de sair,
    /// senão a busca por dívida de identidade (<c>_tag</c>) devolve gente que tem CPF: eram 1.445
    /// em 10/08/2026.
    /// </summary>
    [Fact]
    public void Marca_de_identidade_incompleta_sai_quando_o_cpf_aparece()
    {
        // Solicitação sem CPF: o mapper carimba, e está certo — ele só viu o CNS.
        var doSer = SerPacienteFhirMapper.Construir(Solicitacao(s => s.Cpf = null));
        SMSMarica.Core.Pacientes.PacienteFhirMapper.TemIdentidadeIncompleta(doSer).Should().BeTrue();

        // A união de identifiers traz o CPF que o Salux já tinha posto no hub.
        doSer.Identifier.Add(new Identifier(PatientMergeFhir.SystemCpf, "52998224725"));
        PatientMergeFhir.RevisarIdentidadeIncompleta(doSer);

        SMSMarica.Core.Pacientes.PacienteFhirMapper.TemIdentidadeIncompleta(doSer).Should().BeFalse();
    }

    /// <summary>
    /// E não sai por CPF de mentira: "00000000000" é preenchimento de campo obrigatório, não
    /// chave nacional — desmarcar por causa dele esconderia a dívida em vez de saldá-la.
    /// </summary>
    [Fact]
    public void Marca_nao_sai_com_cpf_invalido()
    {
        var doSer = SerPacienteFhirMapper.Construir(Solicitacao(s => s.Cpf = null));
        doSer.Identifier.Add(new Identifier(PatientMergeFhir.SystemCpf, "00000000000"));

        PatientMergeFhir.RevisarIdentidadeIncompleta(doSer);

        SMSMarica.Core.Pacientes.PacienteFhirMapper.TemIdentidadeIncompleta(doSer).Should().BeTrue();
    }

    /// <summary>
    /// O contato preservado não pode disputar o slot principal com quem a origem acabou de
    /// afirmar — nem voltar duplicado a cada importação.
    /// </summary>
    [Fact]
    public void Telefone_herdado_do_hub_vira_secundario_e_nao_duplica()
    {
        var noHub = new Patient();
        PatientMergeFhir.AplicarContatos(noHub, principal: "2197173488", celular: null,
            residencial: null, email: null, manual: false);

        var doSer = SerPacienteFhirMapper.Construir(Solicitacao(s => s.TelefoneContato = "21997150212"));
        PatientMergeFhir.PreservarDoExistente(doSer, noHub);

        doSer.Telecom.Single(t => t.Rank == 1).Value.Should().Be("21997150212",
            "o principal é de quem falou agora");
        doSer.Telecom.Should().Contain(t => t.Value == "2197173488", "mas o antigo continua lá");

        // Segunda passagem sobre o resultado da primeira: o mesmo número não pode entrar de novo.
        PatientMergeFhir.PreservarDoExistente(doSer, noHub);
        doSer.Telecom.Count(t => t.Value == "2197173488").Should().Be(1);
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
