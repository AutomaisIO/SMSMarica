using System.Text.Json;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.EsusSgWeb.Varredura;
using SMSMais.Core.Regulacao.Conciliacao;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Tests.EsusSg;

/// <summary>
/// O cliente do ESUS de São Gonçalo (ADR-0063) sem rede: a trava de somente-leitura e a leitura do
/// JSON do legado — as duas coisas que, erradas, ou escrevem no SG ou somem com dado em silêncio.
/// Os formatos vêm do que foi MEDIDO no laboratório em 30/09/2026.
/// </summary>
public class EsusSgClienteTests
{
    [Theory]
    [InlineData("exames2/controller-fila-exame/buscar")]
    [InlineData("exames2/controller-paciente-agendado-fila-exames/buscar")]
    [InlineData("consultas/controller-fila-consulta/buscar")]
    [InlineData("exames2/controller-fila-exame/combo-box-procedimentos-regulaveis-por-solicitante")]
    [InlineData("exames2/controller-fila-exame/carregar-dados-visualizacao-fila")]
    public void Trava_deixa_passar_leitura(string caminho) =>
        EsusSgSessao.GarantirLeitura(caminho);

    [Theory]
    [InlineData("exames2/controller-fila-exame/salvar")]
    [InlineData("exames2/controller-fila-exame/adicionar")]
    [InlineData("exames2/controller-fila-exame/regular-em-lote")]
    [InlineData("exames2/controller-fila-exame/cancelar-regulacao-em-lote")]
    [InlineData("exames2/controller-fila-exame/avancar")]
    // Começa com "obter", mas gerar comprovante pode carimbar "comprovante impresso" no SG.
    [InlineData("exames2/controller-exame-agendamento-procedimento/obter-comprovante-exame-agendamento-em-lote")]
    // Começa com "buscar" e embute escrita.
    [InlineData("exames2/controller-fila-exame/buscar-e-agendar")]
    public void Trava_recusa_escrita(string caminho) =>
        Assert.Throws<EscritaNoEsusSgBloqueadaException>(() => EsusSgSessao.GarantirLeitura(caminho));

    [Fact]
    public void Paginacao_entende_as_duas_formas_do_legado()
    {
        using var fila = JsonDocument.Parse("""[[{"fil_id":"1"},{"fil_id":"2"}],"650"]""");
        var (l1, t1) = EsusSgLeitorService.LinhasETotal(fila.RootElement, "fila");
        Assert.Equal(2, l1.Count);
        Assert.Equal(650, t1);

        using var agendados = JsonDocument.Parse("""{"recordSet":[{"fil_id":"9"}],"total":"61"}""");
        var (l2, t2) = EsusSgLeitorService.LinhasETotal(agendados.RootElement, "agendados");
        Assert.Single(l2);
        Assert.Equal(61, t2);
    }

    [Fact]
    public void Paginacao_com_forma_desconhecida_e_erro_nao_lista_vazia()
    {
        // Devolver vazio aqui foi o que escondeu 61 agendados na primeira medição.
        using var estranho = JsonDocument.Parse("""{"linhas":[],"qtd":3}""");
        Assert.Throws<FormatException>(() => EsusSgLeitorService.LinhasETotal(estranho.RootElement, "x"));
    }

    [Fact]
    public void Leitura_completa_compara_unicos_com_o_declarado()
    {
        // 2019 medido: 1.405 linhas brutas, 1.352 únicas = declarado → completa.
        var completa = new LeituraEsusSg<int>([.. Enumerable.Range(0, 1352)], Declarado: 1352, Recebidas: 1405, Requisicoes: 2);
        Assert.True(completa.Completa);

        var faltando = new LeituraEsusSg<int>([.. Enumerable.Range(0, 97)], Declarado: 100, Recebidas: 100, Requisicoes: 1);
        Assert.False(faltando.Completa);
    }

    [Fact]
    public void Linha_da_fila_nome_e_o_procedimento_e_pes_nome_e_o_paciente()
    {
        using var doc = JsonDocument.Parse("""
            {"fil_id":"1586329","nome":"TRATAMENTO DE RETINA (PPI)","fle_nome_procedimento":"TRATAMENTO DE RETINA (PPI)",
             "pes_nome":"MARIA DA SILVA","pep_cpf_numero":"","pae_cpf":"52998224725","pep_cartaosus":"700000000000005",
             "fil_data":"30/01/2023","fil_data_pedido":"12/01/2023","pep_nascimento":"21/07/2001","pep_sexo":"F",
             "pfi_nome":"URGENTE","pendencia":"NAO","fil_ordem_regulada":"2","fil_ordem_entrada":"250",
             "codigo_procedimento":"3251","usu_nome":"OPERADORA MARICA","telefone":"(21) 99999-0000",
             "pae_ultima_alteracao":"31/12/1969 21:00:00"}
            """);
        var l = EsusSgLinhaFila.De(doc.RootElement);

        Assert.Equal("1586329", l.IdEsusSg);
        Assert.Equal("TRATAMENTO DE RETINA (PPI)", l.Recurso);
        Assert.Equal("MARIA DA SILVA", l.PacienteNome);
        Assert.Equal("52998224725", l.Cpf); // pep_* vazio → cai no pae_*
        Assert.Equal("700000000000005", l.Cns);
        Assert.Equal(new DateOnly(2023, 1, 30), l.DataEntradaFila);
        Assert.Equal(new DateOnly(2023, 1, 12), l.DataSolicitacao);
        Assert.Equal(2, l.PosicaoFila);
        Assert.Equal("3251", l.CodigoInterno);
        Assert.Equal("OPERADORA MARICA", l.UsuarioInclusao);
    }

    [Fact]
    public void Linha_de_agendado_traz_sessao_hora_em_utc_e_cnes_do_nome()
    {
        using var doc = JsonDocument.Parse("""
            {"fil_id":"77","eap_id":"555","stp_novo_nome_procedimento":"MODALIDADE AUDITIVA",
             "nomePaciente":"JOAO","cpf_numero":"52998224725","unidadeAgendamento":"ABRAE  2297523",
             "data_agendada":"06/10/2026","data_hora_formatada":"06/10/2026 13:15:00",
             "data_cadastro_agendamento":"04/08/2026","usuarioAgendamento":"OPERADOR SG",
             "comprovanteImpresso":"SIM","agendado_tfd":"NÃO","not_resposta":"CONFIRMADO"}
            """);
        var a = EsusSgLinhaAgendado.De(doc.RootElement);

        Assert.Equal("555", a.AgendamentoIdEsus);
        Assert.Equal("2297523", a.CnesExecutora);
        Assert.Equal(new DateOnly(2026, 10, 6), a.DataAgendada);
        // 13:15 de Brasília = 16:15 UTC (régua única de fuso).
        Assert.Equal(new DateTime(2026, 10, 6, 16, 15, 0, DateTimeKind.Utc), a.DataHoraAgendada);
        Assert.True(a.ComprovanteImpresso);
        Assert.False(a.AgendadoTfd);
    }

    [Theory]
    [InlineData("ABRAE  2297523", "2297523")]
    [InlineData("OFTALMOCLÍNICA SÃO GONÇALO - 2291525", "2291525")]
    [InlineData("VISATTO UNIDADE RODOSHOPPING", null)]
    [InlineData("MUNICÍPIO DE MARICÁ - 0000001", null)] // o placeholder de Maricá não é CNES real
    [InlineData(null, null)]
    public void Cnes_so_quando_vem_no_fim_do_nome(string? nome, string? esperado) =>
        Assert.Equal(esperado, EsusSgJson.CnesNoNome(nome));

    [Theory]
    [InlineData("31/12/1969", null)] // "vazio" do ESUS (epoch em UTC−3)
    [InlineData("", null)]
    [InlineData("12/01/2023", "2023-01-12")]
    [InlineData("06/10/2026 13:15:00", "2026-10-06")]
    public void Datas_do_esus(string entrada, string? esperado) =>
        Assert.Equal(esperado is null ? null : DateOnly.Parse(esperado), EsusSgJson.Data(entrada));

    [Theory]
    [InlineData("NAO", false)]
    [InlineData("TODAS RESOLVIDAS", false)]
    [InlineData(null, false)]
    [InlineData("AGUARDANDO EXAME DE IMAGEM", true)]
    public void Pendencia_ativa(string? texto, bool ativa) =>
        Assert.Equal(ativa, EsusSgSincronizacaoService.EhPendenciaAtiva(texto));

    [Fact]
    public void Principal_e_a_proxima_sessao_ou_a_ultima_se_todas_passaram()
    {
        EsusSgLinhaAgendado Sessao(string eap, DateOnly d) => new(
            "1", eap, "RETINA", null, null, null, null, null, null, null, null, "P", null, null, null, null, null,
            null, d, $"{d:dd/MM/yyyy} 08:00:00", null, null, null, null, null, null, null, null, null);

        var hoje = new DateOnly(2026, 9, 30);
        var passadas = new[] { Sessao("a", hoje.AddDays(-20)), Sessao("b", hoje.AddDays(-5)) };
        Assert.Equal("b", EsusSgSincronizacaoService.Principal(passadas, hoje).AgendamentoIdEsus);

        var mistas = new[] { Sessao("a", hoje.AddDays(-5)), Sessao("c", hoje.AddDays(30)), Sessao("b", hoje.AddDays(7)) };
        Assert.Equal("b", EsusSgSincronizacaoService.Principal(mistas, hoje).AgendamentoIdEsus);
    }

    [Theory]
    [InlineData(SituacaoEsusSg.EmFila, StatusRegulacao.EmFilaExterna)]
    [InlineData(SituacaoEsusSg.Pendente, StatusRegulacao.EmFilaExterna)]
    [InlineData(SituacaoEsusSg.Agendada, StatusRegulacao.Agendada)]
    public void Mapa_de_situacao(SituacaoEsusSg de, StatusRegulacao para) =>
        Assert.Equal(para, MapaSituacaoExterna.DeEsusSg(de));

    [Fact]
    public void Saiu_da_fila_nao_vira_cancelada_porque_o_motivo_nao_e_visivel() =>
        Assert.Null(MapaSituacaoExterna.DeEsusSg(SituacaoEsusSg.SaiuDaFila));
}
