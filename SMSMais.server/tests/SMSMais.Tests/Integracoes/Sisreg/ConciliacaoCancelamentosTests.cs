using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Integracoes.SisregWeb.Cancelamento;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Conciliação dos cancelamentos feitos NO SISREG por outra pessoa.
///
/// <para>O que estes testes protegem é a regra que custou caro para aparecer: <b>não concluir de
/// leitura incompleta</b>. A tela declara quantas linhas existem; ler menos e seguir em frente
/// significaria dar por conciliado um dia em que um cancelamento se perdeu — e foi exatamente o
/// que a primeira versão do coletor do laboratório fez, em silêncio, deixando 269 linhas de
/// fora.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConciliacaoCancelamentosTests(PostgresFixture fixture)
{
    private sealed class SessaoFake(Func<int, string> porPagina) : ISisregWebSessao
    {
        public int Requisicoes { get; private set; }

        public Task<string> PostFormAsync(
            string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken)
        {
            var pagina = int.Parse(campos.TryGetValue("pagina", out var p) ? p : "0");
            Requisicoes++;
            return Task.FromResult(porPagina(pagina));
        }

        public Task<string> GetAsync(
            string caminho, IReadOnlyDictionary<string, string>? query, CancellationToken cancellationToken,
            Func<string, bool>? pareceSessaoCaida = null) => Task.FromResult(string.Empty);

        /// <summary>Escrita assina com o login do operador — aqui só se lê.</summary>
        public void UsarCredencialDoOperador(string usuario, string senha) { }
    }

    /// <summary>Uma página como o SISREG a devolve: cabeçalho com o total e linhas de 9 colunas.</summary>
    private static string Pagina(int declaradas, int paginas, params (string Codigo, string Just, string Op)[] linhas)
    {
        var corpo = string.Join("", linhas.Select(l =>
            $"<tr><td>{l.Codigo}</td><td>23.10.2026</td><td>08:00:00</td><td>CONSULTA</td>"
            + $"<td>DR FULANO</td><td>PACIENTE</td><td>{l.Just}</td><td>{l.Op}</td>"
            + "<td>18.09.2026 11:24:54</td></tr>"));

        return $"<html><body>MARCA&Ccedil;&Otilde;ES PESQUISADAS ({declaradas})"
               + $"<table>{corpo}</table>"
               + $"<a onClick=\"exibirPagina(1,{paginas})\">Pr&oacute;xima</a></body></html>";
    }

    private static ConciliacaoCancelamentosSisregService Criar(
        SmsMaisDbContext db, ISisregWebSessao sessao, IComunicacaoPacienteService? comunicacoes = null) =>
        new(sessao, db,
            comunicacoes ?? Substitute.For<IComunicacaoPacienteService>(),
            NullLogger<ConciliacaoCancelamentosSisregService>.Instance);

    [Fact]
    public async Task Cancelamento_do_SISREG_e_aplicado_na_nossa_base()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(10));
        var codigo = $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var s = await db.Solicitacoes.SingleAsync(x => x.Id == exame.SolicitacaoId);
        s.CodigoSolicitacao = codigo;
        await db.SaveChangesAsync();

        var sessao = new SessaoFake(_ => Pagina(1, 1, (codigo, "erro de marcacao", "123THAIS-EX")));
        var comunicacoes = Substitute.For<IComunicacaoPacienteService>();

        var r = await Criar(db, sessao, comunicacoes).ConciliarDiaAsync(new DateOnly(2026, 9, 18));

        Assert.Null(r.Aviso);
        Assert.Equal(1, r.Conciliados);

        await using var db2 = fixture.CriarDbContext();
        var depois = await db2.Solicitacoes.SingleAsync(x => x.Id == exame.SolicitacaoId);
        Assert.Equal(StatusSolicitacao.Cancelada, depois.Status);
        Assert.Equal(StatusConfirmacaoAgendamento.Cancelada, depois.StatusConfirmacao);
        Assert.Equal(ConciliacaoCancelamentosSisregService.Canal, depois.ConfirmadoCanal);

        // O instante é o do SISREG, não o de agora: a trilha conta quando aconteceu.
        Assert.Equal(new DateTime(2026, 9, 18, 14, 24, 54, DateTimeKind.Utc), depois.CanceladoEm);

        // Motivo e operador ficam na trilha INTERNA — e é isso que a mensagem ao paciente não diz.
        Assert.Contains("erro de marcacao", depois.MotivoCancelamento);
        Assert.Contains("123THAIS-EX", depois.MotivoCancelamento);

        // Agendamento futuro: o paciente é avisado.
        await comunicacoes.Received(1).EnfileirarAsync(
            Arg.Any<SMSMais.Data.Entities.Solicitacao>(),
            FinalidadeComunicacao.CancelamentoAgendamento,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A regra que não se negocia: se o lido não bate com o que a tela declarou, a passada é
    /// descartada inteira. Aplicar o que veio seria dar por conciliado um dia em que faltou linha.
    /// </summary>
    [Fact]
    public async Task Leitura_incompleta_NAO_concilia_nada()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(10));
        var codigo = $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var s = await db.Solicitacoes.SingleAsync(x => x.Id == exame.SolicitacaoId);
        s.CodigoSolicitacao = codigo;
        await db.SaveChangesAsync();

        // A tela diz 40 linhas em 2 páginas; as páginas entregam 1. Falta linha.
        var sessao = new SessaoFake(_ => Pagina(40, 2, (codigo, "erro", "123THAIS-EX")));

        var r = await Criar(db, sessao).ConciliarDiaAsync(new DateOnly(2026, 9, 18));

        Assert.NotNull(r.Aviso);
        Assert.Equal(0, r.Conciliados);

        await using var db2 = fixture.CriarDbContext();
        Assert.NotEqual(
            StatusSolicitacao.Cancelada,
            (await db2.Solicitacoes.SingleAsync(x => x.Id == exame.SolicitacaoId)).Status);
    }

    /// <summary>
    /// Data passada é arrumação de base, não notícia: mandar mensagem sobre um agendamento que já
    /// passou confunde quem foi (ou quem não foi) e não devolve vaga nenhuma.
    /// </summary>
    [Fact]
    public async Task Agendamento_que_ja_passou_e_conciliado_sem_avisar_o_paciente()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(-3));
        var codigo = $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var s = await db.Solicitacoes.SingleAsync(x => x.Id == exame.SolicitacaoId);
        s.CodigoSolicitacao = codigo;
        await db.SaveChangesAsync();

        var sessao = new SessaoFake(_ => Pagina(1, 1, (codigo, "nao compareceu", "211ESTER-REG")));
        var comunicacoes = Substitute.For<IComunicacaoPacienteService>();

        var r = await Criar(db, sessao, comunicacoes).ConciliarDiaAsync(new DateOnly(2026, 9, 18));

        Assert.Equal(1, r.Conciliados);
        await comunicacoes.DidNotReceive().EnfileirarAsync(
            Arg.Any<SMSMais.Data.Entities.Solicitacao>(),
            FinalidadeComunicacao.CancelamentoAgendamento,
            Arg.Any<CancellationToken>());
    }

    /// <summary>Rodar duas vezes o mesmo dia não pode cancelar de novo nem avisar de novo.</summary>
    [Fact]
    public async Task Passar_duas_vezes_no_mesmo_dia_nao_repete()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(10));
        var codigo = $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var s = await db.Solicitacoes.SingleAsync(x => x.Id == exame.SolicitacaoId);
        s.CodigoSolicitacao = codigo;
        await db.SaveChangesAsync();

        var sessao = new SessaoFake(_ => Pagina(1, 1, (codigo, "erro", "123THAIS-EX")));
        var comunicacoes = Substitute.For<IComunicacaoPacienteService>();
        var servico = Criar(db, sessao, comunicacoes);

        await servico.ConciliarDiaAsync(new DateOnly(2026, 9, 18));

        await using var db3 = fixture.CriarDbContext();
        var segunda = await Criar(db3, sessao, comunicacoes).ConciliarDiaAsync(new DateOnly(2026, 9, 18));

        Assert.Equal(0, segunda.Conciliados);
        Assert.Equal(1, segunda.JaConheciamos);
        await comunicacoes.Received(1).EnfileirarAsync(
            Arg.Any<SMSMais.Data.Entities.Solicitacao>(),
            FinalidadeComunicacao.CancelamentoAgendamento,
            Arg.Any<CancellationToken>());
    }
}
