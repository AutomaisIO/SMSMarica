using NSubstitute;
using QuestPDF.Infrastructure;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Exames;
using SMSMais.Core.Institucional;
using SMSMais.Core.Integracoes.Sisreg.Base;
using SMSMais.Core.Integracoes.Sisreg.Base.Dtos;
using SMSMais.Core.Midias;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Consulta SISREG na nossa base (tela SISREG → Consultar). O que está sob teste é a situação de
/// cada atendimento — é ela que vira relatório nominal e cobrança à unidade, então errar aqui põe
/// paciente que compareceu na lista de "pendentes" ou chama de falta quem a unidade nem apontou.
///
/// <para>Régua (a mesma da ficha do paciente): a unidade executante aponta Confirmado ou Falta no
/// SISREG; passou do dia sem nenhum dos dois é Pendente — não é falta.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConsultaBaseSisregTests(PostgresFixture fixture)
{
    // Todo dia de teste fica no passado distante, para "hoje" nunca mudar a resposta.
    private static readonly DateOnly Dia = new(2025, 3, 10);

    private static ConsultaBaseSisregService Servico(SmsMaisDbContext db) =>
        new(db, new UsuarioAtualAccessorFake(), Substitute.For<IPacienteResolver>(),
            Substitute.For<IInstituicaoService>(), Substitute.For<IMidiasService>(),
            new FrescorBaseSisregService(db,
                Microsoft.Extensions.Options.Options.Create(new FrescorBaseSisregOpcoes()),
                Microsoft.Extensions.Options.Options.Create(new SMSMais.Core.Integracoes.SisregWeb.Indicadores.ColetaIndicadoresOpcoes()),
                Microsoft.Extensions.Options.Options.Create(new SMSMais.Core.Integracoes.SisregWeb.Varredura.VarreduraSisregOpcoes())));

    /// <summary>Unidade nova a cada teste: a bancada é compartilhada e o serviço vê a rede inteira.</summary>
    private static async Task<Guid> UnidadeAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE CONSULTA {Guid.NewGuid():N}"[..40],
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade.Id;
    }

    private static Solicitacao Semear(
        SmsMaisDbContext db,
        Guid unidadeId,
        DateTime? agendadaUtc,
        StatusSolicitacao status = StatusSolicitacao.Agendada,
        CategoriaSolicitacao categoria = CategoriaSolicitacao.Consulta,
        string procedimento = "FISIOTERAPIA DE TESTE",
        Guid? pacienteId = null)
    {
        var s = new Solicitacao
        {
            Id = Guid.NewGuid(),
            PacienteId = pacienteId ?? Guid.NewGuid(),
            Categoria = categoria,
            UnidadeExecutanteId = unidadeId,
            SolicitanteNome = "DR TESTE",
            ProcedimentoTexto = procedimento,
            Status = status,
            StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = agendadaUtc is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null,
            DataSolicitacao = Dia.AddDays(-30),
            // Código único por linha: o índice de código é único na base inteira.
            CodigoSolicitacao = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString(),
            CriadoEm = DateTime.UtcNow,
        };
        db.Solicitacoes.Add(s);
        return s;
    }

    /// <summary>10h de Brasília do <see cref="Dia"/>.</summary>
    private static DateTime NoDia(int deslocamentoDias = 0) =>
        Dia.AddDays(deslocamentoDias).ToDateTime(new TimeOnly(13, 0), DateTimeKind.Utc);

    private static ConsultaBaseSisregFiltro Filtro(
        Guid unidade,
        DateOnly? inicio = null,
        DateOnly? fim = null,
        IReadOnlyList<SituacaoAgendamentoSisreg>? situacoes = null,
        IReadOnlyList<string>? procedimentos = null,
        bool exames = true,
        bool consultas = true) =>
        new(inicio ?? Dia, fim ?? Dia, EixoDataConsultaSisreg.Agendamento, [unidade], situacoes, procedimentos,
            exames, consultas);

    private static int Contagem(ConsultaBaseSisregResultado r, SituacaoAgendamentoSisreg s) =>
        r.PorSituacao.FirstOrDefault(c => c.Situacao == s)?.Quantidade ?? 0;

    /// <summary>
    /// Cada fonte de prova leva à situação certa — e a ausência de prova leva a Pendente, não a
    /// Faltou. Falta da lista oficial só vale no MESMO dia: o código pode ter faltado numa data,
    /// sido remarcado e ainda não ter apontamento na nova.
    /// </summary>
    [Fact]
    public async Task Situacao_vem_da_chegada_da_lista_de_faltas_e_do_status()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);

        var relida = Semear(db, unidade, NoDia());
        relida.ChegadaConfirmadaSisreg = true;

        // Histórico carregado depois do fato: o CONFIRMADO da coluna 34 do TXT guardado vale.
        var txt = Semear(db, unidade, NoDia());
        txt.RawSisreg = string.Join(';', Enumerable.Range(0, 38)
            .Select(i => i switch { 0 => txt.CodigoSolicitacao!, 34 => "CONFIRMADO", _ => "x" }));

        var recepcao = Semear(db, unidade, NoDia());
        recepcao.AutorizadoEm = DateTime.UtcNow;

        var faltou = Semear(db, unidade, NoDia());
        var faltaEmOutroDia = Semear(db, unidade, NoDia());
        var semApontamento = Semear(db, unidade, NoDia());
        semApontamento.ChegadaConfirmadaSisreg = false;
        Semear(db, unidade, NoDia(), StatusSolicitacao.Cancelada);

        db.SisregFaltasOficiais.AddRange(
            new SisregFaltaOficial
            {
                Id = Guid.NewGuid(), CodigoSolicitacao = faltou.CodigoSolicitacao!, DataExecucao = Dia,
                LidoEm = DateTime.UtcNow,
            },
            new SisregFaltaOficial
            {
                Id = Guid.NewGuid(), CodigoSolicitacao = faltaEmOutroDia.CodigoSolicitacao!,
                DataExecucao = Dia.AddDays(-7), LidoEm = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var r = await Servico(db).BuscarAsync(Filtro(unidade));

        Assert.Equal(7, r.TotalAtendimentos);
        Assert.Equal(3, Contagem(r, SituacaoAgendamentoSisreg.Compareceu));
        Assert.Equal(1, Contagem(r, SituacaoAgendamentoSisreg.Faltou));
        Assert.Equal(2, Contagem(r, SituacaoAgendamentoSisreg.Pendente));
        Assert.Equal(1, Contagem(r, SituacaoAgendamentoSisreg.Cancelada));
        Assert.Equal(
            SituacaoAgendamentoSisreg.Faltou,
            r.Itens.Single(i => i.SolicitacaoId == faltou.Id).Situacao);
        Assert.Equal(
            SituacaoAgendamentoSisreg.Pendente,
            r.Itens.Single(i => i.SolicitacaoId == faltaEmOutroDia.Id).Situacao);
    }

    /// <summary>Data futura é Agendada, mesmo sem nenhum apontamento — ainda não tinha como haver.</summary>
    [Fact]
    public async Task Data_futura_e_agendada()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var amanha = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        Semear(db, unidade, amanha.ToDateTime(new TimeOnly(13, 0), DateTimeKind.Utc));
        await db.SaveChangesAsync();

        var r = await Servico(db).BuscarAsync(Filtro(unidade, amanha, amanha));

        Assert.Equal(SituacaoAgendamentoSisreg.Agendada, Assert.Single(r.Itens).Situacao);
    }

    /// <summary>
    /// Agendamento das 22h de Brasília conta no dia de Brasília. Sem isto, a lista "do dia 10"
    /// perderia os atendimentos do fim da noite e a "do dia 11" ganharia os do dia anterior.
    /// </summary>
    [Fact]
    public async Task Agendamento_noturno_conta_no_dia_de_Brasilia()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        // 01:00 UTC do dia seguinte = 22:00 de Brasília do Dia.
        Semear(db, unidade, Dia.AddDays(1).ToDateTime(new TimeOnly(1, 0), DateTimeKind.Utc));
        await db.SaveChangesAsync();

        var servico = Servico(db);
        Assert.Equal(1, (await servico.BuscarAsync(Filtro(unidade, Dia, Dia))).TotalAtendimentos);
        Assert.Equal(0, (await servico.BuscarAsync(Filtro(unidade, Dia.AddDays(1), Dia.AddDays(1)))).TotalAtendimentos);
    }

    /// <summary>
    /// Situação, procedimento e tipo filtram; o total de pessoas conta paciente distinto — quem
    /// tem duas sessões pendentes é UMA pessoa a procurar, não duas.
    /// </summary>
    [Fact]
    public async Task Filtros_e_contagem_de_pessoas_distintas()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var paciente = Guid.NewGuid();
        Semear(db, unidade, NoDia(), procedimento: "GRUPO A", pacienteId: paciente);
        Semear(db, unidade, NoDia(1), procedimento: "GRUPO A", pacienteId: paciente);
        Semear(db, unidade, NoDia(), procedimento: "GRUPO B");
        Semear(db, unidade, NoDia(), procedimento: "GRUPO A", categoria: CategoriaSolicitacao.Imagem);
        Semear(db, unidade, NoDia(), StatusSolicitacao.Cancelada, procedimento: "GRUPO A");
        await db.SaveChangesAsync();

        var r = await Servico(db).BuscarAsync(Filtro(
            unidade, Dia, Dia.AddDays(1),
            situacoes: [SituacaoAgendamentoSisreg.Pendente],
            procedimentos: ["GRUPO A"],
            exames: false));

        Assert.Equal(2, r.TotalAtendimentos);
        Assert.Equal(1, r.TotalPessoas);
        Assert.All(r.Itens, i => Assert.Equal(paciente, i.PacienteId));
    }

    /// <summary>Paginação devolve só a página, mas os totais são do resultado inteiro.</summary>
    [Fact]
    public async Task Pagina_traz_o_recorte_e_totais_do_resultado_inteiro()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        for (var i = 0; i < 105; i++) Semear(db, unidade, NoDia());
        await db.SaveChangesAsync();

        var r = await Servico(db).BuscarAsync(Filtro(unidade) with { Pagina = 2, Tamanho = 100 });

        Assert.Equal(105, r.TotalAtendimentos);
        Assert.Equal(105, r.TotalPessoas);
        Assert.Equal(5, r.Itens.Count);
    }

    [Fact]
    public async Task Filtro_invalido_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        var unidade = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            servico.BuscarAsync(Filtro(unidade, Dia, Dia.AddDays(-1))));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            servico.BuscarAsync(Filtro(unidade, Dia, Dia.AddDays(400))));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            servico.BuscarAsync(Filtro(unidade, exames: false, consultas: false)));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            servico.BuscarAsync(Filtro(unidade) with { Tamanho = 1000 }));
    }

    /// <summary>As opções trazem as unidades e os procedimentos que existem no período.</summary>
    [Fact]
    public async Task Opcoes_trazem_unidade_e_procedimentos_do_periodo()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var procedimento = $"PROC {Guid.NewGuid():N}";
        Semear(db, unidade, NoDia(), procedimento: procedimento);
        Semear(db, unidade, NoDia(), procedimento: procedimento);
        await db.SaveChangesAsync();

        var o = await Servico(db).OpcoesAsync(Dia, Dia, EixoDataConsultaSisreg.Agendamento);

        Assert.Equal(2, o.Unidades.Single(u => u.Id == unidade).Quantidade);
        Assert.Equal(2, o.Procedimentos.Single(p => p.Nome == procedimento).Quantidade);
    }

    /// <summary>O PDF nominal sai (QuestPDF compõe a tabela inteira, com o filtro por extenso).</summary>
    [Fact]
    public void Pdf_nominal_e_gerado()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var itens = Enumerable.Range(0, 60).Select(i => new AgendamentoBaseSisregDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"PACIENTE {i:00}", null, "123",
            "FISIOTERAPIA", CategoriaSolicitacao.Consulta, NoDia(), Dia, "UNIDADE",
            SituacaoAgendamentoSisreg.Pendente)).ToList();

        var pdf = new ConsultaBaseSisregPdf(
            new ConsultaBaseSisregFiltro(Dia, Dia, Situacoes: [SituacaoAgendamentoSisreg.Pendente]),
            itens, ["UNIDADE"],
            // Com o aviso de base incompleta: é o caminho que imprime a faixa amarela.
            new FrescorBaseSisregDto([new DiaSemFaltasDto(Dia, 3, null)],
                [new ChegadaAtrasadaDto(Guid.NewGuid(), "UNIDADE", 2, null)], 30),
            DateTime.UtcNow,
            new IdentidadeVisualPdf("Secretaria de Teste", "#B71C1C", "#700000", null)).Gerar();

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
