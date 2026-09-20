using System.Text.RegularExpressions;
using SMSMais.Core.Ouvidoria;

namespace SMSMais.Tests.Ouvidoria;

/// <summary>
/// Protocolo público e código de acesso (ADR-0060; plano §2.1.3). Regressões impedidas: código
/// com 0/O/1/I (o cidadão dita por telefone e confunde); hash não determinístico (o cidadão
/// nunca mais entraria); <c>Confere</c> aceitando hash nulo (anônima "acessável" por qualquer
/// código); protocolo sem os seis dígitos com zero à esquerda.
/// </summary>
public partial class OuvidoriaProtocoloTests
{
    [GeneratedRegex("^[A-HJ-NP-Z2-9]{8}$")]
    private static partial Regex AlfabetoPermitido();

    [Fact]
    public void Codigo_de_acesso_tem_8_caracteres_so_do_alfabeto_permitido()
    {
        for (var i = 0; i < 200; i++)
        {
            var codigo = OuvidoriaProtocolo.GerarCodigoAcesso();

            Assert.Equal(OuvidoriaProtocolo.TamanhoCodigo, codigo.Length);
            Assert.Matches(AlfabetoPermitido(), codigo);
            Assert.DoesNotContain('0', codigo);
            Assert.DoesNotContain('O', codigo);
            Assert.DoesNotContain('1', codigo);
            Assert.DoesNotContain('I', codigo);
        }
    }

    [Fact]
    public void Codigos_gerados_em_sequencia_sao_diferentes()
    {
        var codigos = Enumerable.Range(0, 50).Select(_ => OuvidoriaProtocolo.GerarCodigoAcesso()).ToHashSet();

        Assert.True(codigos.Count > 45, "colisoes demais para um gerador criptografico");
    }

    [Fact]
    public void Hash_e_deterministico_e_hexadecimal_de_64_caracteres()
    {
        var a = OuvidoriaProtocolo.Hash("ABCD2345");
        var b = OuvidoriaProtocolo.Hash("ABCD2345");

        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
        Assert.Matches("^[0-9a-f]{64}$", a);
        Assert.NotEqual(a, OuvidoriaProtocolo.Hash("ABCD2346"));
    }

    /// <summary>O cidadão digita minúsculo, com espaço ou hífen: o hash tem que ser o mesmo.</summary>
    [Fact]
    public void Hash_normaliza_caixa_espacos_e_hifens()
    {
        var referencia = OuvidoriaProtocolo.Hash("ABCD2345");

        Assert.Equal(referencia, OuvidoriaProtocolo.Hash("abcd2345"));
        Assert.Equal(referencia, OuvidoriaProtocolo.Hash(" abcd-2345 "));
        Assert.Equal(referencia, OuvidoriaProtocolo.Hash("ABCD 2345"));
    }

    [Fact]
    public void Confere_true_para_o_codigo_certo_e_false_para_o_errado()
    {
        var codigo = OuvidoriaProtocolo.GerarCodigoAcesso();
        var hash = OuvidoriaProtocolo.Hash(codigo);

        Assert.True(OuvidoriaProtocolo.Confere(codigo, hash));
        Assert.True(OuvidoriaProtocolo.Confere(codigo.ToLowerInvariant(), hash));
        Assert.True(OuvidoriaProtocolo.Confere(codigo, hash.ToUpperInvariant()));
        Assert.False(OuvidoriaProtocolo.Confere("ZZZZZZZZ", hash));
        Assert.False(OuvidoriaProtocolo.Confere(codigo + "X", hash));
    }

    /// <summary>Anônima não tem código: hash nulo NUNCA confere, seja qual for o código.</summary>
    [Fact]
    public void Confere_false_para_hash_nulo_ou_codigo_vazio()
    {
        Assert.False(OuvidoriaProtocolo.Confere("ABCD2345", null));
        Assert.False(OuvidoriaProtocolo.Confere("ABCD2345", ""));
        Assert.False(OuvidoriaProtocolo.Confere("ABCD2345", "   "));
        Assert.False(OuvidoriaProtocolo.Confere(null, OuvidoriaProtocolo.Hash("ABCD2345")));
        Assert.False(OuvidoriaProtocolo.Confere("", OuvidoriaProtocolo.Hash("ABCD2345")));
    }

    [Fact]
    public void Formatar_gera_ano_hifen_seis_digitos()
    {
        Assert.Equal("2026-000007", OuvidoriaProtocolo.Formatar(2026, 7));
        Assert.Equal("2026-123456", OuvidoriaProtocolo.Formatar(2026, 123456));
        Assert.Equal("2027-1000000", OuvidoriaProtocolo.Formatar(2027, 1_000_000)); // D6 não trunca acima de 999999
    }

    [Fact]
    public void Normalizar_protocolo_faz_trim_e_maiusculas()
    {
        Assert.Equal("2026-000007", OuvidoriaProtocolo.Normalizar("  2026-000007 "));
        Assert.Equal("", OuvidoriaProtocolo.Normalizar(null));
        Assert.Equal("ABCD2345", OuvidoriaProtocolo.NormalizarCodigo(" ab-cd 2345 "));
    }
}
