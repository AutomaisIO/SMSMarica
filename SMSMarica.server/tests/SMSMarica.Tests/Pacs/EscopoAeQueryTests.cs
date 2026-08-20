using SMSMarica.Core.Pacs;

namespace SMSMarica.Tests.Pacs;

/// <summary>
/// O recorte por unidade da listagem do PACS acontece reescrevendo a query string do QIDO-RS —
/// o dcm4chee não sabe o que é unidade, mas sabe de qual AE cada série veio, e esse AE é o
/// <c>Equipamento.IdentificadorDicom</c> que cadastramos.
///
/// <para>Cada caso aqui é um jeito de vazar exame de outra unidade: esquecer de descartar o
/// parâmetro que o cliente mandou (o proxy é passthrough puro da query string), escapar a
/// vírgula que faz a união dos AEs, ou aplicar o filtro numa rota que não é de listagem.</para>
/// </summary>
public class EscopoAeQueryTests
{
    private static readonly EscopoAeResultado DuasUnidades =
        new(SemRestricao: false, ["DO-CDT", "US_CMI"]);

    [Fact]
    public void Sem_restricao_nao_carimba_filtro()
    {
        var q = EscopoAeQuery.Aplicar("?limit=10&offset=0", EscopoAeResultado.Tudo);

        Assert.DoesNotContain(EscopoAeQuery.ChaveAeOrigem, q, StringComparison.Ordinal);
        Assert.Equal("?limit=10&offset=0", q);
    }

    [Fact]
    public void Com_restricao_acrescenta_os_aes_preservando_o_resto_do_filtro()
    {
        var q = EscopoAeQuery.Aplicar("?limit=10&00080061=MG,OT", DuasUnidades);

        Assert.Contains("limit=10", q, StringComparison.Ordinal);
        Assert.Contains("00080061=MG,OT", q, StringComparison.Ordinal);
        Assert.Contains($"{EscopoAeQuery.ChaveAeOrigem}=DO-CDT,US_CMI", q, StringComparison.Ordinal);
    }

    /// <summary>
    /// A vírgula é o "ou" do dcm4chee (verificado em produção: US02-CDT(9) + DO-CDT(143) = 152).
    /// Escapada junto com o valor, viraria %2C e o filtro casaria um AE literal "A,B" — zero
    /// resultados. Este teste existe para não perdermos isso num refactor de encoding.
    /// </summary>
    [Fact]
    public void Virgula_entre_aes_fica_literal()
    {
        var q = EscopoAeQuery.Aplicar(string.Empty, DuasUnidades);

        Assert.Equal($"?{EscopoAeQuery.ChaveAeOrigem}=DO-CDT,US_CMI", q);
        Assert.DoesNotContain("%2C", q, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("?SendingApplicationEntityTitleOfSeries=FDR-MAMO")]
    [InlineData("?sendingapplicationentitytitleofseries=FDR-MAMO")]
    [InlineData("?SendingApplicationEntityTitle=FDR-MAMO")]
    [InlineData("?77771037=FDR-MAMO")]
    public void Ae_mandado_pelo_cliente_e_descartado(string queryDoCliente)
    {
        var q = EscopoAeQuery.Aplicar(queryDoCliente, DuasUnidades);

        // Sobra só o nosso; o AE de outra unidade que o cliente tentou injetar some.
        Assert.DoesNotContain("FDR-MAMO", q, StringComparison.Ordinal);
        Assert.Equal($"?{EscopoAeQuery.ChaveAeOrigem}=DO-CDT,US_CMI", q);
    }

    [Fact]
    public void Ae_do_cliente_e_descartado_tambem_quando_o_escopo_e_irrestrito()
    {
        // Acesso global filtra pela unidade ativa, não digitando AE na URL.
        var q = EscopoAeQuery.Aplicar("?limit=5&SendingApplicationEntityTitleOfSeries=FDR-MAMO", EscopoAeResultado.Tudo);

        Assert.Equal("?limit=5", q);
    }

    /// <summary>
    /// O caso que falharia ABERTO se ninguém olhasse: medido no dcm4chee 5.34.3 de produção,
    /// <c>SendingApplicationEntityTitleOfSeries=</c> com valor vazio é tratado como AUSÊNCIA de
    /// filtro e devolve o acervo inteiro (2615 estudos) — enquanto um AE desconhecido devolve 0.
    /// Quem não tem acesso a unidade nenhuma passaria a ver a rede toda.
    /// </summary>
    [Fact]
    public void Sem_acesso_nao_produz_filtro_vazio_que_abriria_tudo()
    {
        Assert.True(EscopoAeResultado.Nada.SemAcesso);

        var q = EscopoAeQuery.Aplicar("?limit=10", EscopoAeResultado.Nada);

        var valor = q.Split($"{EscopoAeQuery.ChaveAeOrigem}=", StringSplitOptions.None)[1];
        Assert.NotEmpty(valor);
        Assert.DoesNotContain("&", valor, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("studies", true)]
    [InlineData("studies/count", true)]
    [InlineData("series", true)]
    [InlineData("instances", true)]
    // Rota de UM estudo: o recorte não se aplica (e o dcm4chee não aceitaria a chave ali).
    [InlineData("studies/1.2.840.113/series", false)]
    [InlineData("studies/1.2.840.113/series/1.2.3/instances/1.2.4/frames/1", false)]
    [InlineData("studies/1.2.840.113/metadata", false)]
    [InlineData(null, false)]
    public void Reconhece_quais_caminhos_sao_listagem(string? caminho, bool esperado) =>
        Assert.Equal(esperado, EscopoAeQuery.EhListagem(caminho));
}
