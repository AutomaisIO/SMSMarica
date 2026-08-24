using SMSMais.Core.Common.Documentos;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// A régua do CPF é o dígito verificador, não o comprimento (adendo do ADR-0041).
///
/// <para>Estes testes existem por um incidente real: um CPF de preenchimento (<c>00000000000</c>)
/// vindo da origem passava na checagem de "11 dígitos", virava identificador nacional e
/// <b>fundia duas pessoas</b> num único Patient. Por isso os casos degenerados são tão
/// importantes aqui quanto os CPFs bem formados.</para>
/// </summary>
public class CpfBrTests
{
    [Theory]
    // CPFs válidos conhecidos (DV confere), com e sem máscara.
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    public void Cpf_com_dv_correto_e_valido(string cpf) =>
        Assert.True(CpfBr.EhValido(cpf));

    [Theory]
    // O caso que motivou a regra: preenchimento clássico de campo obrigatório.
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("99999999999")]
    public void Repeticao_nao_e_cpf_mesmo_com_11_digitos(string cpf) =>
        Assert.False(CpfBr.EhValido(cpf));

    [Theory]
    [InlineData("52998224724")]      // último dígito trocado
    [InlineData("52998224715")]      // penúltimo trocado
    [InlineData("5299822472")]       // 10 dígitos
    [InlineData("529982247250")]     // 12 dígitos
    [InlineData("")]
    [InlineData(null)]
    public void Cpf_invalido_e_recusado(string? cpf) =>
        Assert.False(CpfBr.EhValido(cpf));

    [Fact]
    public void SoDigitos_remove_mascara_e_tolera_nulo()
    {
        Assert.Equal("52998224725", CpfBr.SoDigitos("529.982.247-25"));
        Assert.Equal("52998224725", CpfBr.SoDigitos(" 529 982 247 25 "));
        Assert.Equal(string.Empty, CpfBr.SoDigitos(null));
    }
}
