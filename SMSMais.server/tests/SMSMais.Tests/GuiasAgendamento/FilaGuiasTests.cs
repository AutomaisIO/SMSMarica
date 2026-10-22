using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using SMSMais.Core.Armazenamento;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Core.Exames;
using SMSMais.Core.GuiasAgendamento;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.EsusSgWeb.Varredura.Background;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SernitWeb.Varredura;
using SMSMais.Core.Integracoes.SernitWeb.Varredura.Background;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Varredura;
using SMSMais.Core.Integracoes.SerWeb.Varredura.Background;
using SMSMais.Core.Integracoes.SisregWeb.Chave;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.GuiasAgendamento;

/// <summary>
/// A fila de guias dos agendamentos regulados (ADR-0076): a guia nasce na fila vazia, sem arquivo, e o
/// baixador de cada sistema a preenche no ritmo configurado, guardando-a UMA vez no acervo.
///
/// <para>O que estes testes guardam: enfileirar não duplica e tira da fila o que deixou de valer
/// (remarcado, desmarcado, passado); cada guia é buscada UMA vez (no SER e no SERNIT cada emissão gasta
/// uma via); a falha espera antes de tentar de novo e desiste depois do teto; um PDF já guardado é
/// ligado em vez de rebuscado; o ESUS sem <c>eap_id</c> espera sem gastar tentativa; o SISREG pede
/// guia nova quando a solicitação é remarcada; a janela que vira o dia vale; e a equipe não edita nem
/// exclui a guia.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class FilaGuiasTests(PostgresFixture fixture)
{
    static FilaGuiasTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static DateOnly Hoje => FusoBrasilia.HojeEmBrasilia();
    private static byte[] Pdf(string marca) => Encoding.UTF8.GetBytes("%PDF-1.4 " + marca);

    private sealed class ArmazenamentoFake : IArmazenamentoArquivos
    {
        private readonly ConcurrentDictionary<string, byte[]> _arquivos = new();

        public string MontarChaveDocumento(Guid pacienteId, Guid documentoId, string extensao = "pdf") =>
            $"teste/{pacienteId:D}/{documentoId:D}.{extensao}";

        public Task SalvarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default)
        {
            _arquivos[chave] = conteudo;
            return Task.CompletedTask;
        }

        public Task<byte[]?> LerAsync(string chave, CancellationToken cancellationToken = default) =>
            Task.FromResult(_arquivos.TryGetValue(chave, out var c) ? c : null);

        public Task ExcluirAsync(string chave, CancellationToken cancellationToken = default)
        {
            _arquivos.TryRemove(chave, out _);
            return Task.CompletedTask;
        }
    }

    private sealed record Cenario(
        FilaGuiasService Fila, DocumentosPacienteService Acervo,
        ISerLeitorService Ser, ISernitLeitorService Sernit, IEsusSgLeitorService Esus, IBaixadorGuia Sisreg);

    private static Cenario Montar(SmsMaisDbContext db)
    {
        var acervo = new DocumentosPacienteService(
            db, new ArmazenamentoFake(), Substitute.For<ICidadaoClinicoService>(), Substitute.For<ILaudoAssinaturaService>(),
            Substitute.For<IExameImagensPdfService>(), new UsuarioAtualAccessorFake(),
            NullLogger<DocumentosPacienteService>.Instance);

        var ser = Substitute.For<ISerLeitorService>();
        ser.EmitirGuiaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(c => Pdf("ser " + c.Arg<string>()));
        var sernit = Substitute.For<ISernitLeitorService>();
        sernit.EmitirGuiaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(c => Pdf("sernit " + c.Arg<string>()));
        var esus = Substitute.For<IEsusSgLeitorService>();
        esus.LerComprovanteExameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(EsusSgComprovanteExame.De(JsonDocument.Parse(ComprovanteJson).RootElement));
        var sisreg = Substitute.For<IBaixadorGuia>();
        sisreg.Sistema.Returns(SistemaRegulacao.Sisreg);
        sisreg.BaixarAsync(Arg.Any<GuiaAgendamento>(), Arg.Any<CancellationToken>())
            .Returns(c => new GuiaBaixada(Pdf("sisreg " + c.Arg<GuiaAgendamento>().Id), "Guia", "Guia SISREG", "guia.pdf"));

        IBaixadorGuia[] baixadores =
        [
            new BaixadorGuiaSer(db, ser, Substitute.For<IVarreduraSerFila>()),
            new BaixadorGuiaSernit(db, sernit, Substitute.For<IVarreduraSernitFila>()),
            new BaixadorGuiaEsusSg(db, esus, Substitute.For<IVarreduraEsusSgFila>()),
            sisreg,
        ];
        var fila = new FilaGuiasService(db, acervo, baixadores, NullLogger<FilaGuiasService>.Instance);
        return new Cenario(fila, acervo, ser, sernit, esus, sisreg);
    }

    /// <summary>O banco é compartilhado: a fila do sistema traz agendamentos de outros testes, e a
    /// ordem é pela data. Drena tudo até a fila ficar vazia (o que adia ou falha fica para depois).</summary>
    private static async Task DrenarAsync(FilaGuiasService fila, SistemaRegulacao sistema)
    {
        for (var i = 0; i < 1000; i++)
        {
            if (await fila.ProcessarProximaAsync(sistema, CancellationToken.None) == DesfechoFilaGuia.FilaVazia) return;
        }
        throw new InvalidOperationException("A fila não esvaziou.");
    }

    private static async Task<AgendamentoExterno> AgendamentoAsync(
        SmsMaisDbContext db, SistemaRegulacao sistema, DateOnly data,
        SituacaoAgendamentoExterno situacao = SituacaoAgendamentoExterno.Ativo, Guid? origemId = null)
    {
        var a = new AgendamentoExterno
        {
            Id = Guid.CreateVersion7(),
            Sistema = sistema,
            OrigemId = origemId ?? Guid.NewGuid(),
            NumeroExterno = Random.Shared.Next(10_000_000, 99_999_999).ToString(),
            PacienteId = Guid.NewGuid(),
            Tipo = TipoAgendamento.Consulta,
            Procedimento = "CONSULTA EM OFTALMOLOGIA - ESTRABISMO",
            Data = data,
            Hora = new TimeOnly(7, 0),
            LocalNome = "HOSPITAL DE EXEMPLO",
            Situacao = situacao,
            CriadoEm = DateTime.UtcNow,
        };
        db.AgendamentosExternos.Add(a);
        await db.SaveChangesAsync();
        return a;
    }

    private static Task<GuiaAgendamento?> GuiaDoExternoAsync(SmsMaisDbContext db, Guid agendamentoId) =>
        db.GuiasAgendamento.AsNoTracking().FirstOrDefaultAsync(g => g.AgendamentoExternoId == agendamentoId);

    // ===================== enfileirar =====================

    [Fact]
    public async Task Enfileira_vazia_uma_vez_e_descarta_o_que_deixou_de_valer()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var a = await AgendamentoAsync(db, SistemaRegulacao.Ser, Hoje.AddDays(5));
        var desmarcado = await AgendamentoAsync(db, SistemaRegulacao.Ser, Hoje.AddDays(5), SituacaoAgendamentoExterno.Desmarcado);

        await c.Fila.EnfileirarAsync(CancellationToken.None);
        await c.Fila.EnfileirarAsync(CancellationToken.None);

        var guia = await GuiaDoExternoAsync(db, a.Id);
        Assert.Equal(SituacaoGuiaAgendamento.Pendente, guia!.Situacao);
        Assert.Null(guia.DocumentoPacienteId);
        Assert.Equal(a.Data, guia.DataAtendimento);
        Assert.Equal(1, await db.GuiasAgendamento.CountAsync(g => g.AgendamentoExternoId == a.Id));
        Assert.Null(await GuiaDoExternoAsync(db, desmarcado.Id));

        // Desmarcou antes de a guia vir: sai da fila sem ir ao SER.
        await db.AgendamentosExternos.Where(x => x.Id == a.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Situacao, SituacaoAgendamentoExterno.Desmarcado));
        await c.Fila.EnfileirarAsync(CancellationToken.None);
        Assert.Equal(SituacaoGuiaAgendamento.Descartada, (await GuiaDoExternoAsync(db, a.Id))!.Situacao);
    }

    [Fact]
    public async Task Atendimento_de_hoje_nao_entra_e_o_que_ficou_pendente_sai_da_fila()
    {
        // 1ª rodada em produção (21/10/2026): no dia, o SER/SERNIT já tiraram o pedido de "Agendada"
        // (o paciente chegou) e o SISREG tinha 1.099 do dia — a fila vale de amanhã em diante.
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var deHoje = await AgendamentoAsync(db, SistemaRegulacao.Ser, Hoje);
        var pendenteDeHoje = await AgendamentoAsync(db, SistemaRegulacao.Sernit, Hoje);
        db.GuiasAgendamento.Add(new GuiaAgendamento
        {
            Id = Guid.CreateVersion7(), Sistema = SistemaRegulacao.Sernit, AgendamentoExternoId = pendenteDeHoje.Id,
            PacienteId = pendenteDeHoje.PacienteId, DataAtendimento = Hoje, CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await c.Fila.EnfileirarAsync(CancellationToken.None);
        await DrenarAsync(c.Fila, SistemaRegulacao.Ser);

        Assert.Null(await GuiaDoExternoAsync(db, deHoje.Id));
        await c.Ser.DidNotReceive().EmitirGuiaAsync(deHoje.NumeroExterno, Arg.Any<CancellationToken>());
        Assert.Equal(SituacaoGuiaAgendamento.Descartada, (await GuiaDoExternoAsync(db, pendenteDeHoje.Id))!.Situacao);
    }

    [Fact]
    public async Task SISREG_enfileira_pela_solicitacao_e_remarcacao_pede_guia_nova()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var dia = Hoje.AddDays(9);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(),
            dataAgendada: FusoBrasilia.DeBrasiliaParaUtc(dia.ToDateTime(new TimeOnly(8, 30))));
        var s = exame.Solicitacao!;
        s.Status = StatusSolicitacao.Agendada;
        s.CodigoSolicitacao = Random.Shared.Next(100_000_000, 999_999_999).ToString();
        await db.SaveChangesAsync();

        await c.Fila.EnfileirarAsync(CancellationToken.None);
        var primeira = await db.GuiasAgendamento.AsNoTracking().SingleAsync(g => g.SolicitacaoId == s.Id);
        Assert.Equal(SistemaRegulacao.Sisreg, primeira.Sistema);
        Assert.Equal(dia, primeira.DataAtendimento);

        // Remarcada: a guia da data velha sai da fila e entra a da data nova.
        s.DataAgendada = FusoBrasilia.DeBrasiliaParaUtc(dia.AddDays(7).ToDateTime(new TimeOnly(10, 0)));
        await db.SaveChangesAsync();
        await c.Fila.EnfileirarAsync(CancellationToken.None);

        var guias = await db.GuiasAgendamento.AsNoTracking().Where(g => g.SolicitacaoId == s.Id).ToListAsync();
        Assert.Equal(SituacaoGuiaAgendamento.Descartada, guias.Single(g => g.DataAtendimento == dia).Situacao);
        Assert.Equal(SituacaoGuiaAgendamento.Pendente, guias.Single(g => g.DataAtendimento == dia.AddDays(7)).Situacao);

        await DrenarAsync(c.Fila, SistemaRegulacao.Sisreg);
        var situacao = await c.Fila.SituacaoPorSolicitacaoAsync(s.Id, CancellationToken.None);
        Assert.Equal("Obtida", situacao!.Situacao);
        Assert.Equal(dia.AddDays(7), situacao.DataAgendamento);
    }

    // ===================== buscar =====================

    [Fact]
    public async Task Busca_a_guia_do_SER_uma_vez_e_guarda_no_acervo_do_paciente()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var a = await AgendamentoAsync(db, SistemaRegulacao.Ser, Hoje.AddDays(5));
        await c.Fila.EnfileirarAsync(CancellationToken.None);

        await DrenarAsync(c.Fila, SistemaRegulacao.Ser);
        await DrenarAsync(c.Fila, SistemaRegulacao.Ser);

        // Uma via só: a segunda passada não emite de novo.
        await c.Ser.Received(1).EmitirGuiaAsync(a.NumeroExterno, Arg.Any<CancellationToken>());

        var guia = await GuiaDoExternoAsync(db, a.Id);
        Assert.Equal(SituacaoGuiaAgendamento.Obtida, guia!.Situacao);
        var doc = await db.DocumentosPaciente.AsNoTracking().SingleAsync(d => d.Id == guia.DocumentoPacienteId);
        Assert.Equal(a.PacienteId, doc.PacienteId);
        Assert.Equal(OrigemDocumentoPaciente.GuiaAgendamento, doc.Origem);
        Assert.Equal(SituacaoDocumentoPaciente.Aceito, doc.Situacao);
        Assert.Contains(a.Data.ToString("dd/MM/yyyy"), doc.Titulo);

        // A tela do pedido e o acervo apontam para o MESMO arquivo.
        var conteudo = await c.Fila.ConteudoPorEspelhoAsync(SistemaRegulacao.Ser, a.OrigemId, CancellationToken.None);
        Assert.Equal(Pdf("ser " + a.NumeroExterno), conteudo.Conteudo);
    }

    [Fact]
    public async Task Falha_conta_tentativa_espera_e_desiste_depois_do_teto()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var a = await AgendamentoAsync(db, SistemaRegulacao.Sernit, Hoje.AddDays(3));
        c.Sernit.EmitirGuiaAsync(a.NumeroExterno, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GuiaSernitIndisponivelException("a linha não oferece a chave"));
        await c.Fila.EnfileirarAsync(CancellationToken.None);

        await DrenarAsync(c.Fila, SistemaRegulacao.Sernit);
        await DrenarAsync(c.Fila, SistemaRegulacao.Sernit);

        await c.Sernit.Received(1).EmitirGuiaAsync(a.NumeroExterno, Arg.Any<CancellationToken>());
        var guia = await GuiaDoExternoAsync(db, a.Id);
        Assert.Equal(SituacaoGuiaAgendamento.Pendente, guia!.Situacao);
        Assert.Equal(1, guia.Tentativas);
        Assert.Contains("não oferece a chave", guia.UltimoErro);
        Assert.True(guia.ProximaTentativaEm > DateTime.UtcNow.AddMinutes(20));

        // Última tentativa: vira Falhou e sai da fila.
        await db.GuiasAgendamento.Where(g => g.Id == guia.Id).ExecuteUpdateAsync(s => s
            .SetProperty(g => g.Tentativas, FilaGuiasService.MaximoTentativas - 1)
            .SetProperty(g => g.ProximaTentativaEm, (DateTime?)null));
        db.ChangeTracker.Clear(); // o processo usa um contexto novo por guia
        await DrenarAsync(c.Fila, SistemaRegulacao.Sernit);
        var falhou = await GuiaDoExternoAsync(db, a.Id);
        Assert.Equal(SituacaoGuiaAgendamento.Falhou, falhou!.Situacao);
        Assert.Null(falhou.ProximaTentativaEm);
    }

    [Fact]
    public async Task PDF_ja_guardado_e_ligado_sem_buscar_de_novo()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var a = await AgendamentoAsync(db, SistemaRegulacao.Ser, Hoje.AddDays(4));
        await c.Fila.EnfileirarAsync(CancellationToken.None);
        var guia = await GuiaDoExternoAsync(db, a.Id);
        // Vez anterior: trouxe e guardou, mas caiu antes de marcar a guia.
        var doc = await c.Acervo.AdicionarAsync(new NovoDocumentoPaciente(
            a.PacienteId, "Guia", null, "guia.pdf", "application/pdf", Pdf("anterior " + a.Id),
            OrigemDocumentoPaciente.GuiaAgendamento, FilaGuiasService.ReferenciaNoAcervo(guia!.Id),
            SituacaoDocumentoPaciente.Aceito));

        await DrenarAsync(c.Fila, SistemaRegulacao.Ser);

        await c.Ser.DidNotReceive().EmitirGuiaAsync(a.NumeroExterno, Arg.Any<CancellationToken>());
        var ligada = await GuiaDoExternoAsync(db, a.Id);
        Assert.Equal(SituacaoGuiaAgendamento.Obtida, ligada!.Situacao);
        Assert.Equal(doc.Id, ligada.DocumentoPacienteId);
    }

    [Fact]
    public async Task Esus_sem_eap_id_espera_sem_gastar_tentativa_e_com_ele_vira_PDF()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var data = Hoje.AddDays(8);
        var espelho = new EsusSgSolicitacao
        {
            Id = Guid.NewGuid(), IdEsusSg = Random.Shared.Next(1_000_000, 9_999_999).ToString(),
            Tipo = TipoRecursoEsusSg.Exame, Recurso = "TRATAMENTO DE RETINA (PPI)", PacienteNome = "PACIENTE (EXEMPLO)",
            Situacao = SituacaoEsusSg.Agendada, DataAgendada = data,
            SincronizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };
        db.EsusSgSolicitacoes.Add(espelho);
        await db.SaveChangesAsync();
        var a = await AgendamentoAsync(db, SistemaRegulacao.EsusSg, data, origemId: espelho.Id);
        await c.Fila.EnfileirarAsync(CancellationToken.None);

        await DrenarAsync(c.Fila, SistemaRegulacao.EsusSg);
        var adiada = await GuiaDoExternoAsync(db, a.Id);
        Assert.Equal(0, adiada!.Tentativas);
        Assert.NotNull(adiada.ProximaTentativaEm);

        espelho.AgendamentoIdEsus = "9000001";
        await db.SaveChangesAsync();
        await db.GuiasAgendamento.Where(g => g.Id == adiada.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ProximaTentativaEm, (DateTime?)null));
        db.ChangeTracker.Clear(); // o processo usa um contexto novo por guia
        await DrenarAsync(c.Fila, SistemaRegulacao.EsusSg);

        await c.Esus.Received(1).LerComprovanteExameAsync("9000001", Arg.Any<CancellationToken>());
        Assert.Equal(SituacaoGuiaAgendamento.Obtida, (await GuiaDoExternoAsync(db, a.Id))!.Situacao);
        var conteudo = await c.Fila.ConteudoPorEspelhoAsync(SistemaRegulacao.EsusSg, espelho.Id, CancellationToken.None);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(conteudo.Conteudo, 0, 4));
    }

    [Fact]
    public async Task A_equipe_nao_edita_nem_exclui_a_guia()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var a = await AgendamentoAsync(db, SistemaRegulacao.Ser, Hoje.AddDays(5));
        await c.Fila.EnfileirarAsync(CancellationToken.None);
        await DrenarAsync(c.Fila, SistemaRegulacao.Ser);
        var documento = (await GuiaDoExternoAsync(db, a.Id))!.DocumentoPacienteId!.Value;

        await Assert.ThrowsAsync<ConflitoException>(() => c.Acervo.ExcluirAsync(a.PacienteId, documento));
        await Assert.ThrowsAsync<ConflitoException>(() =>
            c.Acervo.EditarAsync(a.PacienteId, documento, new EditarDocumentoPacienteRequest("Outro", null)));
    }

    // ===================== configuração =====================

    [Fact]
    public async Task Configuracao_tem_padrao_por_sistema_e_valida_o_que_se_grava()
    {
        await using var db = fixture.CriarDbContext();
        var config = new FilaGuiasConfiguracaoService(db, new UsuarioAtualAccessorFake(), []);
        await db.GuiaFilaConfiguracoes.Where(x => x.Sistema == SistemaRegulacao.Sisreg).ExecuteDeleteAsync();

        // Todos nascem com o dia inteiro; o SISREG mais devagar (orçamento dividido com os motores).
        var padrao = await config.ResumoAsync(SistemaRegulacao.Sisreg, CancellationToken.None);
        Assert.Equal(("00:00", "00:00", 30), (padrao.JanelaInicio, padrao.JanelaFim, padrao.PorHora));
        Assert.Equal(60, (await config.ResumoAsync(SistemaRegulacao.Ser, CancellationToken.None)).PorHora);

        var salvo = await config.SalvarAsync(
            SistemaRegulacao.Sisreg, new SalvarFilaGuiasRequest(true, 100, "23:00", "05:30"), CancellationToken.None);
        Assert.Equal(100, salvo.PorHora);
        Assert.Equal("23:00", salvo.JanelaInicio);

        await Assert.ThrowsAsync<ValidacaoException>(() => config.SalvarAsync(
            SistemaRegulacao.Sisreg, new SalvarFilaGuiasRequest(true, 0, "23:00", "05:30"), CancellationToken.None));
        await Assert.ThrowsAsync<ValidacaoException>(() => config.SalvarAsync(
            SistemaRegulacao.Sisreg, new SalvarFilaGuiasRequest(true, 60, "25:00", "05:30"), CancellationToken.None));
    }

    [Theory]
    [InlineData("23:30", "22:00", "06:00", true)]
    [InlineData("03:00", "22:00", "06:00", true)]
    [InlineData("12:00", "22:00", "06:00", false)]
    [InlineData("06:00", "22:00", "06:00", false)]
    [InlineData("09:00", "08:00", "18:00", true)]
    [InlineData("18:00", "08:00", "18:00", false)]
    [InlineData("15:00", "00:00", "00:00", true)]
    public void Janela_que_vira_o_dia_vale(string agora, string inicio, string fim, bool dentro) =>
        Assert.Equal(dentro, JanelaDoDia.Contem(TimeOnly.Parse(agora), TimeOnly.Parse(inicio), TimeOnly.Parse(fim)));

    [Fact]
    public async Task CAPTCHA_pausa_a_fila_do_sistema_sem_gastar_tentativa_e_salvar_retoma()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var config = new FilaGuiasConfiguracaoService(db, new UsuarioAtualAccessorFake(), [c.Sisreg]);
        await db.GuiaFilaConfiguracoes.Where(x => x.Sistema == SistemaRegulacao.Sisreg).ExecuteDeleteAsync();
        c.Sisreg.ImpedimentoAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        c.Sisreg.BaixarAsync(Arg.Any<GuiaAgendamento>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FilaGuiasPausadaException("O SISREG pediu CAPTCHA.", TimeSpan.FromHours(24)));

        var dia = Hoje.AddDays(2);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(),
            dataAgendada: FusoBrasilia.DeBrasiliaParaUtc(dia.ToDateTime(new TimeOnly(9, 0))));
        exame.Solicitacao!.Status = StatusSolicitacao.Agendada;
        exame.Solicitacao.CodigoSolicitacao = Random.Shared.Next(100_000_000, 999_999_999).ToString();
        await db.SaveChangesAsync();
        await c.Fila.EnfileirarAsync(CancellationToken.None);

        Assert.Equal(DesfechoFilaGuia.Adiada, await c.Fila.ProcessarProximaAsync(SistemaRegulacao.Sisreg, CancellationToken.None));

        var guia = await db.GuiasAgendamento.AsNoTracking().SingleAsync(g => g.SolicitacaoId == exame.Solicitacao.Id);
        Assert.Equal(0, guia.Tentativas);
        var resumo = await config.ResumoAsync(SistemaRegulacao.Sisreg, CancellationToken.None);
        Assert.NotNull(resumo.PausadaAte);
        Assert.Equal("O SISREG pediu CAPTCHA.", resumo.Impedimento);

        // Alguém resolveu o CAPTCHA e salvou a configuração: a fila volta.
        var salvo = await config.SalvarAsync(
            SistemaRegulacao.Sisreg, new SalvarFilaGuiasRequest(true, 30, "00:00", "00:00"), CancellationToken.None);
        Assert.Null(salvo.PausadaAte);
        Assert.Null(salvo.Impedimento);
    }

    // ===================== ficha do SISREG =====================

    /// <summary>A ficha do <c>cons_marcados_reg</c> com a estrutura medida (21/10/2026) e dados
    /// inventados: chave fora do padrão, seções, rótulos em cinza, a visão reduzida escondida (que
    /// fica de fora), dois procedimentos e o rodapé de extração.</summary>
    private const string FichaSisreg = """
        <html><body><FORM NAME="formulario" METHOD="POST">
        <div class="fichaAmbulatorial"><div id="fichaAmbulatorial"><table class='table_listagem' width='700'>
        <tr><td><b>Chave de Confirma&#231;&#227;o:</b></td></tr>
        <tr><td style='font-size: 180%;'><b>12345</b></td></tr>
        <tbody class="FichaCompleta">
        <tr><td colspan='6' class='td_titulo_tabela'>UNIDADE SOLICITANTE</td></tr>
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>Unidade Solicitante:</b></td><td colspan='1'><b>C&#243;d. CNES:</b></td><td colspan='1'><b>Op. Solicitante:</b></td><td colspan='1'><b>Op. Videofonista:</b></td></tr>
        <tr><td colspan='3'>UNIDADE DE EXEMPLO</td><td colspan='1'>0000001</td><td colspan='1'>OPERADOR.EXEMPLO</td><td colspan='1'>---</td></tr>
        </tbody>
        <tr><td colspan='6' class='td_titulo_tabela'>UNIDADE EXECUTANTE</td></tr>
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>Unidade Executante:</b></td><td colspan='3'><b>Data e Hor&aacute;rio de Atendimento:</b></td></tr>
        <tr><td colspan='3'>HOSPITAL DE EXEMPLO</td><td colspan='3'><font size="2"><b>SEX&nbsp;&#9679;&nbsp;30/10/2026&nbsp;&#9679;&nbsp;07h00min</b></font></td></tr>
        <tr><td colspan='6' class='td_titulo_tabela'>Dados do Paciente</td></tr>
        <tbody class="FichaCompleta">
        <tr bgcolor='#F3F3F3'><td colspan='6'><b>CNS:</b></td></tr>
        <tr><td colspan='6'><font color="blue">700000000000000</font></td></tr>
        <tr bgcolor='#F3F3F3'><td colspan='6'><b>Telefone(s):</b></td></tr>
        <tr><td colspan='6'>(21) 0000-0000 <a href="#">(Exibir Lista Detalhada)</a></td></tr>
        </tbody>
        <tbody class="FichaReduzida" style="display:none">
        <tr bgcolor='#F3F3F3'><td colspan='6'><b>CNS:</b></td></tr>
        <tr><td colspan='6'>NAO DEVE APARECER</td></tr>
        </tbody>
        <tr><td colspan='6' class='td_titulo_tabela'>Dados da Solicita&ccedil;&atilde;o</td></tr>
        <tbody class="FichaCompleta">
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>C&#243;digo da Solicita&#231;&#227;o:</b></td><td colspan='3'><b>Situa&#231;&#227;o Atual:</b></td></tr>
        <tr><td colspan='3'><font color="blue">670000001</font></td><td colspan='3'>AUTORIZADA / AGENDADA / REGULADOR</td></tr>
        </tbody>
        <tr bgcolor='#F3F3F3'><td colspan='3'><b>Procedimentos Solicitados:</b></td><td colspan='1'><b>C&#243;d. Unificado:</b></td><td colspan='2'><b>C&#243;d. Interno:</b></td></tr>
        <tr><td colspan='3'>MAMOGRAFIA BILATERAL</td><td colspan='1'>0204030188</td><td colspan='2'>0701</td></tr>
        <tr><td colspan='3'>ULTRASSONOGRAFIA MAMARIA</td><td colspan='1'>0205020097</td><td colspan='2'>0702</td></tr>
        </table>
        <p class="somente_tela somente_impressao">Data da Extra&ccedil;&atilde;o dos Dados: 21/10/2026 12:00</p>
        </div></div>
        <input type='button' value='IMPRIMIR' onClick='imprimirFicha()'>
        </FORM></body></html>
        """;

    [Fact]
    public void Ficha_do_SISREG_sai_por_secao_sem_a_visao_reduzida()
    {
        var f = FichaSisregHtmlParser.Ler(FichaSisreg)!;

        Assert.Equal("12345", f.Chave);
        Assert.Equal("670000001", f.CodigoSolicitacao);
        Assert.Equal(
            ["UNIDADE SOLICITANTE", "UNIDADE EXECUTANTE", "Dados do Paciente", "Dados da Solicitação"],
            f.Secoes.Select(s => s.Titulo));
        Assert.Equal("SEX · 30/10/2026 · 07h00min", f.Valor("Data e Horário de Atendimento"));
        Assert.Equal("(21) 0000-0000", f.Valor("Telefone(s)"));
        // "---" é vazio no SISREG, e a visão reduzida (escondida) não entra.
        Assert.Null(f.Valor("Op. Videofonista"));
        Assert.DoesNotContain(f.Secoes.SelectMany(s => s.Campos), c => c.Valor == "NAO DEVE APARECER");
        // Um par por procedimento.
        Assert.Equal(2, f.Secoes.Last().Campos.Count(c => c.Rotulo == "Procedimentos Solicitados"));
        Assert.Equal("Data da Extração dos Dados: 21/10/2026 12:00", f.ExtraidaEm);

        var pdf = GuiaSisregPdf.Gerar(f, new DateTime(2026, 10, 21, 12, 0, 0));
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Pagina_sem_ficha_nao_vira_guia()
    {
        Assert.Null(FichaSisregHtmlParser.Ler("<html><body>Sessão expirada</body></html>"));
        Assert.Null(FichaSisregHtmlParser.Ler("<div class='fichaAmbulatorial'></div>"));
    }

    // ===================== pedaços sem banco =====================

    /// <summary>O item do SER como a tela entrega (21/10/2026): o <c>&lt;a&gt;</c> do commandLink não
    /// tem id; o parâmetro está no <c>onclick</c>.</summary>
    private const string LinhaComGuia = """
        <table id="form0:listagem"><tbody><tr><td>
        <div class="rich-menu-item" id="form0:listagem:0:j_id162" onclick="RichFaces.Menu.updateItem(event,this);">
          <span class="rich-menu-item-label" id="form0:listagem:0:j_id162:anchor">
            <a class="bold" href="#" onclick="if(typeof jsfcljs == 'function'){jsfcljs(document.getElementById('form0'),{'form0:listagem:0:j_id163':'form0:listagem:0:j_id163'},'');}return false">Emitir CHAVE DE AUTORIZAÇÃO DO AGENDAMENTO - 2ª via</a>
          </span>
        </div>
        <div class="rich-menu-item" id="form0:listagem:0:j_id164">
          <a href="#" id="form0:listagem:0:j_id165" onclick="A4J.AJAX.Submit('form0',event,{});return false;">Historico da Solicitação</a>
        </div>
        </td></tr></tbody></table>
        """;

    [Fact]
    public void Item_da_guia_sai_do_onclick_do_commandLink()
    {
        Assert.Equal("form0:listagem:0:j_id163", SerHtmlParser.ItemGuia(SerHtmlParser.Documento(LinhaComGuia), 0));
        Assert.Equal("form0:listagem:0:j_id163", SernitHtmlParser.ItemGuia(SernitHtmlParser.Documento(LinhaComGuia), 0));
        // O item de outra linha não serve.
        Assert.Null(SerHtmlParser.ItemGuia(SerHtmlParser.Documento(LinhaComGuia), 1));
    }

    [Fact]
    public void Trava_geral_do_ESUS_continua_recusando_o_comprovante()
    {
        // Só a porta nomeada (ObterComprovanteExameAsync) passa por este caminho.
        Assert.Throws<EscritaNoEsusSgBloqueadaException>(() =>
            EsusSgSessao.GarantirLeitura(EsusSgSessao.CaminhoComprovanteExame));
        Assert.Throws<EscritaNoEsusSgBloqueadaException>(() =>
            EsusSgSessao.GarantirLeitura("exames2/controller-exame-agendamento-procedimento/cadastrar-comprovante-impressao-lote"));
    }

    private const string ComprovanteJson = """
        {
          "cli_nome": "Sao Goncalo", "uns_nome": "OFTALMOCLÍNICA DE EXEMPLO - 2291525",
          "tlg_nome": "RUA", "end_logradouro": "DE EXEMPLO", "end_numero": "40", "end_complemento": "",
          "bai_nome": "CENTRO", "mun_nome": "SAO GONCALO", "end_referencia": "",
          "eha_data_exame": "13/10/2026", "eap_hora": "13:25",
          "pes_nome": "PACIENTE (EXEMPLO)", "pes_nome_social": "", "ppr_numero": "", "pac_prontuario_unico": "0000000001",
          "crs_numero": "700000000000000", "set_nome": "TRATAMENTO DE RETINA", "lca_nome": "ÚNICO",
          "stp_novo_nome_procedimento": "TRATAMENTO DE RETINA (PPI)", "subprocedimentos": "TONOMETRIA",
          "atendente": "Profissional de Exemplo", "ocp_nome": "MÉDICO OFTALMOLOGISTA",
          "observacoes": "Trazer a identidade / Cartao Sus", "data_marcacao": "30/09/2026", "hora_marcacao": "12:21",
          "usu_nome": "OPERADOR DE EXEMPLO", "agendadoPelaFila": "Paciente agendado pela fila",
          "lblNomeRegulador": "Profissional Regulador:", "nomeRegulador": "", "eap_senha_efetivacao": "",
          "recomendacaoPreparo": "<p>Jejum de <b>2 horas</b>.</p><p>Trazer exames anteriores.</p>"
        }
        """;

    [Fact]
    public void Comprovante_do_ESUS_vira_texto_e_PDF()
    {
        var c = EsusSgComprovanteExame.De(JsonDocument.Parse(ComprovanteJson).RootElement);

        Assert.Equal("RUA DE EXEMPLO, 40 - CENTRO SAO GONCALO", c.Endereco);
        Assert.Equal("30/09/2026 às 12:21", c.MarcadoEm);
        Assert.Null(c.Regulador);
        Assert.Equal("Jejum de 2 horas.\nTrazer exames anteriores.", c.RecomendacaoPreparo);

        var pdf = GuiaEsusSgPdf.Gerar(c, new DateTime(2026, 10, 21, 12, 0, 0));
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
