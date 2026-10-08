using FluentAssertions;
using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Regulacao.EnvioSer;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Regulacao.EnvioSer;

/// <summary>
/// O painel do paciente digitado com o nosso cadastro quando o SERNIT não conhece o paciente
/// (ADR-0069). Os rótulos, os ids e as máscaras são os da aba real do SERNIT (08/10/2026).
/// </summary>
public class PacienteNaTelaTests
{
    internal static PacienteDto Paciente(
        string nome = "Maria da Silva", string cpf = "12345678909", Sexo sexo = Sexo.Feminino,
        DateOnly? nascimento = null, string? mae = "Joana da Silva", EnderecoDto? endereco = null,
        string? celular = "+55 21 99876-5432", string? residencial = null, RacaCor raca = RacaCor.Parda) =>
        new(Guid.NewGuid(), nome, cpf, "700000000000000", 0, 0, true, DateTime.UtcNow,
            null, nascimento ?? new DateOnly(1980, 3, 7), sexo, default, raca, default, null, null, "Brasileira",
            mae, null, null,
            endereco ?? new EnderecoDto("24900-000", "Rua das Flores", "12", "casa 2", "Centro", "Maricá", "RJ", null),
            celular, celular, residencial, null, null,
            null, null, default, default, [], [], [], [], null,
            null, null);

    /// <summary>O painel vazio e aberto, como o SERNIT devolve para paciente que não conhece.</summary>
    internal static List<SerCampoPacienteDto> PainelVazio() =>
    [
        Texto("form0:nome", "Nome"),
        Texto("form0:cpf", "CPF"),
        new("form0:sexo", "Sexo", null, "select", false, true, [new("M", "Masculino"), new("F", "Feminino")]),
        Texto("form0:dataNascimento", "Data de Nascimento"),
        Texto("form0:nomeMae", "Nome da Mãe"),
        Texto("form0:logradouro", "Logradouro"),
        Texto("form0:numero", "Número"),
        Texto("form0:complemento", "Complemento"),
        Texto("form0:cep", "CEP"),
        new("form0:uf", "UF", null, "select", false, true, [new("58", "RIO DE JANEIRO")]),
        new("form0:municipio", "Município", null, "select", false, true, []),
        Texto("form0:bairro", "Bairro"),
        Texto("form0:j_id152", "Telefone Residencial"),
        Texto("form0:j_id157", "Telefone Celular"),
        Texto("form0:j_id159", "Telefone Comercial"),
        new("form0:raca", "Raça", null, "select", false, true,
            [new("BRANCA", "Caucasiano"), new("PRETA", "Negra"), new("PARDA", "Parda"), new("SEM_INFORMACAO", "Sem informacao")]),
    ];

    private static SerCampoPacienteDto Texto(string campo, string rotulo, string? valor = null, bool editavel = true) =>
        new(campo, rotulo, valor, "text", false, editavel, null);

    [Fact]
    public void Painel_vazio_recebe_o_nosso_cadastro_com_as_mascaras_da_tela()
    {
        var m = PacienteNaTela.Montar(Paciente(), PainelVazio());

        m.Faltando.Should().BeEmpty();
        var campos = m.Campos.ToDictionary(c => c.Campo, c => c.Valor);
        campos["form0:nome"].Should().Be("MARIA DA SILVA");
        campos["form0:cpf"].Should().Be("123.456.789-09");
        campos["form0:sexo"].Should().Be("F");
        campos["form0:dataNascimento"].Should().Be("07/03/1980");
        campos["form0:nomeMae"].Should().Be("JOANA DA SILVA");
        campos["form0:cep"].Should().Be("24900-000");
        campos["form0:numero"].Should().Be("12");
        campos["form0:bairro"].Should().Be("CENTRO");
        campos["form0:j_id157"].Should().Be("(21)99876-5432", "o celular é achado pelo RÓTULO e o 55 do país sai");
        campos["form0:raca"].Should().Be("PARDA");
        campos.Should().NotContainKey("form0:uf", "UF e município dependem do onchange — quem faz é o envio");
        campos.Should().NotContainKey("form0:j_id152", "sem telefone residencial no cadastro, o campo fica vazio");
    }

    [Fact]
    public void Sem_nascimento_ou_sexo_no_nosso_cadastro_o_envio_para()
    {
        var m = PacienteNaTela.Montar(Paciente(sexo: Sexo.NaoInformado, nascimento: null) with { DataNascimento = null },
            PainelVazio());

        m.Faltando.Should().BeEquivalentTo("Sexo", "Data de Nascimento");
    }

    [Fact]
    public void Campo_que_o_sistema_ja_trouxe_nao_e_sobrescrito()
    {
        var painel = PainelVazio();
        painel[0] = Texto("form0:nome", "Nome", "MARIA DA SILVA", editavel: false);
        painel[1] = Texto("form0:cpf", "CPF", "123.456.789-09");

        var m = PacienteNaTela.Montar(Paciente(), painel);

        m.Campos.Select(c => c.Campo).Should().NotContain(["form0:nome", "form0:cpf"]);
        m.Faltando.Should().BeEmpty();
    }

    [Fact]
    public void Nome_da_mae_longo_e_cortado_no_limite_da_tela_com_aviso()
    {
        var mae = new string('A', 60);
        var m = PacienteNaTela.Montar(Paciente(mae: mae), PainelVazio());

        m.Campos.Single(c => c.Campo == "form0:nomeMae").Valor.Should().HaveLength(50);
        m.Avisos.Should().ContainSingle(a => a.Contains("mãe"));
    }

    [Theory]
    [InlineData("RJ", "RIO DE JANEIRO")]
    [InlineData("sp", "SAO PAULO")]
    [InlineData("Rio de Janeiro", "Rio de Janeiro")]
    public void Uf_vira_nome_por_extenso(string uf, string esperado) =>
        PacienteNaTela.NomeDaUf(uf).Should().Be(esperado);
}
