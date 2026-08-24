using SMSMais.Core.Integracoes.Pep.Estrategias.Salux;

namespace SMSMais.Tests.Integracoes.Pep;

/// <summary>
/// Identifier com valor VAZIO derruba o recurso inteiro no hub.
///
/// <para>Bug real de 03/08: o mapper emitia <c>Identifier(CPF, "")</c> mesmo sem CPF, e o hub
/// respondia <b>400 — "'' is not a correct literal for a string. At
/// Patient.identifier[0].value"</b>. Resultado: os pacientes sem CPF, que o deploy daquele dia
/// passou a aceitar, NÃO entravam — 5 a 7 falhas por ciclo e zero registros gravados. O dano
/// foi nulo (nada entrou errado), mas a funcionalidade nascia morta.</para>
///
/// <para>A defesa é geral (<c>SemVazios</c>), não pontual: a mesma armadilha vale para RG, PIS,
/// passaporte e qualquer campo opcional que a origem devolva em branco.</para>
/// </summary>
public class PacienteSemCpfMapperTests
{
    private static SaluxFhirMapper Mapper() =>
        new("salux-hcml", "https://smsmarica.saude.marica/source/salux/salux-hcml");

    private static PacienteLinha Linha(string? cpf = null, string? cns = null, string? rg = null) =>
        new(Cd: 4242, Nome: "FULANO DE TAL", Social: null, FlagSocial: null,
            Nasc: "1980-05-10", Sexo: "M", Cpf: cpf, Cns: cns, Rg: rg, Orgao: null,
            Pis: null, Passaporte: null, Rne: null, Certidao: null, Sgh: null, Cem: null,
            Obito: null, Ativo: "S", Logr: null, NrLogr: null, Compl: null, Bairro: null,
            Cep: null, Ref: null, Ddd: null, Fone: null, DddResp: null, FoneResp: null,
            Email: null, Mae: null, Pai: null, Conjuge: null, Responsavel: null,
            GrauParentesco: null, CdCor: null, CdNacionalidade: null, Pais: null,
            Profissao: null, Ocupacao: null, Peso: null, Altura: null, Sangue: null, Rh: null,
            Etnia: null, EntradaPais: null, Cidade: null, UfSigla: null, EstadoCivilDs: null,
            InstrucaoDs: null, ReligiaoDs: null, BarreiraDs: null);

    [Fact]
    public void Sem_CPF__nao_emite_identifier_vazio()
    {
        var p = Mapper().BuildPatient(Linha(cpf: null));

        Assert.DoesNotContain(p.Identifier, i => string.IsNullOrWhiteSpace(i.Value));
        Assert.DoesNotContain(p.Identifier, i => i.System == SaluxFhirMapper.IdentCpf);
    }

    [Fact]
    public void Sem_CPF__mantem_o_identificador_LOCAL_que_e_a_chave_de_upsert()
    {
        var p = Mapper().BuildPatient(Linha(cpf: null));

        var local = Assert.Single(p.Identifier, i => i.System == SaluxFhirMapper.IdentSaluxPaciente);
        Assert.Equal("salux-hcml:4242", local.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("000")]      // lixo com menos de 11 dígitos
    public void CPF_em_branco_ou_curto_nao_vira_identifier_nacional(string cpf)
    {
        var p = Mapper().BuildPatient(Linha(cpf: cpf));

        Assert.DoesNotContain(p.Identifier, i => string.IsNullOrWhiteSpace(i.Value));
    }

    [Fact]
    public void Com_CPF__o_identificador_nacional_continua_presente()
    {
        var p = Mapper().BuildPatient(Linha(cpf: "123.456.789-09"));

        var cpf = Assert.Single(p.Identifier, i => i.System == SaluxFhirMapper.IdentCpf);
        Assert.Equal("12345678909", cpf.Value);
    }

    [Fact]
    public void Campos_opcionais_em_branco_tambem_nao_viram_identifier_vazio()
    {
        var p = Mapper().BuildPatient(Linha(cpf: "12345678909", cns: "   ", rg: ""));

        Assert.All(p.Identifier, i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.Value));
            Assert.False(string.IsNullOrWhiteSpace(i.System));
        });
    }
}
