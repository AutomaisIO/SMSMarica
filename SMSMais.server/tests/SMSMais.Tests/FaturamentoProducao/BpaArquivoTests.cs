using SMSMais.Core.FaturamentoProducao;

namespace SMSMais.Tests.FaturamentoProducao;

public class BpaArquivoTests
{
    private static BpaLinhaFonte Fonte(
        string cns = "700000000000001",
        string cbo = "223505",
        string sigtap = "0205020160",
        string? cnsPac = "708000000000002",
        string? cpfPac = null,
        char sexo = 'F') =>
        new(
            SolicitacaoId: Guid.NewGuid(),
            Cnes: "3132358",
            Competencia: 202610,
            CnsProfissional: cns,
            Cbo: cbo,
            DataAtendimento: new DateOnly(2026, 10, 22),
            ProcedimentoSigtap: sigtap,
            CnsPaciente: cnsPac,
            CpfPaciente: cpfPac,
            Sexo: sexo,
            Ibge6: "330270",
            Cid: "N946",
            DataNascimento: new DateOnly(1979, 6, 28),
            Quantidade: 1,
            NomePaciente: "MARIA DAS DORES",
            Raca: "99",
            Nacionalidade: "010",
            Cep: "24900000",
            Logradouro: "RUA TAL");

    [Theory]
    [InlineData(0, 0, 1111)]
    [InlineData(1110, 0, 2221)]
    [InlineData(1111, 0, 1111)]
    [InlineData(1112, 0, 1112)]
    [InlineData(205020160, 1, 1111 + (205020160 + 1) % 1111)]
    public void Controle_segue_a_formula_do_datasus(long somaProc, long somaQt, int esperado) =>
        Assert.Equal(esperado, BpaArquivo.Controle(somaProc, somaQt));

    [Fact]
    public void Gera_header_de_126_e_linha_de_350_colunas()
    {
        var res = BpaArquivo.Gerar([Fonte()]);
        var linhas = res.Texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(126, linhas[0].Length);
        Assert.Equal(350, linhas[1].Length);
        Assert.StartsWith("01#BPA#202610", linhas[0]);
        Assert.EndsWith("D05.00", linhas[0]);
    }

    [Fact]
    public void Linha_posiciona_os_campos_nas_colunas_certas()
    {
        var l = BpaArquivo.Gerar([Fonte()]).Texto.Split("\r\n")[1];

        Assert.Equal("03", l.Substring(0, 2));            // prd_ident
        Assert.Equal("3132358", l.Substring(2, 7));       // prd_cnes
        Assert.Equal("202610", l.Substring(9, 6));        // prd_cmp
        Assert.Equal("700000000000001", l.Substring(15, 15)); // prd_cnsmed
        Assert.Equal("223505", l.Substring(30, 6));       // prd_cbo
        Assert.Equal("20261022", l.Substring(36, 8));     // prd_dtaten
        Assert.Equal("001", l.Substring(44, 3));          // prd_flh
        Assert.Equal("01", l.Substring(47, 2));           // prd_seq
        Assert.Equal("0205020160", l.Substring(49, 10));  // prd_pa
        Assert.Equal("F", l.Substring(74, 1));            // prd_sexo
        Assert.Equal("330270", l.Substring(75, 6));       // prd_ibge
        Assert.Equal("N946", l.Substring(81, 4));         // prd_cid
        Assert.Equal("01", l.Substring(94, 2));           // prd_caten
        Assert.Equal("BPA", l.Substring(109, 3));         // prd_org
        Assert.Equal("MARIA DAS DORES".PadRight(30), l.Substring(112, 30)); // prd_nmpac
        Assert.Equal("19790628", l.Substring(142, 8));    // prd_dtnasc
        Assert.Equal("99", l.Substring(150, 2));          // prd_raca
        Assert.Equal("010", l.Substring(156, 3));         // prd_nac
        Assert.Equal("N", l.Substring(349, 1));           // prd_situacao_rua
    }

    [Fact]
    public void Paciente_com_CNS_usa_cnspac_e_deixa_cpf_em_branco()
    {
        var l = BpaArquivo.Gerar([Fonte(cnsPac: "708000000000002", cpfPac: null)]).Texto.Split("\r\n")[1];
        Assert.Equal("708000000000002", l.Substring(59, 15)); // prd_cnspac preenchido
        Assert.Equal(new string(' ', 11), l.Substring(338, 11)); // prd_cpf em branco
    }

    [Fact]
    public void Paciente_sem_CNS_usa_cpf_e_deixa_cnspac_em_branco()
    {
        var l = BpaArquivo.Gerar([Fonte(cnsPac: null, cpfPac: "11257225766")]).Texto.Split("\r\n")[1];
        Assert.Equal(new string(' ', 15), l.Substring(59, 15)); // prd_cnspac em branco
        Assert.Equal("11257225766", l.Substring(338, 11));      // prd_cpf preenchido
    }

    [Fact]
    public void Folha_transborda_em_99_linhas_do_mesmo_profissional()
    {
        var cns = "700000000000001";
        var linhas = Enumerable.Range(0, 100).Select(_ => Fonte(cns: cns)).ToList();
        var res = BpaArquivo.Gerar(linhas);

        Assert.Equal(100, res.Linhas);
        Assert.Equal(2, res.Folhas); // 99 na folha 001, a 100ª na folha 002
        var corpo = res.Texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
        Assert.Equal("00199", corpo[98].Substring(44, 5));  // 99ª linha: folha 001, seq 99
        Assert.Equal("00201", corpo[99].Substring(44, 5));  // 100ª linha: folha 002, seq 01
    }

    [Fact]
    public void Profissionais_diferentes_vao_para_folhas_diferentes()
    {
        var res = BpaArquivo.Gerar([
            Fonte(cns: "700000000000001"),
            Fonte(cns: "700000000000009"),
        ]);
        Assert.Equal(2, res.Folhas);
        Assert.Equal(2, res.Linhas);
    }

    [Fact]
    public void Header_conta_linhas_e_folhas()
    {
        var res = BpaArquivo.Gerar([Fonte(), Fonte()]);
        var header = res.Texto.Split("\r\n")[0];
        Assert.Equal("000002", header.Substring(13, 6)); // nº de linhas
        Assert.Equal("000001", header.Substring(19, 6)); // nº de folhas (mesmo prof)
    }
}
