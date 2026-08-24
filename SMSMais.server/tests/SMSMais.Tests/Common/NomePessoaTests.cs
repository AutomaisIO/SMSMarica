using SMSMais.Core.Common.Texto;

namespace SMSMais.Tests.Common;

public class NomePessoaTests
{
    [Theory]
    [InlineData("MARIA DAS DORES DA SILVA", "Maria das Dores da Silva")]
    [InlineData("JOAO E SOUZA DOS SANTOS", "Joao e Souza dos Santos")]
    [InlineData("Ana Paula", "Ana Paula")]                    // já capitalizado passa intacto
    [InlineData("DE PAULA MENDES", "De Paula Mendes")]        // partícula na 1ª palavra sobe
    [InlineData("JEAN-PIERRE D'AVILA", "Jean-Pierre D'Avila")] // hífen e apóstrofo
    [InlineData("  josé   antônio  ", "José Antônio")]
    [InlineData("", "")]
    public void Capitalizar_formata_para_exibicao(string entrada, string esperado) =>
        Assert.Equal(esperado, NomePessoa.Capitalizar(entrada));

    [Theory]
    [InlineData("MARIA DAS DORES", "Maria")]
    [InlineData("joão silva", "João")]
    [InlineData("", "")]
    public void PrimeiroNome_capitaliza(string entrada, string esperado) =>
        Assert.Equal(esperado, NomePessoa.PrimeiroNome(entrada));
}
