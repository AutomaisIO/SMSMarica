using Microsoft.Extensions.Caching.Memory;

using SMSMais.Core.Regulacao.Regras;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Regras;

/// <summary>
/// Ranking dos procedimentos por demanda, que é como a tela de regras decide o que mostrar
/// primeiro (plano 03).
///
/// <para>O que estes testes prendem: que o topo seja calculado <b>por sistema</b> — as escalas
/// diferem em três ordens de grandeza e um ranking único apagaria o SERNIT —, que omitir um
/// sistema realmente o tire da lista, e que a contagem de regras separe ativas de total, que é a
/// informação pela qual a curadoria escolhe onde trabalhar.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoProcedimentoRegradoServiceTests(PostgresFixture fixture)
{
    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static RegulacaoProcedimentoRegradoService Servico(SmsMaisDbContext db) =>
        new(db, new MemoryCache(new MemoryCacheOptions()));

    /// <summary>Cria um procedimento com origem no sistema pedido e devolve o id e o rótulo.</summary>
    private static async Task<(Guid Id, string Rotulo, string Chave)> ProcedimentoAsync(
        SmsMaisDbContext db, SistemaRegulacao sistema, string? ramo = null)
    {
        var sufixo = Sufixo();
        var rotulo = $"CONSULTA EM CARDIOLOGIA {sufixo}";

        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = rotulo,
            NomeNormalizado = rotulo,
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(procedimento);

        var chave = sistema == SistemaRegulacao.Sisreg ? sufixo : $"1|{sufixo}|{ramo}";
        db.RegulacaoProcedimentoOrigens.Add(new RegulacaoProcedimentoOrigem
        {
            Id = Guid.CreateVersion7(),
            ProcedimentoId = procedimento.Id,
            Sistema = sistema,
            ChaveExterna = chave,
            RotuloExterno = rotulo,
            Ramo = ramo,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return (procedimento.Id, rotulo, chave);
    }

    /// <summary>Uma unidade de verdade: `solicitacao.unidade_executante_id` tem FK.</summary>
    private static async Task<Guid> UnidadeAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE REGRAS {Guid.NewGuid():N}"[..40],
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return unidade.Id;
    }

    private static void SemearSisreg(SmsMaisDbContext db, Guid unidadeId, string codigo, int quantas)
    {
        for (var i = 0; i < quantas; i++)
        {
            db.Solicitacoes.Add(new Solicitacao
            {
                Id = Guid.NewGuid(),
                PacienteId = Guid.NewGuid(),
                Categoria = CategoriaSolicitacao.Imagem,
                UnidadeExecutanteId = unidadeId,
                SolicitanteNome = "DR TESTE",
                ProcedimentoTexto = "PROCEDIMENTO DE TESTE",
                ProcedimentoCodigoSisreg = codigo,
                Status = StatusSolicitacao.Solicitada,
                StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente,
                Prioridade = PrioridadeSolicitacao.Eletiva,
                CriadoEm = DateTime.UtcNow,
            });
        }
    }

    private static void SemearSer(SmsMaisDbContext db, string recurso, int quantas)
    {
        for (var i = 0; i < quantas; i++)
        {
            db.SerSolicitacoes.Add(new SerSolicitacao
            {
                Id = Guid.CreateVersion7(),
                // `id_ser` é único: sem valor próprio, a segunda linha do laço colide.
                IdSer = Guid.NewGuid().ToString("N")[..20],
                Recurso = recurso,
                CriadoEm = DateTime.UtcNow,
            });
        }
    }

    [Fact]
    public async Task Ordena_pelos_mais_pedidos_e_conta_as_regras()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await UnidadeAsync(db);
        var muito = await ProcedimentoAsync(db, SistemaRegulacao.Sisreg);
        var pouco = await ProcedimentoAsync(db, SistemaRegulacao.Sisreg);

        SemearSisreg(db, unidadeId, muito.Chave, 40);
        SemearSisreg(db, unidadeId, pouco.Chave, 3);

        // Três regras no mais pedido, uma delas desligada: a tela precisa distinguir "tem regra"
        // de "tem regra valendo" — é por isso que a curadoria escolhe onde mexer.
        foreach (var ativo in new[] { true, true, false })
        {
            db.RegulacaoRegras.Add(new RegulacaoRegra
            {
                Id = Guid.CreateVersion7(),
                ProcedimentoId = muito.Id,
                Tipo = TipoRegraRegulacao.NaoDedutivel,
                Severidade = SeveridadeRegraRegulacao.Bloqueia,
                Descricao = "criterio",
                Pergunta = "pergunta?",
                RespostaBloqueia = RespostaRegraRegulacao.Nao,
                Versao = 1,
                Ativo = ativo,
                CriadoEm = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var top = await Servico(db).MaisPedidosAsync(
            [SistemaRegulacao.Sisreg], 500, CancellationToken.None);

        var a = top.Single(x => x.ProcedimentoId == muito.Id);
        var b = top.Single(x => x.ProcedimentoId == pouco.Id);

        a.Demanda.Should().Be(40);
        a.TotalRegras.Should().Be(3);
        a.RegrasAtivas.Should().Be(2);
        b.Demanda.Should().Be(3);
        b.TotalRegras.Should().Be(0);

        top.ToList().IndexOf(a).Should().BeLessThan(top.ToList().IndexOf(b));
    }

    [Fact]
    public async Task O_topo_e_por_sistema_para_o_menor_nao_sumir()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await UnidadeAsync(db);
        var grande = await ProcedimentoAsync(db, SistemaRegulacao.Sisreg);
        var pequeno = await ProcedimentoAsync(db, SistemaRegulacao.Ser, "AE");

        // A proporção real: o SISREG tem 1.017.902 solicitações históricas e o SERNIT 1.223.
        // Num ranking único por contagem bruta, o sistema menor não alcança a centésima linha.
        SemearSisreg(db, unidadeId, grande.Chave, 500);
        SemearSer(db, pequeno.Rotulo, 2);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var top = await Servico(db).MaisPedidosAsync(
            [SistemaRegulacao.Sisreg, SistemaRegulacao.Ser], 1, CancellationToken.None);

        // Com limite 1 e um ranking ÚNICO, viria uma linha só — a do sistema grande. Com o topo
        // por sistema, vem uma de cada, e é isso que se afirma aqui.
        //
        // A afirmação é sobre a PRESENÇA de cada sistema, não sobre estes dois procedimentos:
        // a bancada é compartilhada e outros testes semeiam demanda no SER e no SISREG, então
        // quem ocupa o primeiro lugar de cada um varia conforme a suíte inteira roda. Fixar os
        // ids fazia o teste passar isolado e falhar no meio dos 1.352.
        top.Should().HaveCountGreaterThan(1);
        top.Should().Contain(x => x.PorSistema.Any(d => d.Sistema == SistemaRegulacao.Ser));
        top.Should().Contain(x => x.PorSistema.Any(d => d.Sistema == SistemaRegulacao.Sisreg));
    }

    [Fact]
    public async Task Sistema_omitido_nao_aparece()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await UnidadeAsync(db);
        var sisreg = await ProcedimentoAsync(db, SistemaRegulacao.Sisreg);
        var ser = await ProcedimentoAsync(db, SistemaRegulacao.Ser, "AE");

        SemearSisreg(db, unidadeId, sisreg.Chave, 10);
        SemearSer(db, ser.Rotulo, 10);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var soSer = await Servico(db).MaisPedidosAsync(
            [SistemaRegulacao.Ser], 500, CancellationToken.None);

        soSer.Should().Contain(x => x.ProcedimentoId == ser.Id);
        soSer.Should().NotContain(x => x.ProcedimentoId == sisreg.Id);
    }

    [Fact]
    public async Task SER_casa_pelo_rotulo_mesmo_com_acento_e_espaco_a_mais()
    {
        await using var db = fixture.CriarDbContext();
        var p = await ProcedimentoAsync(db, SistemaRegulacao.Ser, "AE");

        // O SER não grava o código na solicitação, só o rótulo digitado — e a mesma coisa aparece
        // como "CONSULTA EM CARDIOLOGIA" e "Consulta  em Cardiología".
        SemearSer(db, p.Rotulo.Replace("CARDIOLOGIA", "CARDIOLOGÍA  ").ToLowerInvariant(), 7);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var top = await Servico(db).MaisPedidosAsync(
            [SistemaRegulacao.Ser], 500, CancellationToken.None);

        top.Single(x => x.ProcedimentoId == p.Id).Demanda.Should().Be(7);
    }

    [Fact]
    public async Task Sem_sistema_nenhum_pedido_considera_todos()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await UnidadeAsync(db);
        var p = await ProcedimentoAsync(db, SistemaRegulacao.Sisreg);
        SemearSisreg(db, unidadeId, p.Chave, 5);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var top = await Servico(db).MaisPedidosAsync([], 500, CancellationToken.None);

        top.Should().Contain(x => x.ProcedimentoId == p.Id);
    }

    [Theory]
    [InlineData("CONSULTA EM CARDIOLOGIA", "Consulta  em Cardiología")]
    [InlineData("EXAME - TIPO A", "exame   tipo a")]
    [InlineData("  RESSONÂNCIA  ", "ressonancia")]
    public void A_chave_do_rotulo_ignora_acento_caixa_pontuacao_e_espaco(string a, string b) =>
        ChaveRotulo.Normalizar(a).Should().Be(ChaveRotulo.Normalizar(b));

    [Fact]
    public void Rotulos_diferentes_continuam_diferentes() =>
        ChaveRotulo.Normalizar("CONSULTA EM CARDIOLOGIA")
            .Should().NotBe(ChaveRotulo.Normalizar("CONSULTA EM CARDIOLOGIA PEDIATRIA"));
}
