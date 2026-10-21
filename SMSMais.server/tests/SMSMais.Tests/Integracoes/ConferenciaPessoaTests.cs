using FluentAssertions;
using SMSMais.Core.Integracoes.Cadastro;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// A régua de "são duas pessoas?". O que dói se quebrar para um lado: fundir de novo a mãe na ficha
/// da filha (caso de 21/10/2026). Para o outro: travar a mesma pessoa por uma grafia de recepção —
/// e aí a importação vira pendência à toa. Por isso metade dos casos aqui é "NÃO são diferentes".
/// </summary>
public class ConferenciaPessoaTests
{
    private static readonly DateOnly NascFilha = new(2017, 6, 12);
    private static readonly DateOnly NascMae = new(1994, 11, 23);

    [Fact]
    // O caso que abriu a guarda: ficha da filha (8 anos) achada pelo CPF/CNS da mãe.
    public void Mae_e_filha_com_o_mesmo_documento_sao_pessoas_diferentes() =>
        ConferenciaPessoa.SaoPessoasDiferentes("ALICE MOURA ROCHA", NascFilha, "JULIANA COSTA MOURA", NascMae)
            .Should().BeTrue();

    [Fact]
    // O sobrenome em comum (SA) não pode fazer a mãe passar pela filha.
    public void Sobrenome_de_familia_nao_basta() =>
        ConferenciaPessoa.SaoPessoasDiferentes("MARIA SILVA", new DateOnly(1980, 1, 1), "ANA SILVA", new DateOnly(2010, 1, 1))
            .Should().BeTrue();

    [Fact]
    // Nascimento digitado errado é o erro comum — e o upsert já manda para a arbitragem.
    public void Mesmo_nome_com_nascimento_diferente_nao_e_outra_pessoa() =>
        ConferenciaPessoa.SaoPessoasDiferentes("JULIANA COSTA MOURA", NascMae, "JULIANA COSTA MOURA", new DateOnly(1994, 11, 3))
            .Should().BeFalse();

    [Fact]
    // Nome social, casamento, nome trocado: com o mesmo nascimento, não dá para afirmar nada.
    public void Nome_diferente_com_o_mesmo_nascimento_nao_e_outra_pessoa() =>
        ConferenciaPessoa.SaoPessoasDiferentes("JOAO SILVA", NascMae, "MARIA SILVA", NascMae)
            .Should().BeFalse();

    [Theory]
    [InlineData("KAROLINE COSTA MOURA", "CAROLINE COSTA MOURA")]
    [InlineData("THAIS SOUZA", "TAIS SOUZA")]
    [InlineData("KAUANY LIMA", "KAUANE LIMA")]
    [InlineData("LUIZ CARLOS", "LUIS CARLOS")]
    [InlineData("ANNA BEATRIZ", "ANA BEATRIZ")]
    [InlineData("JOSÉ DA SILVA", "JOSE SILVA")]
    [InlineData("ELISANGELA MOURA", "ELIZANGELA MOURA")]
    // Grafias de recepção do MESMO primeiro nome — com nascimento diferente continua "mesma pessoa".
    public void Grafias_do_mesmo_nome_sao_compativeis(string a, string b) =>
        ConferenciaPessoa.SaoPessoasDiferentes(a, NascFilha, b, NascMae).Should().BeFalse();

    [Theory]
    [InlineData("GRACAS MARIA DA SILVA", "MARIA DAS GRACAS DA SILVA")]
    [InlineData("JOSE DA SILVA", "MARIA JOSE DA SILVA")]
    [InlineData("M. JOSE DA SILVA", "MARIA JOSE DA SILVA")]
    // Inversão, primeiro nome omitido, inicial: o primeiro nome de um aparece no outro.
    public void Nome_invertido_ou_abreviado_e_compativel(string a, string b) =>
        ConferenciaPessoa.SaoPessoasDiferentes(a, NascFilha, b, NascMae).Should().BeFalse();

    [Fact]
    // "RN DE…" é o recém-nascido — quem mais herda o documento da mãe no cadastro hospitalar.
    public void Recem_nascido_nao_e_a_mae_mesmo_com_o_nome_dela() =>
        ConferenciaPessoa.SaoPessoasDiferentes("RN DE JULIANA COSTA MOURA", new DateOnly(2026, 10, 1), "JULIANA COSTA MOURA", NascMae)
            .Should().BeTrue();

    [Fact]
    public void Dois_recem_nascidos_comparam_o_resto_do_nome() =>
        ConferenciaPessoa.SaoPessoasDiferentes("RN DE JULIANA MOURA", new DateOnly(2026, 10, 1), "RN JULIANA COSTA MOURA", new DateOnly(2026, 10, 2))
            .Should().BeFalse();

    [Fact]
    // Sem os dois sinais não se afirma nada: quem chama segue como sempre fez.
    public void Sem_nascimento_ou_sem_nome_nao_afirma()
    {
        ConferenciaPessoa.SaoPessoasDiferentes("ALICE MOURA ROCHA", null, "JULIANA COSTA MOURA", NascMae).Should().BeFalse();
        ConferenciaPessoa.SaoPessoasDiferentes("ALICE MOURA ROCHA", default(DateOnly), "JULIANA COSTA MOURA", NascMae).Should().BeFalse();
        ConferenciaPessoa.SaoPessoasDiferentes("ALICE MOURA ROCHA", new DateOnly(1800, 1, 1), "JULIANA COSTA MOURA", NascMae).Should().BeFalse();
        ConferenciaPessoa.SaoPessoasDiferentes(null, NascFilha, "JULIANA COSTA MOURA", NascMae).Should().BeFalse();
        ConferenciaPessoa.SaoPessoasDiferentes("  ", NascFilha, "JULIANA COSTA MOURA", NascMae).Should().BeFalse();
    }

    [Fact]
    // FHIR traz yyyy-MM-dd; o e-SUS também; o painel mostra dd/MM/yyyy.
    public void Nascimento_em_texto_e_lido_nos_formatos_que_aparecem()
    {
        ConferenciaPessoa.SaoPessoasDiferentes("ALICE MOURA ROCHA", "2017-06-12", "JULIANA COSTA MOURA", "23/11/1994").Should().BeTrue();
        ConferenciaPessoa.SaoPessoasDiferentes("ALICE MOURA ROCHA", "2017-06", "JULIANA COSTA MOURA", "1994-11-23").Should().BeFalse();
        ConferenciaPessoa.Data("1994-11-23T00:00:00").Should().Be(NascMae);
    }

    [Fact]
    public void Descrever_mostra_nome_e_nascimento()
    {
        ConferenciaPessoa.Descrever("JULIANA COSTA MOURA", NascMae).Should().Be("JULIANA COSTA MOURA (nasc. 23/11/1994)");
        ConferenciaPessoa.Descrever("JULIANA COSTA MOURA", null).Should().Be("JULIANA COSTA MOURA");
    }
}
