using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Integracoes.SisregWeb.Fila;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Motor da fila de espera do SISREG.
///
/// <para>O que está sob teste é a parte que <b>não dá para conferir olhando</b>: quem sai da fila.
/// Marcar saída errado é dizer que uma pessoa foi atendida quando ela ainda espera — o pior erro
/// possível nesta tabela, porque some com ela da lista de quem precisa ser chamado.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class FilaPendenteSisregTests(PostgresFixture fixture)
{
    /// <summary>Sessão dublada: devolve o HTML combinado, sem tocar no SISREG.</summary>
    private sealed class SessaoFake(string html) : ISisregWebSessao
    {
        public int Chamadas { get; private set; }

        public Task<string> PostFormAsync(
            string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken ct) =>
            throw new NotSupportedException("A fila é lida por GET.");

        public Task<string> GetAsync(
            string caminho, IReadOnlyDictionary<string, string>? query, CancellationToken ct,
            Func<string, bool>? pareceSessaoCaida = null)
        {
            Chamadas++;
            return Task.FromResult(html);
        }
    }

    private static string Pagina(params (string Codigo, string Data, string Cns)[] linhas) =>
        "<html><script>function visualizaFicha(c){}</script><TABLE>"
        + string.Join("", linhas.Select(l => $"""
            <TR onClick="visualizaFicha({l.Codigo});">
            <TD align="center">{l.Codigo}</TD>
            <TD align="center">{l.Data}</TD>
            <TD align="center" title="1" ><img src=/imagens/amarelo.png></TD>
            <TD align="left" title="N&uacute;mero CNS: {l.Cns} &#10;Nome Paciente: PACIENTE TESTE &#10;Nome da M&atilde;e: MAE TESTE &#10;Data Nascimento: 01/01/1980">PACIENTE TESTE</TD>
            <TD align="center">(21) 99999-0000</TD>
            <TD align="left">MARICA</TD>
            <TD align="center">45 anos</TD>
            <TD align="left">CONSULTA EM CARDIOLOGIA</TD>
            <TD align="center" title="I10 - HIPERTENSAO">I10</TD>
            <TD align="left">USF TESTE</TD>
            <TD align="left">---</TD>
            <TD align="left">SOL/PEN/REG</TD>
            </TR>
            """))
        + "</TABLE></html>";

    private static FilaPendenteSisregService Criar(SmsMaisDbContext db, string html) =>
        new(db,
            new SessaoFake(html),
            new SisregOrcamentoRequisicoes(),
            Options.Create(new SisregOrcamentoOpcoes()),
            NullLogger<FilaPendenteSisregService>.Instance);

    private static string Codigo() => Random.Shared.Next(100_000_000, 999_999_999).ToString();

    [Fact]
    public async Task Grava_quem_esta_na_fila_e_relida_atualiza_em_vez_de_duplicar()
    {
        await using var db = fixture.CriarDbContext();
        var cod = Codigo();
        var html = Pagina((cod, "10/09/2026", "700000000000001"));
        var ini = new DateOnly(2026, 9, 1);
        var fim = new DateOnly(2026, 9, 30);

        var r1 = await Criar(db, html).LerJanelaAsync(ini, fim);
        var r2 = await Criar(db, html).LerJanelaAsync(ini, fim);

        Assert.Equal(1, r1.Novas);
        Assert.Equal(0, r2.Novas);
        Assert.Equal(1, r2.Atualizadas);

        var linhas = await db.SisregFilaPendentes.AsNoTracking()
            .Where(f => f.CodigoSolicitacao == cod).ToListAsync();
        var f = Assert.Single(linhas);
        Assert.Equal("700000000000001", f.Cns);
        Assert.Equal(45, f.IdadeAnos);
        Assert.Null(f.SaiuEm);
    }

    /// <summary>
    /// Sumiu do SISREG e existe agendada no nosso banco: foi atendida. É o desfecho bom, e é o que
    /// permite medir a espera de ponta a ponta sem perguntar nada ao SISREG.
    /// </summary>
    [Fact]
    public async Task Quem_sumiu_e_esta_agendada_no_nosso_banco_sai_como_Agendada()
    {
        await using var db = fixture.CriarDbContext();
        var cod = Codigo();
        var ini = new DateOnly(2026, 9, 1);
        var fim = new DateOnly(2026, 9, 30);

        await Criar(db, Pagina((cod, "10/09/2026", "700000000000002"))).LerJanelaAsync(ini, fim);

        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE FILA {Random.Shared.Next(100_000, 999_999)}",
            Cnes = Random.Shared.Next(1_000_000, 9_999_999).ToString(),
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        db.Solicitacoes.Add(new Solicitacao
        {
            Id = Guid.CreateVersion7(),
            CodigoSolicitacao = cod,
            UnidadeExecutanteId = unidade.Id,
            DataAgendada = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            Status = StatusSolicitacao.Agendada,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Leitura seguinte sem ela, mas com OUTRA linha: a página não pode vir vazia, senão o
        // motor (corretamente) se recusa a concluir qualquer coisa.
        var r = await Criar(db, Pagina((Codigo(), "11/09/2026", "700000000000003")))
            .LerJanelaAsync(ini, fim);

        Assert.Equal(1, r.Saidas);
        Assert.Equal(1, r.SaidasAgendadas);

        var f = await db.SisregFilaPendentes.AsNoTracking()
            .SingleAsync(x => x.CodigoSolicitacao == cod);
        Assert.NotNull(f.SaiuEm);
        Assert.Equal(SaidaDaFilaSisreg.Agendada, f.SaiuPara);
    }

    /// <summary>Sumiu e não apareceu agendada: cancelou, negou ou devolveu — o SISREG não diz qual.</summary>
    [Fact]
    public async Task Quem_sumiu_sem_aparecer_agendada_sai_como_SaiuSemAgendar()
    {
        await using var db = fixture.CriarDbContext();
        var cod = Codigo();
        var ini = new DateOnly(2026, 9, 1);
        var fim = new DateOnly(2026, 9, 30);

        await Criar(db, Pagina((cod, "10/09/2026", "700000000000004"))).LerJanelaAsync(ini, fim);
        var r = await Criar(db, Pagina((Codigo(), "11/09/2026", "700000000000005")))
            .LerJanelaAsync(ini, fim);

        Assert.Equal(1, r.Saidas);
        Assert.Equal(0, r.SaidasAgendadas);

        var f = await db.SisregFilaPendentes.AsNoTracking()
            .SingleAsync(x => x.CodigoSolicitacao == cod);
        Assert.Equal(SaidaDaFilaSisreg.SaiuSemAgendar, f.SaiuPara);
    }

    /// <summary>
    /// <b>O recorte pela janela é o que impede o desastre.</b> Ler setembro e concluir sobre a base
    /// inteira marcaria como atendida toda pessoa que pediu antes — e a maior parte da fila pediu
    /// antes. Mesma lição que o detector de ausentes da agenda já pagou.
    /// </summary>
    [Fact]
    public async Task Quem_pediu_FORA_da_janela_lida_nao_e_marcado_como_saida()
    {
        await using var db = fixture.CriarDbContext();
        var antigo = Codigo();

        // Entra por uma janela de julho.
        await Criar(db, Pagina((antigo, "05/07/2026", "700000000000006")))
            .LerJanelaAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        // Setembro é lido e obviamente não traz quem pediu em julho.
        await Criar(db, Pagina((Codigo(), "10/09/2026", "700000000000007")))
            .LerJanelaAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var f = await db.SisregFilaPendentes.AsNoTracking()
            .SingleAsync(x => x.CodigoSolicitacao == antigo);
        Assert.Null(f.SaiuEm);
    }

    /// <summary>
    /// Zero linhas é indistinguível de sessão caída ou SISREG fora do ar. Marcar a janela inteira
    /// como "saiu" transformaria uma falha de rede em "todo mundo foi atendido".
    /// </summary>
    [Fact]
    public async Task Leitura_vazia_nao_marca_ninguem_como_saida()
    {
        await using var db = fixture.CriarDbContext();
        var cod = Codigo();
        var ini = new DateOnly(2026, 9, 1);
        var fim = new DateOnly(2026, 9, 30);

        await Criar(db, Pagina((cod, "10/09/2026", "700000000000008"))).LerJanelaAsync(ini, fim);
        var r = await Criar(db, "<html>Nenhum registro encontrado</html>").LerJanelaAsync(ini, fim);

        Assert.Equal(0, r.Lidas);
        Assert.Equal(0, r.Saidas);

        var f = await db.SisregFilaPendentes.AsNoTracking()
            .SingleAsync(x => x.CodigoSolicitacao == cod);
        Assert.Null(f.SaiuEm);
    }

    /// <summary>
    /// O SISREG devolve solicitação para a fila (devolvida, reenviada). Quem volta tem de deixar de
    /// constar como atendido — senão fica invisível para quem monta a lista de chamada.
    /// </summary>
    [Fact]
    public async Task Quem_volta_para_a_fila_deixa_de_constar_como_saida()
    {
        await using var db = fixture.CriarDbContext();
        var cod = Codigo();
        var ini = new DateOnly(2026, 9, 1);
        var fim = new DateOnly(2026, 9, 30);
        var html = Pagina((cod, "10/09/2026", "700000000000009"));

        await Criar(db, html).LerJanelaAsync(ini, fim);
        await Criar(db, Pagina((Codigo(), "11/09/2026", "700000000000010"))).LerJanelaAsync(ini, fim);
        Assert.NotNull((await db.SisregFilaPendentes.AsNoTracking()
            .SingleAsync(x => x.CodigoSolicitacao == cod)).SaiuEm);

        await Criar(db, html).LerJanelaAsync(ini, fim);

        var f = await db.SisregFilaPendentes.AsNoTracking()
            .SingleAsync(x => x.CodigoSolicitacao == cod);
        Assert.Null(f.SaiuEm);
        Assert.Null(f.SaiuPara);
    }

    [Fact]
    public async Task Janela_maior_que_o_teto_do_SISREG_e_recusada_antes_de_gastar_requisicao()
    {
        await using var db = fixture.CriarDbContext();
        var sessao = new SessaoFake(Pagina());
        var servico = new FilaPendenteSisregService(
            db, sessao, new SisregOrcamentoRequisicoes(),
            Options.Create(new SisregOrcamentoOpcoes()),
            NullLogger<FilaPendenteSisregService>.Instance);

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            servico.LerJanelaAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1)));

        Assert.Equal(0, sessao.Chamadas);
    }

    [Fact]
    public async Task Data_inicial_depois_da_final_e_recusada()
    {
        await using var db = fixture.CriarDbContext();

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            Criar(db, Pagina()).LerJanelaAsync(new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1)));
    }

    /// <summary>
    /// Uma requisição por situação: pendente (1) e reenviada (5). As reenviadas estão na fila do
    /// regulador (conferido na tela Autorizar em 10/09/2026) e ficavam de fora. Continua barato:
    /// 2 requisições por janela, e a mesma solicitação nas duas respostas grava uma vez só.
    /// </summary>
    [Fact]
    public async Task Uma_janela_custa_uma_requisicao_por_situacao_e_nao_duplica()
    {
        await using var db = fixture.CriarDbContext();
        var cod = Codigo();
        var sessao = new SessaoFake(Pagina((cod, "10/09/2026", "700000000000011")));
        var servico = new FilaPendenteSisregService(
            db, sessao, new SisregOrcamentoRequisicoes(),
            Options.Create(new SisregOrcamentoOpcoes()),
            NullLogger<FilaPendenteSisregService>.Instance);

        var r = await servico.LerJanelaAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal(2, sessao.Chamadas);
        Assert.Equal(2, r.Requisicoes);
        Assert.Equal(1, r.Lidas);
        Assert.Equal(1, await db.SisregFilaPendentes.CountAsync(f => f.CodigoSolicitacao == cod));
    }

    /// <summary>Sessão que responde conforme a situação pedida.</summary>
    private sealed class SessaoPorSituacao(IReadOnlyDictionary<string, string> porSituacao) : ISisregWebSessao
    {
        public Task<string> PostFormAsync(
            string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string> GetAsync(
            string caminho, IReadOnlyDictionary<string, string>? query, CancellationToken ct,
            Func<string, bool>? pareceSessaoCaida = null) =>
            Task.FromResult(porSituacao.GetValueOrDefault(query!["cmb_situacao"], ""));
    }

    /// <summary>
    /// Quem decide se a leitura conclui saídas é a situação 1. As reenviadas são ~1%: se a pendente
    /// volta vazia (sessão caída) e só a reenviada traz gente, concluir saídas marcaria como
    /// atendidos os 99% que simplesmente não foram lidos.
    /// </summary>
    [Fact]
    public async Task Pendente_vazia_nao_conclui_saida_mesmo_com_reenviada_cheia()
    {
        await using var db = fixture.CriarDbContext();
        var ini = new DateOnly(2026, 9, 1);
        var fim = new DateOnly(2026, 9, 30);
        var pendente = Codigo();

        await Criar(db, Pagina((pendente, "10/09/2026", "700000000000012"))).LerJanelaAsync(ini, fim);

        var servico = new FilaPendenteSisregService(
            db,
            new SessaoPorSituacao(new Dictionary<string, string>
            {
                ["1"] = "<html>Nenhum registro encontrado</html>",
                ["5"] = Pagina((Codigo(), "12/09/2026", "700000000000013")),
            }),
            new SisregOrcamentoRequisicoes(),
            Options.Create(new SisregOrcamentoOpcoes()),
            NullLogger<FilaPendenteSisregService>.Instance);

        var r = await servico.LerJanelaAsync(ini, fim);

        Assert.Equal(1, r.Lidas);
        Assert.Equal(0, r.Saidas);
        Assert.Null((await db.SisregFilaPendentes.AsNoTracking()
            .SingleAsync(x => x.CodigoSolicitacao == pendente)).SaiuEm);
    }

    /// <summary>
    /// A carga completa anda do mais recente para o mais antigo, sem buraco e sem sobreposição, e
    /// nenhuma janela passa do teto que o SISREG aceita.
    /// </summary>
    [Fact]
    public void Carga_completa_cobre_o_periodo_em_janelas_de_ate_31_dias_sem_buraco()
    {
        var hoje = new DateOnly(2026, 9, 10);
        var desde = new DateOnly(2026, 7, 1);

        var janelas = SMSMais.Core.Integracoes.SisregWeb.Fila.Background.JanelasDaFila.Completa(hoje, desde);

        Assert.Equal(hoje, janelas[0].Fim);
        Assert.Equal(desde, janelas[^1].Inicio);
        Assert.All(janelas, j => Assert.InRange(j.Fim.DayNumber - j.Inicio.DayNumber + 1, 1,
            FilaPendenteSisregService.MaxDiasPorJanela));
        for (var i = 1; i < janelas.Count; i++)
        {
            Assert.Equal(janelas[i - 1].Inicio.AddDays(-1), janelas[i].Fim);
        }
    }
}
