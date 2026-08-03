using SMSMarica.Core.Integracoes.SisregWeb.Varredura;

namespace SMSMarica.Tests.Integracoes.Sisreg;

/// <summary>
/// Parser da listagem de agenda do SISREG.
///
/// <para><b>Sobre a fixture:</b> reproduz a marcação real observada em
/// <c>Automais.SISREG/capturas/_agenda_B.html</c> — as tags em caixa alta, os atributos com aspas
/// simples, as entidades HTML, o <c>&amp;nbsp;</c> dentro da Situação e o <c>rowspan</c> da célula
/// de procedimento. Os <b>dados</b> são sintéticos de propósito: a captura verdadeira contém
/// nome, CNS e telefone de pacientes reais e é gitignored.</para>
/// </summary>
public class VarreduraAgendaParserTests
{
    private const string Cabecalho = """
        <CENTER><TABLE class='table_listagem'>
        <TR><TD colspan='13' class='td_titulo_tabela'>Propriedades da Agenda</TD></TR>
        <TR><TD colspan='1'>Unidade Executante:</TD><TD colspan='12'>CDT DR ALBERTO LUIS MACHADO BORGES (3132358)</TD></TR>
        <TR><TD colspan='1'>Per&iacute;odo:</TD><TD colspan='12'>25/07/2026 a 31/07/2026</TD></TR>
        <TR><TD colspan='1'>Profissional Executante:</TD><TD colspan='12'>MARCO ANTONIO (12345678901)</TD></TR>
        <TR><TD colspan='1'>Procedimento Ambulatorial:</TD><TD colspan='12'>MAMOGRAFIA BILATERAL (1305007)</TD></TR>
        </TABLE></CENTER>
        """;

    private const string Agendamento = """
        <TABLE id='tblConsulta673133160' class='table_listagem' align='center' width='100%'>
        	<TR>
        		<TD colspan='1' rowspan='2' width='8%'><CENTER><B>673133160</B></CENTER></TD>
        		<TD colspan='1'><B>CNS:</B><BR>700000000000001</TD>
        		<TD colspan='1'><B>Paciente:</B><BR>PACIENTE DE TESTE</TD>
        		<TD colspan='2'><B>Nascimento:</B><BR>15/03/1970</TD>
        		<TD colspan='1'><B>Idade:</B><BR>56</TD>
        		<TD colspan='1'><B>Origem:</B><BR>MARICA - RJ</TD>
        		<TD colspan='1'><B>Telefone(s):</B><BR>21999990000<br>2126330000</TD>
        	</TR>
        	<TR>
        		<TD colspan='2'><B>Unidade Solicitante:</B><BR>USF JOSEFA XAVIER LEAL (1234567)</TD>
        		<TD colspan='1'><B>Vaga Solicitada:</B><BR>1&ordf; VEZ</TD>
        		<TD colspan='1'><B>Vaga Consumida:</B><BR>RESERVA</TD>
        		<TD colspan='1'><B>CID-10:</B><BR>Z014</TD>
        		<TD rowspan='1'><B>Data/Hora:</B><BR>28/07/2026 - TER - 08:00</B></TD>
        		<TD colspan='1'><B>Situa&ccedil;&atilde;o:</B><BR><B><FONT color='red'>Agendamento/Pendente&nbsp;Confirma&ccedil;&atilde;o/Executante</FONT></B></TD>
        	</TR>
        	<TR>
        		<TD colspan='3' rowspan='100' align='center'><B>Procedimento(s):</B></TD>
        		<TD colspan='5'><B>01</B> - MAMOGRAFIA BILATERAL</TD>
        	</TR>
        </TABLE><BR>
        """;

    private const string Paginacao =
        "<center><table><tr><td>Mostrando P&aacute;gina <input size='3' type='text' name='txtPagina' value='1'> de 2 </td></tr></table></center>";

    [Fact]
    public void Le_um_agendamento_inteiro()
    {
        var pagina = VarreduraAgendaParser.Parse(Cabecalho + Agendamento + Paginacao);

        var linha = Assert.Single(pagina.Linhas);

        Assert.Equal("673133160", linha.CoSolicitacao);
        Assert.Equal("700000000000001", linha.Cns);
        Assert.Equal("PACIENTE DE TESTE", linha.Paciente);
        Assert.Equal("15/03/1970", linha.Nascimento);
        Assert.Equal("56", linha.Idade);
        Assert.Equal("MARICA - RJ", linha.Origem);
        Assert.Equal("Z014", linha.Cid10);
        Assert.Equal("RESERVA", linha.VagaConsumida);
    }

    [Fact]
    public void Separa_a_unidade_solicitante_do_cnes()
    {
        var linha = Assert.Single(VarreduraAgendaParser.Parse(Agendamento).Linhas);

        Assert.Equal("USF JOSEFA XAVIER LEAL", linha.UnidadeSolicitante);
        Assert.Equal("1234567", linha.CnesSolicitante);
    }

    [Fact]
    public void Separa_data_dia_e_hora()
    {
        var linha = Assert.Single(VarreduraAgendaParser.Parse(Agendamento).Linhas);

        Assert.Equal("28/07/2026", linha.Data);
        Assert.Equal("TER", linha.DiaSemana);
        Assert.Equal("08:00", linha.Hora);
    }

    [Fact]
    public void Entidades_html_sao_decodificadas()
    {
        var linha = Assert.Single(VarreduraAgendaParser.Parse(Agendamento).Linhas);

        Assert.Equal("1ª VEZ", linha.VagaSolicitada);          // &ordf;
        Assert.Contains("Confirmação", linha.Situacao);          // &ccedil; &atilde;
    }

    [Fact]
    public void Nbsp_dentro_da_situacao_vira_espaco_comum()
    {
        var linha = Assert.Single(VarreduraAgendaParser.Parse(Agendamento).Linhas);

        // Sem normalizar, o valor traria U+00A0 e qualquer comparação de texto falharia de um
        // jeito invisível — na tela os dois parecem idênticos.
        Assert.Equal("Agendamento/Pendente Confirmação/Executante", linha.Situacao);
        Assert.DoesNotContain(' ', linha.Situacao!);
    }

    [Fact]
    public void Procedimento_vem_da_celula_seguinte_ao_rotulo()
    {
        // A célula "Procedimento(s):" tem rowspan e NÃO contém o valor — ele está na célula ao lado.
        var linha = Assert.Single(VarreduraAgendaParser.Parse(Agendamento).Linhas);

        Assert.Equal("01 - MAMOGRAFIA BILATERAL", linha.Procedimentos);
    }

    [Fact]
    public void Le_a_unidade_executante_do_cabecalho()
    {
        var pagina = VarreduraAgendaParser.Parse(Cabecalho + Agendamento);

        Assert.Equal("3132358", pagina.CnesExecutante);
        Assert.Equal("CDT DR ALBERTO LUIS MACHADO BORGES", pagina.NomeExecutante);
    }

    [Fact]
    public void Le_o_total_de_paginas()
    {
        // O <input> não contribui texto: o texto achatado vira "Mostrando Página de 2".
        Assert.Equal(2, VarreduraAgendaParser.Parse(Agendamento + Paginacao).TotalPaginas);
    }

    [Fact]
    public void Sem_bloco_de_paginacao_vale_uma_pagina()
    {
        Assert.Equal(1, VarreduraAgendaParser.Parse(Cabecalho + Agendamento).TotalPaginas);
    }

    [Fact]
    public void Pagina_sem_resultados_devolve_lista_vazia_sem_estourar()
    {
        var pagina = VarreduraAgendaParser.Parse(
            Cabecalho + "<center>A pesquisa n&atilde;o retornou nenhum resultado.</center>");

        Assert.Empty(pagina.Linhas);
        Assert.Equal(1, pagina.TotalPaginas);
        Assert.Equal("3132358", pagina.CnesExecutante);
    }

    [Fact]
    public void Html_vazio_ou_lixo_nao_estoura()
    {
        Assert.Empty(VarreduraAgendaParser.Parse("").Linhas);
        Assert.Empty(VarreduraAgendaParser.Parse("   ").Linhas);
        Assert.Empty(VarreduraAgendaParser.Parse("<html><body>qualquer coisa</body></html>").Linhas);
    }

    [Fact]
    public void Varios_agendamentos_na_mesma_pagina()
    {
        var segundo = Agendamento
            .Replace("tblConsulta673133160", "tblConsulta673133161")
            .Replace("673133160</B>", "673133161</B>");

        var pagina = VarreduraAgendaParser.Parse(Cabecalho + Agendamento + segundo + Paginacao);

        Assert.Equal(2, pagina.Linhas.Count);
        Assert.Equal(["673133160", "673133161"], pagina.Linhas.Select(l => l.CoSolicitacao));
    }

    [Fact]
    public void O_codigo_vem_do_id_da_tabela_nao_do_texto()
    {
        // O id é a fonte confiável: o texto do código está dentro de um <B> que o parser usaria
        // como rótulo, e é o mesmo elemento que muda de lugar quando o SISREG mexe no layout.
        var semTexto = Agendamento.Replace("<CENTER><B>673133160</B></CENTER>", "<CENTER></CENTER>");

        var linha = Assert.Single(VarreduraAgendaParser.Parse(semTexto).Linhas);
        Assert.Equal("673133160", linha.CoSolicitacao);
    }
}
