using FluentAssertions;
using SMSMais.Core.Integracoes.SisregWeb.Importacao.AgendaPontual;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Testes puros do parser do RESULTADO da tela de agenda (SISREG III <c>cons_agendas</c>,
/// <c>etapa=ListaConsulta</c>). O HTML abaixo espelha a estrutura real — cada agendamento numa
/// <c>&lt;table id="tblConsulta&lt;codigo&gt;"&gt;</c>, células <c>&lt;B&gt;Rótulo:&lt;/B&gt;&lt;BR&gt;valor</c>,
/// e o procedimento como <c>&lt;B&gt;01&lt;/B&gt; - NOME</c> — com dados SINTÉTICOS, nunca PII real.
/// </summary>
public class ConsAgendasParserTests
{
    private static readonly ConsAgendasParser.Contexto Ctx =
        new("2930242", "CENTRO MATERNO INFANTIL", "08675605765", "RENATO ROQUE DE ANDRADE");

    // Dois agendamentos, incluindo acento em "Situação" (para testar o match de rótulo sem acento)
    // e o procedimento na 3ª linha. Códigos/CNS são fictícios.
    private const string ResultadoHtml = """
        <html><body>
        <table id='tblConsulta900000001' class='table_listagem'>
          <tr>
            <td><B>CNS:</B><BR>700000000000001</td>
            <td><B>Paciente:</B><BR>FULANA DE TAL</td>
            <td><B>Nascimento:</B><BR>27/06/1991</td>
            <td><B>Idade:</B><BR>35</td>
            <td><B>Origem:</B><BR>MARICA - RJ</td>
            <td><B>Telefone(s):</B><BR>(21) 2636-1088</td>
          </tr>
          <tr>
            <td><B>Unidade Solicitante:</B><BR>UNIDADE DE SAUDE DA FAMILIA CHACARA DE INOA (6289851)</td>
            <td><B>Vaga Solicitada:</B><BR>1&ordf; VEZ</td>
            <td><B>Vaga Consumida:</B><BR>RESERVA</td>
            <td><B>CID-10:</B><BR>Z34</td>
            <td><B>Data/Hora:</B><BR>26/08/2026 - QUA - 13:00</td>
            <td><B>Situa&ccedil;&atilde;o:</B><BR>Agendamento/Pendente Confirma&ccedil;&atilde;o/Executante</td>
          </tr>
          <tr>
            <td><B>Procedimento(s):</B></td>
            <td><B>01</B> - USG OBSTETRICA</td>
          </tr>
        </table>
        <table id='tblConsulta900000002' class='table_listagem'>
          <tr>
            <td><B>CNS:</B><BR>700000000000002</td>
            <td><B>Paciente:</B><BR>SICRANA DE TAL</td>
            <td><B>Nascimento:</B><BR>01/03/2000</td>
            <td><B>Idade:</B><BR>26</td>
            <td><B>Origem:</B><BR>MARICA - RJ</td>
            <td><B>Telefone(s):</B><BR>(21) 97273-8879</td>
          </tr>
          <tr>
            <td><B>Unidade Solicitante:</B><BR>CENTRO MATERNO INFANTIL (2930242)</td>
            <td><B>Vaga Solicitada:</B><BR>RETORNO</td>
            <td><B>Vaga Consumida:</B><BR>1&ordf; VEZ</td>
            <td><B>CID-10:</B><BR>Z321</td>
            <td><B>Data/Hora:</B><BR>26/08/2026 - QUA - 14:30</td>
            <td><B>Situa&ccedil;&atilde;o:</B><BR>Agendamento/Confirmado/Executante</td>
          </tr>
          <tr>
            <td><B>Procedimento(s):</B></td>
            <td><B>01</B> - USG TRANSVAGINAL - GESTANTE</td>
          </tr>
        </table>
        <div>Mostrando P&aacute;gina [ 1 ] de 1</div>
        </body></html>
        """;

    [Fact]
    public void Parse_extrai_um_registro_por_tabela_com_todos_os_campos()
    {
        var registros = ConsAgendasParser.Parse(ResultadoHtml, Ctx);

        registros.Should().HaveCount(2);

        var primeiro = registros[0];
        primeiro.CodigoSolicitacao.Should().Be("900000001");
        primeiro.CnsPaciente.Should().Be("700000000000001");
        primeiro.NomePaciente.Should().Be("FULANA DE TAL");
        primeiro.NascimentoPaciente.Should().Be(new DateOnly(1991, 6, 27));
        primeiro.TelefonePaciente.Should().Be("(21) 2636-1088");
        primeiro.ProcedimentoTexto.Should().Be("USG OBSTETRICA");
        primeiro.CnesUnidadeSolicitante.Should().Be("6289851");
        primeiro.NomeUnidadeSolicitante.Should().Be("UNIDADE DE SAUDE DA FAMILIA CHACARA DE INOA");
        primeiro.Cid.Should().Be("Z34");
        primeiro.VagaConsumida.Should().Be("RESERVA");
        primeiro.SituacaoAgendamento.Should().Be("Agendamento/Pendente Confirmação/Executante");
        primeiro.DataHoraAtendimento.Should().Be(new DateTime(2026, 8, 26, 13, 0, 0, DateTimeKind.Unspecified));

        // Contexto do par consultado vira proveniência.
        primeiro.CnesUnidadeExecutante.Should().Be("2930242");
        primeiro.CpfProfissionalExecutante.Should().Be("08675605765");

        // cons_agendas não informa SIGTAP nem pa — resolvidos depois, por nome.
        primeiro.CodigoSigtap.Should().BeNull();
        primeiro.CodigoProcedimentoSisreg.Should().BeNull();

        // O RAW (envelope) marca a origem — é o que distingue o importado da manual na reconciliação.
        primeiro.LinhaRaw.Should().NotBeNullOrEmpty();
        primeiro.LinhaRaw.Should().Contain("cons_agendas");

        registros[1].ProcedimentoTexto.Should().Be("USG TRANSVAGINAL - GESTANTE");
        registros[1].DataHoraAtendimento.Should().Be(new DateTime(2026, 8, 26, 14, 30, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void TotalPaginas_le_o_rodape() =>
        ConsAgendasParser.TotalPaginas(ResultadoHtml).Should().Be(1);

    [Fact]
    public void TotalPaginas_devolve_1_quando_nao_ha_rodape() =>
        ConsAgendasParser.TotalPaginas("<html><body>sem paginacao</body></html>").Should().Be(1);

    [Fact]
    public void SemResultado_detecta_a_mensagem_do_sisreg()
    {
        ConsAgendasParser.SemResultado("A pesquisa não retornou nenhum resultado.").Should().BeTrue();
        ConsAgendasParser.SemResultado(ResultadoHtml).Should().BeFalse();
    }

    [Fact]
    public void Parse_ignora_html_sem_agendamentos()
    {
        ConsAgendasParser.Parse("<html><body><table id='outra'><tr><td>x</td></tr></table></body></html>", Ctx)
            .Should().BeEmpty();
    }
}
