using FluentAssertions;
using SMSMais.Core.Integracoes.Cadastro;
using SMSMais.Core.Integracoes.SisregWeb;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// O cache de cadastros da pré-carga. A sutileza toda está em distinguir três estados que um
/// dicionário comum confunde: <b>resolvido</b>, <b>a fonte disse que não existe</b> e <b>ninguém
/// perguntou</b>.
///
/// <para>Confundir os dois últimos custa caro nos dois sentidos: tratar "não perguntei" como "não
/// existe" faria a importação desistir de um paciente que o CADSUS conhece; tratar "não existe"
/// como "não perguntei" mandaria a importação repetir a consulta cara para ouvir o mesmo não.</para>
/// </summary>
public class CacheCadastroSerTests
{
    private const string Cns = "702802144727663";

    private static ConsultaCnsRespostaDto Ficha(string cns) =>
        new(cns, "02733913727", "FULANA DE TAL", "Feminino", new DateOnly(1965, 12, 29), "MAE DE TAL");

    [Fact]
    public void Sem_ninguem_ter_perguntado_o_cache_nao_responde()
    {
        var cache = new CacheCadastroSer();

        cache.TentarObter(Cns, out var r).Should().BeFalse("quem chama tem que ir à fonte");
        r.Should().BeNull();
    }

    [Fact]
    public void Resolvido_volta_pronto()
    {
        var cache = new CacheCadastroSer();
        cache.Guardar(Cns, Ficha(Cns));

        cache.TentarObter(Cns, out var r).Should().BeTrue();
        r!.Nome.Should().Be("FULANA DE TAL");
        cache.Resolvidos.Should().Be(1);
    }

    /// <summary>
    /// "A fonte respondeu que não existe" é resposta, não ausência: o cache confirma que sabe, e
    /// entrega null. É isso que evita repetir a consulta na importação.
    /// </summary>
    [Fact]
    public void Nao_encontrado_e_uma_resposta_e_nao_um_vazio()
    {
        var cache = new CacheCadastroSer();
        cache.GuardarNaoEncontrado(Cns);

        cache.TentarObter(Cns, out var r).Should().BeTrue("o cache SABE que não existe");
        r.Should().BeNull();
        cache.NaoEncontrados.Should().Be(1);
    }

    /// <summary>
    /// A importação passa o CNS como veio do TXT, e a pré-carga normaliza. Se as duas pontas não
    /// concordarem na chave, o cache nunca acerta e o paralelismo não serve para nada — sem
    /// nenhum sintoma além de continuar lento.
    /// </summary>
    [Theory]
    [InlineData("702 8021 4472 7663")]
    [InlineData("702.802.144.727-663")]
    public void Chave_ignora_formatacao(string comMascara)
    {
        var cache = new CacheCadastroSer();
        cache.Guardar(Cns, Ficha(Cns));

        cache.TentarObter(comMascara, out var r).Should().BeTrue();
        r!.Cns.Should().Be(Cns);
    }

    [Fact]
    public void Resolvido_depois_de_nao_encontrado_passa_a_valer()
    {
        var cache = new CacheCadastroSer();
        cache.GuardarNaoEncontrado(Cns);
        cache.Guardar(Cns, Ficha(Cns));

        cache.TentarObter(Cns, out var r).Should().BeTrue();
        r.Should().NotBeNull("a ficha é mais informativa que a ausência");
    }
}
