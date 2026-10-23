using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Alertas;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Aviso automático da remarcação feita no SISREG (23/10/2026). A sincronização já detectava a
/// data nova e a registrava na fila de Alterações da agenda; agora o paciente que tinha a data
/// antiga recebe a confirmação de novo, e quem tinha confirmado volta a "sem resposta". Com volume
/// de leitura errada, nada sai: a unidade inteira fica para a equipe.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AvisoRemarcacaoTests(PostgresFixture fixture)
{
    private const string CodigoProcedimento = "1402147";

    private static IConfirmacaoConfiguracaoService Regras(bool ligado = true, DateTime? ligadoEm = null)
    {
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>()).Returns(new ConfirmacaoConfiguracaoDto(
            "08:00", "18:00", 100, SomenteSisreg: true, JanelaAbertaAgora: true, AtualizadoEm: null,
            AvisoRemarcacaoHabilitado: ligado,
            // Corte recente: o banco dos testes é compartilhado, e só as remarcações destes testes contam.
            AvisoRemarcacaoLigadoEm: ligadoEm ?? DateTime.UtcNow.AddHours(-1)));
        return regras;
    }

    /// <summary>Catálogo da Meta com os dois modelos de mudança de data aprovados (padrão) ou sem eles.</summary>
    private static IWhatsAppCliente Catalogo(bool modelosDeMudancaAprovados = true)
    {
        var whats = Substitute.For<IWhatsAppCliente>();
        var modelos = new List<TemplateWhatsApp> { new("confirmacao_regulacao", "pt_BR", "UTILITY", null, 7, []) };
        if (modelosDeMudancaAprovados)
        {
            modelos.Add(new("agendamento_data_alterada", "pt_BR", "UTILITY", null, 4, []));
            modelos.Add(new("agendamento_data_alterada_anonimo", "pt_BR", "UTILITY", null, 2, []));
        }
        whats.ListarTemplatesAsync(Arg.Any<CancellationToken>()).Returns(modelos);
        return whats;
    }

    private static AvisoRemarcacaoService CriarService(
        SmsMaisDbContext db, IConfirmacaoConfiguracaoService? regras = null, IAlertaPlataforma? alerta = null,
        IWhatsAppCliente? catalogo = null)
    {
        regras ??= Regras();
        var comunicacoes = new ComunicacaoPacienteService(db,
            Substitute.For<IPacientesService>(),
            Substitute.For<ICidadaoLoginLinkService>(),
            Substitute.For<IWhatsAppCliente>(),
            Options.Create(new ComunicacaoPacienteOptions()),
            new UsuarioAtualAccessorFake(),
            Substitute.For<SMSMais.Core.Telefones.IDispensaContatoService>(),
            Substitute.For<IContatoComprometidoService>(),
            regras,
            NullLogger<ComunicacaoPacienteService>.Instance);
        return new AvisoRemarcacaoService(db, regras, comunicacoes, catalogo ?? Catalogo(),
            Options.Create(new ComunicacaoPacienteOptions()),
            alerta ?? Substitute.For<IAlertaPlataforma>(), NullLogger<AvisoRemarcacaoService>.Instance);
    }

    private sealed record Cenario(Guid ExameId, Guid SolicitacaoId, Guid UnidadeId, Guid AlteracaoId);

    /// <summary>
    /// Uma solicitação do SISREG remarcada de <c>dataAntiga</c> para daqui a 10 dias, numa unidade
    /// com o aviso ligado, e a linha que a sincronização gravou na fila de alterações.
    /// </summary>
    private static async Task<Cenario> CriarAsync(
        SmsMaisDbContext db,
        StatusConfirmacaoAgendamento resposta = StatusConfirmacaoAgendamento.Confirmada,
        StatusComunicacao? comunicacao = StatusComunicacao.Lida,
        bool unidadeAvisa = true,
        DateTime? detectadaEm = null)
    {
        var dataNova = DateTime.UtcNow.AddDays(10);
        var dataAntiga = DateTime.UtcNow.AddDays(3);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: dataNova, confirmacao: resposta);
        var s = await db.Solicitacoes.FirstAsync(x => x.Id == exame.SolicitacaoId);
        s.Status = StatusSolicitacao.Agendada;
        s.ProcedimentoCodigoSisreg = CodigoProcedimento;
        s.RawSisreg = "linha do SISREG";
        s.FonteCriacao = FonteSolicitacao.ImportacaoSisreg;
        if (resposta == StatusConfirmacaoAgendamento.Confirmada)
        {
            s.ConfirmadoEm = DateTime.UtcNow.AddDays(-2);
            s.ConfirmadoCanal = "whatsapp-link";
        }

        db.SisregVarreduraAgendas.Add(new SisregVarreduraAgenda
        {
            UnidadeId = s.UnidadeExecutanteId, EnviarConfirmacao = unidadeAvisa, AtualizadoEm = DateTime.UtcNow,
        });
        var prof = new SisregProfissionalUnidade
        {
            Id = Guid.CreateVersion7(), UnidadeId = s.UnidadeExecutanteId, Cpf = "24156779653",
            Nome = "PROFISSIONAL DE TESTE (exemplo)", Habilitado = true, CriadoEm = DateTime.UtcNow,
        };
        db.SisregProfissionaisUnidade.Add(prof);
        db.SisregProcedimentosProfissional.Add(new SisregProcedimentoProfissional
        {
            Id = Guid.CreateVersion7(), ProfissionalId = prof.Id, Codigo = CodigoProcedimento,
            Nome = "ULTRA-SONOGRAFIA DE MAMAS BILATERAL", Habilitado = true, EnviarConfirmacao = true,
        });

        if (comunicacao is { } status)
        {
            var enviada = status is StatusComunicacao.Enviada or StatusComunicacao.Entregue or StatusComunicacao.Lida;
            db.ComunicacoesPaciente.Add(new ComunicacaoPaciente
            {
                Id = Guid.CreateVersion7(),
                Tipo = TipoAgendamento.Exame,
                Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
                SolicitacaoId = s.Id,
                PacienteId = s.PacienteId,
                Status = status,
                EnviadoEm = enviada ? DateTime.UtcNow.AddDays(-3) : null,
                LidoEm = status == StatusComunicacao.Lida ? DateTime.UtcNow.AddDays(-3) : null,
                ProximaTentativaEm = status == StatusComunicacao.Pendente ? DateTime.UtcNow.AddHours(1) : null,
                CriadoEm = DateTime.UtcNow.AddDays(-3),
            });
        }

        var alteracao = new SisregAlteracaoAgenda
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = s.Id,
            CodigoSolicitacao = "123456789",
            Tipo = TipoAlteracaoAgenda.DataHora,
            ValorAntes = ComparadorMarcacao.FormatarData(dataAntiga),
            ValorDepois = ComparadorMarcacao.FormatarData(dataNova),
            UnidadeExecutanteId = s.UnidadeExecutanteId,
            DetectadaEm = detectadaEm ?? DateTime.UtcNow.AddMinutes(-10),
        };
        db.SisregAlteracoesAgenda.Add(alteracao);
        await db.SaveChangesAsync();

        return new(exame.Id, s.Id, s.UnidadeExecutanteId, alteracao.Id);
    }

    [Fact]
    public async Task Quem_confirmou_a_data_antiga_recebe_a_confirmacao_de_novo_e_volta_a_sem_resposta()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db);

        await CriarService(db).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var s = await db.Solicitacoes.AsNoTracking().SingleAsync(x => x.Id == c.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Pendente, s.StatusConfirmacao);
        Assert.Null(s.ConfirmadoEm);

        var confirmacao = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x =>
            x.SolicitacaoId == c.SolicitacaoId && x.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento);
        Assert.Equal(StatusComunicacao.Pendente, confirmacao.Status); // o enviador pega, com a data de agora
        Assert.Null(confirmacao.EnviadoEm);

        var a = await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == c.AlteracaoId);
        Assert.NotNull(a.AvisoAutomaticoEm);
        Assert.NotNull(a.ComunicadaEm);
        Assert.Null(a.ComunicadaPor); // foi o automático
        Assert.NotNull(a.TratadaEm);  // sai da fila da equipe
        Assert.Contains("Aviso de mudança de data", a.NotaAviso);
        Assert.Contains("deixou de valer", a.NotaAviso);

        // Ele conhecia a data antiga: sai o aviso de mudança de data, e a data anterior fica guardada
        // para a mensagem com os dados.
        Assert.Equal(a.ValorAntes, ComparadorMarcacao.FormatarData(confirmacao.RemarcadaDe));
    }

    [Fact]
    public async Task Quem_esperava_a_identificacao_e_tinha_confirmado_recebe_o_aviso_de_mudanca()
    {
        await using var db = fixture.CriarDbContext();
        // Recebeu só a primeira mensagem (sem data), mas confirmou por outro caminho — sabia a data.
        var c = await CriarAsync(db, StatusConfirmacaoAgendamento.Confirmada, StatusComunicacao.AguardandoVerificacaoCadastral);

        await CriarService(db).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var confirmacao = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x =>
            x.SolicitacaoId == c.SolicitacaoId && x.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento);
        Assert.Equal(StatusComunicacao.Pendente, confirmacao.Status); // sai de novo, agora como aviso de mudança
        Assert.NotNull(confirmacao.RemarcadaDe);
        var a = await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == c.AlteracaoId);
        Assert.NotNull(a.ComunicadaEm);
    }

    [Fact]
    public async Task Sem_os_modelos_aprovados_a_mensagem_espera_e_a_remarcacao_fica_visivel_para_a_equipe()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db);

        await CriarService(db, catalogo: Catalogo(modelosDeMudancaAprovados: false)).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var a = await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == c.AlteracaoId);
        Assert.Null(a.TratadaEm);
        Assert.Null(a.ComunicadaEm);
        Assert.Contains("aprovados como utilidade", a.NotaAviso);

        // Já está na fila, marcada como mudança de data: sai sozinha quando a Meta aprovar.
        var confirmacao = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x =>
            x.SolicitacaoId == c.SolicitacaoId && x.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento);
        Assert.Equal(StatusComunicacao.Pendente, confirmacao.Status);
        Assert.NotNull(confirmacao.RemarcadaDe);
    }

    [Fact]
    public async Task Quem_nunca_soube_da_data_antiga_recebe_a_confirmacao_comum()
    {
        await using var db = fixture.CriarDbContext();
        // Não confirmou e a mensagem com a data ainda não tinha saído.
        var c = await CriarAsync(db, StatusConfirmacaoAgendamento.Pendente, StatusComunicacao.Pendente);

        await CriarService(db).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var confirmacao = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x =>
            x.SolicitacaoId == c.SolicitacaoId && x.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento);
        Assert.Null(confirmacao.RemarcadaDe);
    }

    [Fact]
    public async Task Mensagem_que_ainda_nao_tinha_saido_vai_com_a_data_nova_sem_reenvio()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db, StatusConfirmacaoAgendamento.Pendente, StatusComunicacao.Pendente);

        await CriarService(db).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var a = await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == c.AlteracaoId);
        Assert.NotNull(a.TratadaEm);
        Assert.Null(a.ComunicadaEm);
        Assert.Contains("sai com a nova", a.NotaAviso);
    }

    [Fact]
    public async Task Unidade_sem_aviso_por_whatsapp_fica_na_fila_da_equipe_e_a_confirmacao_antiga_deixa_de_valer()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db, unidadeAvisa: false);

        await CriarService(db).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var a = await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == c.AlteracaoId);
        Assert.NotNull(a.AvisoAutomaticoEm);
        Assert.Null(a.TratadaEm); // continua para a equipe
        Assert.Contains("telefone", a.NotaAviso);

        var confirmacao = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x =>
            x.SolicitacaoId == c.SolicitacaoId && x.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento);
        Assert.Equal(StatusComunicacao.Lida, confirmacao.Status); // nada saiu de novo
        var s = await db.Solicitacoes.AsNoTracking().SingleAsync(x => x.Id == c.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Pendente, s.StatusConfirmacao);
    }

    [Fact]
    public async Task Volume_de_leitura_errada_segura_a_unidade_inteira_e_avisa_no_celular()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db);
        // Mais 24 remarcações na mesma unidade em 24 h, contra uma agenda futura de 1 — leitura errada.
        for (var i = 0; i < AvisoRemarcacaoService.FreioMinimo + 4; i++)
        {
            db.SisregAlteracoesAgenda.Add(new SisregAlteracaoAgenda
            {
                Id = Guid.CreateVersion7(), SolicitacaoId = c.SolicitacaoId, Tipo = TipoAlteracaoAgenda.DataHora,
                ValorAntes = $"0{i % 9 + 1}/01/2027 08:00", ValorDepois = $"0{i % 9 + 1}/02/2027 08:00",
                UnidadeExecutanteId = c.UnidadeId, DetectadaEm = DateTime.UtcNow.AddHours(-2),
                TratadaEm = DateTime.UtcNow, // já tratadas: contam para o volume, não para a passada
            });
        }
        await db.SaveChangesAsync();
        var alerta = Substitute.For<IAlertaPlataforma>();

        await CriarService(db, alerta: alerta).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var a = await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == c.AlteracaoId);
        Assert.NotNull(a.AvisoAutomaticoEm);
        Assert.Null(a.TratadaEm);
        Assert.StartsWith("Retida", a.NotaAviso);
        var s = await db.Solicitacoes.AsNoTracking().SingleAsync(x => x.Id == c.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Confirmada, s.StatusConfirmacao); // nada foi desfeito
        alerta.Received().Reportar(Arg.Is<EventoAlerta>(e => e.Chave == AlertaCatalogo.SisregRemarcacaoRetida));
    }

    [Fact]
    public async Task Enquanto_a_leitura_da_unidade_corre_a_remarcacao_espera()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db);
        db.SisregVarreduraExecucoes.Add(new SisregVarreduraExecucao
        {
            Id = Guid.CreateVersion7(), UnidadeId = c.UnidadeId, UnidadeNome = "UNIDADE TESTE",
            Status = StatusVarredura.EmExecucao, IniciadoEm = DateTime.UtcNow.AddMinutes(-20),
            UltimoSinalEm = DateTime.UtcNow.AddSeconds(-30),
        });
        await db.SaveChangesAsync();

        await CriarService(db).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var a = await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == c.AlteracaoId);
        Assert.Null(a.AvisoAutomaticoEm);
    }

    [Fact]
    public async Task Ligar_vale_daqui_para_frente_e_desligado_nao_olha_nada()
    {
        await using var db = fixture.CriarDbContext();
        var antiga = await CriarAsync(db, detectadaEm: DateTime.UtcNow.AddHours(-3));
        var nova = await CriarAsync(db);

        await CriarService(db, Regras(ligado: false)).ProcessarPendentesAsync();
        db.ChangeTracker.Clear();
        Assert.Null((await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == nova.AlteracaoId)).AvisoAutomaticoEm);

        await CriarService(db, Regras(ligadoEm: DateTime.UtcNow.AddHours(-1))).ProcessarPendentesAsync();
        db.ChangeTracker.Clear();
        Assert.Null((await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == antiga.AlteracaoId)).AvisoAutomaticoEm);
        Assert.NotNull((await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == nova.AlteracaoId)).AvisoAutomaticoEm);
    }

    [Fact]
    public async Task Quem_disse_que_nao_ia_fica_para_a_equipe_com_o_nao_vou_preservado()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db, StatusConfirmacaoAgendamento.Cancelada);

        await CriarService(db).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var s = await db.Solicitacoes.AsNoTracking().SingleAsync(x => x.Id == c.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Cancelada, s.StatusConfirmacao);
        var a = await db.SisregAlteracoesAgenda.AsNoTracking().SingleAsync(x => x.Id == c.AlteracaoId);
        Assert.Null(a.TratadaEm);
    }

    [Fact]
    public async Task A_remarcacao_aparece_no_historico_da_solicitacao_com_o_que_foi_feito()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db);
        await CriarService(db).ProcessarPendentesAsync();
        db.ChangeTracker.Clear();

        var historico = await new SolicitacaoHistoricoService(db, new UsuarioAtualAccessorFake()).ObterAsync(c.ExameId);

        var evento = Assert.Single(historico.Eventos, e => e.Id == c.AlteracaoId);
        Assert.Equal("RemarcadaNoSisreg", evento.Acao);
        Assert.NotNull(evento.ValorAnterior);
        Assert.NotNull(evento.ValorNovo);
        Assert.Contains("mudança de data", evento.Detalhe);
    }

    [Fact]
    public async Task Avisar_paciente_pela_fila_reabre_ate_o_nao_vou_e_poe_a_confirmacao_para_sair()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db, StatusConfirmacaoAgendamento.Cancelada);

        var preparado = await CriarService(db).PrepararAvisoManualAsync(c.SolicitacaoId);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        Assert.NotNull(preparado.ComunicacaoId);
        var s = await db.Solicitacoes.AsNoTracking().SingleAsync(x => x.Id == c.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Pendente, s.StatusConfirmacao);
        var confirmacao = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == preparado.ComunicacaoId);
        Assert.Equal(StatusComunicacao.Pendente, confirmacao.Status);
        Assert.Contains("deixou de valer", preparado.Nota);
    }

    [Fact]
    public async Task O_lembrete_da_data_antiga_volta_a_valer_para_a_nova()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db);
        // O lembrete da data antiga já tinha saído.
        db.ComunicacoesPaciente.Add(new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(), Tipo = TipoAgendamento.Exame,
            Finalidade = FinalidadeComunicacao.LembreteAgendamento, SolicitacaoId = c.SolicitacaoId,
            PacienteId = Guid.NewGuid(), Status = StatusComunicacao.Lida,
            EnviadoEm = DateTime.UtcNow.AddDays(-1), CriadoEm = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        await CriarService(db).ProcessarPendentesAsync();

        db.ChangeTracker.Clear();
        var lembrete = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x =>
            x.SolicitacaoId == c.SolicitacaoId && x.Finalidade == FinalidadeComunicacao.LembreteAgendamento);
        Assert.Equal(StatusComunicacao.Dispensada, lembrete.Status);
        Assert.StartsWith(AvisoRemarcacaoService.CarimboLembreteRemarcado, lembrete.MotivoFalha);

        // Às vésperas da data nova (lembrete de 30 dias para cair na janela), o mesmo lembrete é rearmado.
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>()).Returns(new ConfirmacaoConfiguracaoDto(
            "08:00", "18:00", 100, SomenteSisreg: true, JanelaAbertaAgora: true, AtualizadoEm: null,
            LembreteDiasAntes: 30, LembreteHabilitado: true));
        var lembretes = new LembreteAgendamentoService(db, regras,
            Substitute.For<IComunicacaoPacienteService>(),
            Options.Create(new ComunicacaoPacienteOptions()),
            NullLogger<LembreteAgendamentoService>.Instance);

        await lembretes.EnfileirarDevidosAsync();

        db.ChangeTracker.Clear();
        lembrete = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == lembrete.Id);
        Assert.Equal(StatusComunicacao.Pendente, lembrete.Status);
        Assert.Null(lembrete.EnviadoEm);
    }
}
