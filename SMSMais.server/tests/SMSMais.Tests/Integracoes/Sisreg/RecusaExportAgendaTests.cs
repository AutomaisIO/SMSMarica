using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Core.Integracoes.SisregWeb.Varredura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// A fatia que o SISREG devolveu é mesmo o export pedido?
///
/// <para><b>O incidente que originou estes testes (06/09/2026).</b> A varredura do CDT fechou como
/// <c>Concluída</c> e a fila de alterações recebeu, no mesmo segundo, <b>734 "sumiu do SISREG"</b>.
/// Não era cancelamento: quatro dias apareciam com <b>100% da agenda ausente</b> (177, 125, 193 e
/// 207 marcações) enquanto todos os dias vizinhos ficavam em 0–3,5%. Os dois intervalos eram
/// exatamente nós-folha da recursão que parte a janela por volume — e, consultado de novo dez dias
/// depois, o SISREG repetiu a falha: para 27–28/08 devolveu <c>alert('Ocorreu um erro nao esperado
/// durante a exportacao do arquivo.')</c> com <b>HTTP 200</b>, e para 02–03/09 um
/// <b>504 Gateway Timeout</b>.</para>
///
/// <para><b>Por que passou.</b> Página de erro não tem linha de dados, então o parser devolvia zero
/// marcações; a única guarda existente perguntava se tinha batido no teto de 700, e zero não bate.
/// A fatia era dada como lida, e "não veio no arquivo" virou "foi cancelado".</para>
///
/// <para><b>O par de testes que importa</b> é o de recusa junto com o de aceite: uma checagem que
/// reprovasse o arquivo legítimo de um <b>dia sem agendamento</b> (fim de semana, feriado — vem com
/// cabeçalho válido e <c>total=0</c>) trocaria o falso cancelamento por uma varredura que nunca
/// fecha completa.</para>
/// </summary>
public class RecusaExportAgendaTests
{
    private static readonly DateOnly Ini = new(2026, 8, 27);
    private static readonly DateOnly Fim = new(2026, 8, 28);

    /// <summary>Cabeçalho do TXT: <c>CNES;unidade;dt_ini;dt_fim;total</c>.</summary>
    private static string Cabecalho(string ini = "27/08/2026", string fim = "28/08/2026", int total = 0)
        => $"3132358;CDT DR ALBERTO LUIS MACHADO BORGES;{ini};{fim};{total}";

    /// <summary>Linha de dados mínima que o parser aceita: 38 campos, nº e SIGTAP numéricos.</summary>
    private static string LinhaDeDados(string codigo = "674880018")
    {
        var c = new string[38];
        for (var i = 0; i < c.Length; i++) c[i] = string.Empty;
        c[0] = codigo;              // nº da solicitação
        c[1] = "1402147";           // pa
        c[2] = "0204030030";        // SIGTAP
        c[3] = "MAMOGRAFIA BILATERAL";
        c[4] = "08594208766";       // CPF executante
        c[5] = "PROFISSIONAL EXECUTANTE";
        c[6] = "27.08.2026";        // data de atendimento
        c[7] = "08:00";
        c[26] = "2296306";          // CNES do solicitante (7 dígitos)
        c[27] = "UNIDADE DE SAUDE DA FAMILIA SAO JOSE I";
        return string.Join(';', c);
    }

    private static string? Recusa(string corpo, DateOnly? ini = null, DateOnly? fim = null)
        => VarreduraAgendaService.RecusarExport(
            AgendaTxtParser.Parse(corpo, "sisreg-unidade-20260827-20260828.txt"),
            ini ?? Ini, fim ?? Fim, corpo.Length);

    // ---------------------------------------------------------------- o que causou o incidente

    /// <summary>115 bytes, HTTP 200, e nenhuma linha de dados — o parser via só "agenda vazia".</summary>
    [Fact]
    public void Pagina_de_alert_do_sisreg_e_recusada()
    {
        var corpo = "<SCRIPT type=\"text/javascript\">alert('Ocorreu um erro nao esperado "
            + "durante a exportacao do arquivo.');</SCRIPT>";

        Assert.NotNull(Recusa(corpo));
    }

    [Fact]
    public void Erro_504_do_gateway_e_recusado()
    {
        var corpo = "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n<html><head>\n"
            + "<title>504 Gateway Timeout</title>\n</head><body>\n<h1>Gateway Timeout</h1>\n"
            + "<p>The gateway did not receive a timely response\nfrom the upstream server or "
            + "application.</p>\n</body></html>";

        Assert.NotNull(Recusa(corpo));
    }

    [Fact]
    public void Resposta_vazia_e_recusada()
    {
        Assert.NotNull(Recusa(string.Empty));
    }

    // ---------------------------------------------------------------- o que NÃO pode ser recusado

    /// <summary>
    /// Dia sem agendamento nenhum é arquivo legítimo. Recusá-lo trocaria o cancelamento falso por
    /// uma varredura que nunca fecha completa — e por retentativas pagas com orçamento anti-robô.
    /// </summary>
    [Fact]
    public void Fatia_sem_agendamento_com_cabecalho_valido_e_aceita()
    {
        Assert.Null(Recusa(Cabecalho(total: 0)));
    }

    [Fact]
    public void Fatia_com_agendamentos_e_aceita()
    {
        Assert.Null(Recusa(Cabecalho(total: 2) + "\n" + LinhaDeDados("674880018")
            + "\n" + LinhaDeDados("686189981")));
    }

    // ---------------------------------------------------------------- as outras formas de furo

    /// <summary>
    /// Arquivo de outro período contado como se fosse o pedido faria a faixa pedida parecer vazia —
    /// o mesmo desfecho do incidente, por outro caminho.
    /// </summary>
    [Fact]
    public void Periodo_diferente_do_pedido_e_recusado()
    {
        Assert.NotNull(Recusa(Cabecalho("01/09/2026", "02/09/2026", total: 0)));
    }

    /// <summary>O total declarado é o contrato do arquivo: vieram menos linhas = corte no meio.</summary>
    [Fact]
    public void Arquivo_cortado_no_meio_e_recusado()
    {
        Assert.NotNull(Recusa(Cabecalho(total: 300) + "\n" + LinhaDeDados()));
    }

    /// <summary>Mais linhas que o declarado não é corte — e recusar aqui perderia agendamento bom.</summary>
    [Fact]
    public void Total_declarado_menor_que_as_linhas_nao_e_recusa()
    {
        Assert.Null(Recusa(Cabecalho(total: 1) + "\n" + LinhaDeDados("674880018")
            + "\n" + LinhaDeDados("686189981")));
    }

    // ---------------------------------------------------------------- o dia não lido sai da conta

    /// <summary>
    /// A trava final: mesmo que tudo acima falhe, agendamento em dia não lido não pode ser comparado
    /// com o arquivo — "não veio" ali não é informação sobre a agenda, é ausência de informação.
    /// </summary>
    [Fact]
    public void Agendamento_em_dia_nao_lido_fica_fora_da_checagem()
    {
        List<(DateOnly, DateOnly)> naoLidas = [(new(2026, 8, 27), new(2026, 8, 28))];

        // 27/08 08:00 em Brasília = 11:00 UTC.
        var dentro = new DateTime(2026, 8, 27, 11, 0, 0, DateTimeKind.Utc);
        var fora = new DateTime(2026, 8, 26, 11, 0, 0, DateTimeKind.Utc);

        Assert.True(VarreduraAgendaService.EmFaixaNaoLida(dentro, naoLidas));
        Assert.False(VarreduraAgendaService.EmFaixaNaoLida(fora, naoLidas));
    }

    /// <summary>
    /// A conversão de fuso não é detalhe: 28/08 22:00 em Brasília é 29/08 01:00 em UTC. Comparar o
    /// instante cru jogaria o fim do dia para fora da faixa e o agendamento voltaria a ser candidato
    /// a "cancelado" — o erro de três horas que o módulo inteiro evita pela régua central.
    /// </summary>
    [Fact]
    public void Fim_do_dia_em_brasilia_continua_dentro_da_faixa()
    {
        List<(DateOnly, DateOnly)> naoLidas = [(new(2026, 8, 27), new(2026, 8, 28))];
        var ultimaHora = new DateTime(2026, 8, 29, 1, 0, 0, DateTimeKind.Utc);

        Assert.True(VarreduraAgendaService.EmFaixaNaoLida(ultimaHora, naoLidas));
    }

    [Fact]
    public void Sem_faixa_nao_lida_nada_fica_de_fora()
    {
        Assert.False(VarreduraAgendaService.EmFaixaNaoLida(
            new DateTime(2026, 8, 27, 11, 0, 0, DateTimeKind.Utc), []));
    }
}
