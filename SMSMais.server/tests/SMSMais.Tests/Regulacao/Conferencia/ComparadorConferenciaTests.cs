using SMSMais.Core.Integracoes.SisregWeb.Chave;
using SMSMais.Core.Regulacao.Conferencia;

namespace SMSMais.Tests.Regulacao.Conferencia;

/// <summary>
/// A régua da conferência do número digitado (pedido do Bernardo, 22/10/2026). O que prende: no NAR,
/// o CNES com que o pedido entrou no SISREG tem de ser o da unidade "em nome de"; diferença real
/// aparece como diferença, e o que o sistema não mostra não vira item.
/// </summary>
public class ComparadorConferenciaTests
{
    private static NossoPedido Nosso(
        string? cnes = "1234567", string? cid = "C50.9", string? medicoCpf = "52998224725") => new(
        PacienteNome: "MARIA DA SILVA",
        PacienteCpf: "52998224725",
        PacienteCns: "700000000000000",
        ProcedimentoRotulos: ["MAMOGRAFIA BILATERAL", "MAMOGRAFIA"],
        ProcedimentoCodigos: ["0701"],
        Cid: cid,
        UnidadeNome: "AMBULATORIO PERICLES",
        UnidadeCnes: cnes,
        MedicoNome: "JOAO MEDICO",
        MedicoCpf: medicoCpf,
        Risco: null);

    private static PedidoNoSistema Lido(string? cnes = "1234567", string? cid = "C509 - NEOPLASIA MALIGNA DA MAMA") => new(
        Situacao: "SOLICITAÇÃO / PENDENTE / REGULADOR",
        PacienteNome: "Maria da Silva",
        PacienteCpf: null,
        PacienteCns: "700 0000 0000 0000",
        Procedimentos: [new ProcedimentoNoSistema("MAMOGRAFIA BILATERAL", "0701")],
        Cid: cid,
        UnidadeSolicitante: "AMBULATORIO PERICLES",
        UnidadeSolicitanteCnes: cnes,
        MedicoNome: "JOAO MEDICO",
        MedicoCpf: "529.982.247-25",
        Risco: "AMARELO",
        DataSolicitacao: "22/10/2026");

    [Fact]
    public void Pedido_igual_nao_tem_divergencia()
    {
        var itens = ComparadorConferencia.Comparar(Nosso(), Lido());

        itens.Should().NotContain(i => i.Resultado == ResultadoItemConferencia.Diferente);
        itens.Should().Contain(i => i.Campo == "unidade" && i.Resultado == ResultadoItemConferencia.Igual);
        itens.Should().Contain(i => i.Campo == "procedimento" && i.Resultado == ResultadoItemConferencia.Igual);
    }

    [Fact]
    public void Nar_lancado_com_o_cnes_de_outra_unidade_e_divergencia()
    {
        // O técnico entrou no SISREG com o login da própria unidade em vez da "em nome de".
        var itens = ComparadorConferencia.Comparar(Nosso(cnes: "1234567"), Lido(cnes: "7654321"));

        itens.Single(i => i.Campo == "unidade").Resultado.Should().Be(ResultadoItemConferencia.Diferente);
    }

    [Fact]
    public void Cid_diferente_aparece_e_categoria_basta_quando_um_lado_nao_tem_subcategoria()
    {
        ComparadorConferencia.Comparar(Nosso(cid: "C50"), Lido(cid: "C50.9"))
            .Single(i => i.Campo == "cid").Resultado.Should().Be(ResultadoItemConferencia.Igual);

        ComparadorConferencia.Comparar(Nosso(cid: "C50.9"), Lido(cid: "D05.1"))
            .Single(i => i.Campo == "cid").Resultado.Should().Be(ResultadoItemConferencia.Diferente);
    }

    [Fact]
    public void O_que_o_sistema_nao_mostra_nao_vira_item()
    {
        // A grade do SER: sem risco, sem médico, sem unidade com CNES.
        var doSer = Lido() with
        {
            UnidadeSolicitante = null, UnidadeSolicitanteCnes = null, MedicoNome = null, MedicoCpf = null, Risco = null,
        };

        var itens = ComparadorConferencia.Comparar(Nosso(), doSer);

        itens.Select(i => i.Campo).Should().NotContain(["unidade", "medico", "risco"]);
    }

    [Fact]
    public void Risco_fica_lado_a_lado_sem_veredito()
    {
        var itens = ComparadorConferencia.Comparar(Nosso() with { Risco = "URGENCIA" }, Lido());

        itens.Single(i => i.Campo == "risco").Resultado.Should().Be(ResultadoItemConferencia.Informativo);
    }

    /// <summary>A ficha do SISREG com os rótulos da captura real (valores inventados).</summary>
    private const string Ficha = """
        <html><body><div class="fichaAmbulatorial"><div id="fichaAmbulatorial"><table class='table_listagem'>
        <tr><td><b>Chave de Confirma&#231;&#227;o:</b></td></tr>
        <tr><td><b>12345</b></td></tr>
        <tbody class="FichaCompleta">
        <tr><td colspan='6' class='td_titulo_tabela'>UNIDADE SOLICITANTE</td></tr>
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>Unidade Solicitante:</b></td><td><b>C&#243;d. CNES:</b></td><td><b>Op. Solicitante:</b></td><td><b>Op. Videofonista:</b></td></tr>
        <tr><td colspan='3'>AMBULATORIO PERICLES</td><td>1234567</td><td>OPERADOR.EXEMPLO</td><td>---</td></tr>
        </tbody>
        <tr><td colspan='6' class='td_titulo_tabela'>UNIDADE EXECUTANTE</td></tr>
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>Unidade Executante:</b></td><td colspan='3'><b>C&#243;d. CNES:</b></td></tr>
        <tr><td colspan='3'>HOSPITAL DE EXEMPLO</td><td colspan='3'>9999999</td></tr>
        <tr><td colspan='6' class='td_titulo_tabela'>Dados do Paciente</td></tr>
        <tbody class="FichaCompleta">
        <tr bgcolor='#F3F3F3'><td colspan='6'><b>CNS:</b></td></tr>
        <tr><td colspan='6'><font color="blue">700000000000000</font></td></tr>
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>Nome do Paciente</b></td><td colspan='3'><b>Nome Social/Apelido:</b></td></tr>
        <tr><td colspan='3'>MARIA DA SILVA</td><td colspan='3'>---</td></tr>
        </tbody>
        <tr><td colspan='6' class='td_titulo_tabela'>Dados da Solicita&ccedil;&atilde;o</td></tr>
        <tbody class="FichaCompleta">
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>C&#243;digo da Solicita&#231;&#227;o:</b></td><td colspan='3'><b>Situa&#231;&#227;o Atual:</b></td></tr>
        <tr><td colspan='3'><font color="blue">670000001</font></td><td colspan='3'>SOLICITAÇÃO / PENDENTE / REGULADOR</td></tr>
        <tr bgcolor='#F3F3F3'><td colspan='2'><b>CPF do M&#233;dico Solicitante:</b></td><td colspan='2'><b>CRM:</b></td><td colspan='2'><b>Nome M&#233;dico Solicitante:</b></td></tr>
        <tr><td colspan='2'>52998224725</td><td colspan='2'>12345</td><td colspan='2'>JOAO MEDICO</td></tr>
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>CID:</b></td><td colspan='1'><b>Risco:</b></td><td colspan='2'><b>Data Solicita&#231;&#227;o:</b></td></tr>
        <tr><td colspan='3'>C509</td><td colspan='1'>AMARELO</td><td colspan='2'>22/10/2026</td></tr>
        </tbody>
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>Procedimentos Solicitados:</b></td><td colspan='1'><b>C&#243;d. Unificado:</b></td><td colspan='2'><b>C&#243;d. Interno:</b></td></tr>
        <tr><td colspan='3'>MAMOGRAFIA BILATERAL</td><td colspan='1'>0204030188</td><td colspan='2'>0701</td></tr>
        </table></div></div></body></html>
        """;

    [Fact]
    public void Ficha_do_sisreg_vira_pedido_com_o_cnes_da_unidade_solicitante_e_nao_da_executante()
    {
        var pedido = LeitorPedidoSisreg.DaFicha(FichaSisregHtmlParser.Ler(Ficha)!);

        pedido.UnidadeSolicitanteCnes.Should().Be("1234567");
        pedido.UnidadeSolicitante.Should().Be("AMBULATORIO PERICLES");
        pedido.PacienteCns.Should().Be("700000000000000");
        pedido.PacienteNome.Should().Be("MARIA DA SILVA");
        pedido.MedicoCpf.Should().Be("52998224725");
        pedido.Cid.Should().Be("C509");
        pedido.Risco.Should().Be("AMARELO");
        pedido.Procedimentos.Should().ContainSingle().Which.Should().Be(new ProcedimentoNoSistema("MAMOGRAFIA BILATERAL", "0701"));

        // E a régua inteira passa limpa contra o pedido igual.
        ComparadorConferencia.Comparar(Nosso(), pedido)
            .Should().NotContain(i => i.Resultado == ResultadoItemConferencia.Diferente);
    }
}
