using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Notificacoes.AgendamentosExternos;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Telefones;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes.AgendamentosExternos;

/// <summary>
/// Lembrete X dias antes e aviso de cancelamento dos agendamentos do SER, do SERNIT e do ESUS SG
/// (21/10/2026) — com as chaves na tela de Configuração de cada regulação.
///
/// <para>O que estes testes guardam: o cancelamento só é avisado quando o sistema DIZ que o atendimento
/// caiu (cancelado ou devolvido à fila), espera a carência e é dispensado se o pedido voltar com o mesmo
/// dia; o texto é o do SISREG, anônimo para número não provado e com "voltou para a fila" quando é o
/// caso; o lembrete segue as regras do SISREG (silêncio de 7 dias, quem disse que não vai fica de fora)
/// e a resposta a ele cai no agendamento externo.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class LembreteECancelamentoExternoTests(PostgresFixture fixture)
{
    private readonly string _telefone = SeedSolicitacao.TelefoneAleatorio();
    private static DateOnly Hoje => FusoBrasilia.HojeEmBrasilia();

    // ===================== infraestrutura =====================

    private static IConfirmacaoConfiguracaoService Regras()
    {
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>()).Returns(new ConfirmacaoConfiguracaoDto(
            "08:00", "18:00", 100, SomenteSisreg: true, true, null, AvisoAgendamentoExternoHabilitado: true));
        return regras;
    }

    private static IWhatsAppCliente Whats()
    {
        var w = Substitute.For<IWhatsAppCliente>();
        w.ListarTemplatesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TemplateWhatsApp>());
        w.EnviarTemplateAsync(default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.PRIMEIRA.{Guid.NewGuid():N}", null));
        w.EnviarTemplateComBotoesAsync(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.MODELO.{Guid.NewGuid():N}", null));
        w.EnviarTextoAsync(null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.TXT.{Guid.NewGuid():N}", null));
        w.EnviarInterativoBotoesAsync(null!, null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.INT.{Guid.NewGuid():N}", null));
        return w;
    }

    private static IAvisoRegulacaoConfiguracaoService Avisos(
        bool lembrete = false, int dias = 2, bool cancelamento = false, DateTime? cancelamentoLigadoEm = null) =>
        AvisoAgendamentoExternoTests.AvisosDaRegulacao(lembrete, dias, cancelamento, cancelamentoLigadoEm);

    private ComunicacaoPacienteService Comunicacoes(
        SmsMaisDbContext db, IWhatsAppCliente whats, Guid pacienteId, IAvisoRegulacaoConfiguracaoService avisos,
        bool verificado = false)
    {
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(pacienteId, Arg.Any<CancellationToken>()).Returns(
            PacienteDtoFabrica.Criar(pacienteId, "MONICA PEREIRA (EXEMPLO)", cpf: SeedSolicitacao.CpfAleatorio(),
                    nascimento: new DateOnly(2003, 1, 1), sexo: Sexo.Feminino)
                with { TelefoneCelular = _telefone, TelefoneVerificado = verificado ? _telefone : null });
        return new ComunicacaoPacienteService(
            db, pacientes, Substitute.For<ICidadaoLoginLinkService>(), whats,
            Options.Create(new ComunicacaoPacienteOptions()), new UsuarioAtualAccessorFake(),
            Substitute.For<IDispensaContatoService>(), Substitute.For<IContatoComprometidoService>(),
            Regras(), NullLogger<ComunicacaoPacienteService>.Instance, avisosRegulacao: avisos);
    }

    private AgendamentoExternoService Varredura(SmsMaisDbContext db, IAvisoRegulacaoConfiguracaoService avisos) =>
        new(db, Comunicacoes(db, Whats(), Guid.NewGuid(), avisos), Regras(), avisos,
            NullLogger<AgendamentoExternoService>.Instance);

    private static string Numero() => Random.Shared.Next(10_000_000, 99_999_999).ToString();

    private static async Task<SerSolicitacao> SerAsync(SmsMaisDbContext db, DateOnly data)
    {
        var agora = DateTime.UtcNow;
        var s = new SerSolicitacao
        {
            Id = Guid.NewGuid(),
            IdSer = Numero(),
            Tipo = TipoRecursoSer.Consulta,
            Recurso = "CONSULTA EM OFTALMOLOGIA - GERAL",
            PacienteNome = "PACIENTE (EXEMPLO)",
            PacienteId = Guid.NewGuid(),
            Situacao = SituacaoSer.Agendada,
            AgendadoParaTexto = data.ToString("dd/MM/yyyy"),
            UnidadeExecutora = "HOSPITAL DE EXEMPLO (RIO DE JANEIRO)",
            SituacaoMudouEm = agora.AddHours(-2),
            SincronizadoEm = agora,
            CriadoEm = agora,
        };
        db.SerSolicitacoes.Add(s);
        db.SerEventos.Add(new SerEvento
        {
            Id = Guid.NewGuid(),
            SerSolicitacaoId = s.Id,
            DataEvento = agora.AddHours(-3),
            Evento = "Agendar",
            TipoEvento = TipoEventoExterno.Agendar,
            Observacao = $"Data do agendamento: {data:dd/MM/yyyy} 08:30 |",
            CapturadoEm = agora,
        });
        await db.SaveChangesAsync();
        return s;
    }

    private static async Task MudarSituacaoAsync(SmsMaisDbContext db, Guid serId, SituacaoSer situacao)
    {
        var linha = await db.SerSolicitacoes.SingleAsync(x => x.Id == serId);
        linha.Situacao = situacao;
        await db.SaveChangesAsync();
    }

    private static Task<List<ComunicacaoPaciente>> AvisosAsync(
        SmsMaisDbContext db, Guid origemId, FinalidadeComunicacao finalidade) =>
        db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.AgendamentoExterno!.OrigemId == origemId && c.Finalidade == finalidade)
            .ToListAsync();

    /// <summary>Um agendamento externo e uma comunicação dele, já gravados.</summary>
    private static async Task<(AgendamentoExterno A, ComunicacaoPaciente C)> ComunicacaoAsync(
        SmsMaisDbContext db, Guid pacienteId, FinalidadeComunicacao finalidade,
        SituacaoAgendamentoExterno situacao = SituacaoAgendamentoExterno.Ativo,
        MotivoDesmarqueExterno? motivo = null,
        StatusConfirmacaoAgendamento confirmacao = StatusConfirmacaoAgendamento.Pendente,
        TimeOnly? hora = null, int diasAteAtendimento = 2)
    {
        var a = new AgendamentoExterno
        {
            Id = Guid.CreateVersion7(),
            Sistema = SistemaRegulacao.Ser,
            OrigemId = Guid.NewGuid(),
            NumeroExterno = Numero(),
            PacienteId = pacienteId,
            Tipo = TipoAgendamento.Consulta,
            Procedimento = "CONSULTA EM OFTALMOLOGIA - GERAL",
            Data = Hoje.AddDays(diasAteAtendimento),
            Hora = hora ?? new TimeOnly(8, 30),
            LocalNome = "HOSPITAL DE EXEMPLO",
            Situacao = situacao,
            MotivoDesmarque = motivo,
            StatusConfirmacao = confirmacao,
            CriadoEm = DateTime.UtcNow,
        };
        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Consulta,
            Finalidade = finalidade,
            AgendamentoExternoId = a.Id,
            PacienteId = pacienteId,
            Status = StatusComunicacao.Pendente,
            ProximaTentativaEm = DateTime.UtcNow,
            IgnorarJanelaHorario = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.AgendamentosExternos.Add(a);
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();
        return (a, c);
    }

    private static object?[] ArgumentosDoModelo(IWhatsAppCliente whats) =>
        Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateComBotoesAsync)).GetArguments();

    // ===================== varredura: quando o cancelamento é avisado =====================

    [Fact]
    public async Task Cancelado_no_ser_com_a_chave_ligada_poe_o_aviso_na_fila_com_carencia()
    {
        await using var db = fixture.CriarDbContext();
        var avisos = Avisos(cancelamento: true);
        var ser = await SerAsync(db, Hoje.AddDays(9));
        await Varredura(db, avisos).SincronizarAsync();

        await MudarSituacaoAsync(db, ser.Id, SituacaoSer.Cancelada);
        var antes = DateTime.UtcNow;
        await Varredura(db, avisos).SincronizarAsync();

        var a = await db.AgendamentosExternos.AsNoTracking().SingleAsync(x => x.OrigemId == ser.Id);
        Assert.Equal(SituacaoAgendamentoExterno.Desmarcado, a.Situacao);
        Assert.Equal(MotivoDesmarqueExterno.Cancelado, a.MotivoDesmarque);
        var aviso = Assert.Single(await AvisosAsync(db, ser.Id, FinalidadeComunicacao.CancelamentoAgendamento));
        Assert.Equal(StatusComunicacao.Pendente, aviso.Status);
        Assert.True(aviso.ProximaTentativaEm >= antes + AgendamentoExternoService.CarenciaDoCancelamento - TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Chave_desligada_e_pendencia_nao_avisam_cancelamento()
    {
        await using var db = fixture.CriarDbContext();
        var desligado = await SerAsync(db, Hoje.AddDays(9));
        var pendente = await SerAsync(db, Hoje.AddDays(9));
        await Varredura(db, Avisos()).SincronizarAsync();

        await MudarSituacaoAsync(db, desligado.Id, SituacaoSer.Cancelada);
        await Varredura(db, Avisos()).SincronizarAsync();
        Assert.Empty(await AvisosAsync(db, desligado.Id, FinalidadeComunicacao.CancelamentoAgendamento));

        // Pendência às vezes volta com o mesmo dia: não se diz "foi cancelado" a quem pode seguir marcado.
        await MudarSituacaoAsync(db, pendente.Id, SituacaoSer.Pendente);
        await Varredura(db, Avisos(cancelamento: true)).SincronizarAsync();
        var a = await db.AgendamentosExternos.AsNoTracking().SingleAsync(x => x.OrigemId == pendente.Id);
        Assert.Equal(MotivoDesmarqueExterno.Outro, a.MotivoDesmarque);
        Assert.Empty(await AvisosAsync(db, pendente.Id, FinalidadeComunicacao.CancelamentoAgendamento));
    }

    [Fact]
    public async Task Devolvido_a_fila_e_avisado_e_voltando_com_o_mesmo_dia_o_aviso_nao_sai()
    {
        await using var db = fixture.CriarDbContext();
        var avisos = Avisos(cancelamento: true);
        var ser = await SerAsync(db, Hoje.AddDays(9));
        await Varredura(db, avisos).SincronizarAsync();

        await MudarSituacaoAsync(db, ser.Id, SituacaoSer.EmFila);
        await Varredura(db, avisos).SincronizarAsync();
        var a = await db.AgendamentosExternos.AsNoTracking().SingleAsync(x => x.OrigemId == ser.Id);
        Assert.Equal(MotivoDesmarqueExterno.VoltouParaFila, a.MotivoDesmarque);
        Assert.Equal(StatusComunicacao.Pendente,
            Assert.Single(await AvisosAsync(db, ser.Id, FinalidadeComunicacao.CancelamentoAgendamento)).Status);

        // A regulação marcou de novo o MESMO dia antes de o aviso sair.
        await MudarSituacaoAsync(db, ser.Id, SituacaoSer.Agendada);
        await Varredura(db, avisos).SincronizarAsync();

        var cancelamento = Assert.Single(await AvisosAsync(db, ser.Id, FinalidadeComunicacao.CancelamentoAgendamento));
        Assert.Equal(StatusComunicacao.Dispensada, cancelamento.Status);
        var vigente = await db.AgendamentosExternos.AsNoTracking()
            .SingleAsync(x => x.OrigemId == ser.Id && x.Situacao == SituacaoAgendamentoExterno.Ativo);
        Assert.Null(vigente.MotivoDesmarque);
    }

    [Fact]
    public async Task Esus_de_volta_na_fila_avisa_e_saida_da_fila_nao()
    {
        await using var db = fixture.CriarDbContext();
        var avisos = Avisos(cancelamento: true);
        EsusSgSolicitacao Esus() => new()
        {
            Id = Guid.NewGuid(), IdEsusSg = Numero(), Tipo = TipoRecursoEsusSg.Exame, Recurso = "TRATAMENTO DE RETINA",
            PacienteNome = "PACIENTE (EXEMPLO)", PacienteId = Guid.NewGuid(), Situacao = SituacaoEsusSg.Agendada,
            DataAgendada = Hoje.AddDays(6), DataHoraAgendadaTexto = $"{Hoje.AddDays(6):dd/MM/yyyy} 13:25:00",
            UnidadeExecutora = "OFTALMOCLÍNICA DE EXEMPLO - 2291525",
            SincronizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };
        var voltou = Esus();
        var saiu = Esus();
        db.EsusSgSolicitacoes.AddRange(voltou, saiu);
        await db.SaveChangesAsync();
        await Varredura(db, avisos).SincronizarAsync();

        (await db.EsusSgSolicitacoes.SingleAsync(x => x.Id == voltou.Id)).Situacao = SituacaoEsusSg.EmFila;
        (await db.EsusSgSolicitacoes.SingleAsync(x => x.Id == saiu.Id)).Situacao = SituacaoEsusSg.SaiuDaFila;
        await db.SaveChangesAsync();
        await Varredura(db, avisos).SincronizarAsync();

        Assert.Single(await AvisosAsync(db, voltou.Id, FinalidadeComunicacao.CancelamentoAgendamento));
        Assert.Empty(await AvisosAsync(db, saiu.Id, FinalidadeComunicacao.CancelamentoAgendamento));
    }

    // ===================== envio do cancelamento =====================

    [Fact]
    public async Task Aviso_de_cancelamento_sai_no_modelo_do_sisreg()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (_, c) = await ComunicacaoAsync(db, paciente, FinalidadeComunicacao.CancelamentoAgendamento,
            SituacaoAgendamentoExterno.Desmarcado, MotivoDesmarqueExterno.Cancelado);
        var whats = Whats();

        await Comunicacoes(db, whats, paciente, Avisos(cancelamento: true)).ProcessarTentativaEnvioAsync(c.Id);

        var args = ArgumentosDoModelo(whats);
        Assert.Equal(new ComunicacaoPacienteOptions().TemplateCancelamento, args[1]);
        Assert.Equal(new[] { "Sra. Monica", "sua consulta foi cancelada" }, (IReadOnlyList<string>)args[3]!);
        Assert.Empty((IReadOnlyList<BotaoTemplateWhatsApp>)args[4]!);
        Assert.Equal(StatusComunicacao.Enviada,
            (await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id)).Status);
    }

    [Fact]
    public async Task Agendamento_que_voltou_a_valer_nao_recebe_cancelamento()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (_, c) = await ComunicacaoAsync(db, paciente, FinalidadeComunicacao.CancelamentoAgendamento);
        var whats = Whats();

        await Comunicacoes(db, whats, paciente, Avisos(cancelamento: true)).ProcessarTentativaEnvioAsync(c.Id);

        Assert.DoesNotContain(whats.ReceivedCalls(), x => x.GetMethodInfo().Name.StartsWith("Enviar"));
        Assert.Equal(StatusComunicacao.Dispensada,
            (await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id)).Status);
    }

    [Fact]
    public async Task Cancelamento_de_antes_de_ligar_o_aviso_nao_sai()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (_, c) = await ComunicacaoAsync(db, paciente, FinalidadeComunicacao.CancelamentoAgendamento,
            SituacaoAgendamentoExterno.Desmarcado, MotivoDesmarqueExterno.VoltouParaFila);
        var whats = Whats();

        await Comunicacoes(db, whats, paciente, Avisos(cancelamento: true, cancelamentoLigadoEm: DateTime.UtcNow.AddMinutes(5)))
            .ProcessarTentativaEnvioAsync(c.Id);

        var depois = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Falha, depois.Status);
        Assert.StartsWith("Aviso retroativo", depois.MotivoFalha);
        Assert.DoesNotContain(whats.ReceivedCalls(), x => x.GetMethodInfo().Name.StartsWith("Enviar"));
    }

    [Fact]
    public async Task Chave_desligada_segura_o_cancelamento_na_fila()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (_, c) = await ComunicacaoAsync(db, paciente, FinalidadeComunicacao.CancelamentoAgendamento,
            SituacaoAgendamentoExterno.Desmarcado, MotivoDesmarqueExterno.Cancelado);

        await Comunicacoes(db, Whats(), paciente, Avisos()).ProcessarTentativaEnvioAsync(c.Id);

        var depois = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Pendente, depois.Status);
        Assert.Equal(0, depois.Tentativas);
    }

    // ===================== lembrete =====================

    private LembreteAgendamentoExternoService Lembretes(SmsMaisDbContext db, IAvisoRegulacaoConfiguracaoService avisos) =>
        new(db, avisos, Comunicacoes(db, Whats(), Guid.NewGuid(), avisos),
            Options.Create(new ComunicacaoPacienteOptions()), NullLogger<LembreteAgendamentoExternoService>.Instance);

    private static async Task<AgendamentoExterno> AgendamentoAsync(
        SmsMaisDbContext db, int diasAteAtendimento,
        StatusConfirmacaoAgendamento confirmacao = StatusConfirmacaoAgendamento.Pendente,
        DateTime? avisoEnviadoEm = null)
    {
        var a = new AgendamentoExterno
        {
            Id = Guid.CreateVersion7(), Sistema = SistemaRegulacao.Sernit, OrigemId = Guid.NewGuid(),
            NumeroExterno = Numero(), PacienteId = Guid.NewGuid(), Tipo = TipoAgendamento.Exame,
            Procedimento = "AUDIOMETRIA", Data = Hoje.AddDays(diasAteAtendimento), Hora = new TimeOnly(14, 0),
            Situacao = SituacaoAgendamentoExterno.Ativo, StatusConfirmacao = confirmacao, CriadoEm = DateTime.UtcNow,
        };
        db.AgendamentosExternos.Add(a);
        if (avisoEnviadoEm is { } enviado)
            db.ComunicacoesPaciente.Add(new ComunicacaoPaciente
            {
                Id = Guid.CreateVersion7(), Tipo = TipoAgendamento.Exame,
                Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento, AgendamentoExternoId = a.Id,
                PacienteId = a.PacienteId, Status = StatusComunicacao.Lida, EnviadoEm = enviado, CriadoEm = enviado,
            });
        await db.SaveChangesAsync();
        return a;
    }

    [Fact]
    public async Task Lembrete_entra_na_janela_de_dias_e_respeita_o_silencio_e_quem_nao_vai()
    {
        await using var db = fixture.CriarDbContext();
        var devido = await AgendamentoAsync(db, 1, avisoEnviadoEm: DateTime.UtcNow.AddDays(-10));
        var longe = await AgendamentoAsync(db, 6, avisoEnviadoEm: DateTime.UtcNow.AddDays(-10));
        var avisadoAgora = await AgendamentoAsync(db, 1, avisoEnviadoEm: DateTime.UtcNow.AddDays(-2));
        var naoVai = await AgendamentoAsync(db, 1, StatusConfirmacaoAgendamento.Cancelada);

        await Lembretes(db, Avisos(lembrete: true, dias: 2)).EnfileirarDevidosAsync();
        await Lembretes(db, Avisos(lembrete: true, dias: 2)).EnfileirarDevidosAsync(); // idempotente

        Assert.Single(await AvisosAsync(db, devido.OrigemId, FinalidadeComunicacao.LembreteAgendamento));
        Assert.Empty(await AvisosAsync(db, longe.OrigemId, FinalidadeComunicacao.LembreteAgendamento));
        Assert.Empty(await AvisosAsync(db, avisadoAgora.OrigemId, FinalidadeComunicacao.LembreteAgendamento));
        Assert.Empty(await AvisosAsync(db, naoVai.OrigemId, FinalidadeComunicacao.LembreteAgendamento));
    }

    [Fact]
    public async Task Lembrete_desligado_nao_enfileira()
    {
        await using var db = fixture.CriarDbContext();
        var a = await AgendamentoAsync(db, 1);

        Assert.Equal(0, await Lembretes(db, Avisos(lembrete: false)).EnfileirarDevidosAsync());
        Assert.Empty(await AvisosAsync(db, a.OrigemId, FinalidadeComunicacao.LembreteAgendamento));
    }

    [Fact]
    public async Task Lembrete_de_quem_confirmou_sai_com_dia_e_hora_e_quem_nao_respondeu_recebe_a_primeira_mensagem()
    {
        await using var db = fixture.CriarDbContext();
        var avisos = Avisos(lembrete: true);

        var confirmou = Guid.NewGuid();
        var (a, c) = await ComunicacaoAsync(db, confirmou, FinalidadeComunicacao.LembreteAgendamento,
            confirmacao: StatusConfirmacaoAgendamento.Confirmada, hora: new TimeOnly(14, 0));
        var whats = Whats();
        await Comunicacoes(db, whats, confirmou, avisos, verificado: true).ProcessarTentativaEnvioAsync(c.Id);

        var args = ArgumentosDoModelo(whats);
        Assert.Equal(new ComunicacaoPacienteOptions().TemplateLembreteConfirmado, args[1]);
        Assert.Equal(
            new[] { "Sra. Monica", "Sua consulta de OFTALMOLOGIA - GERAL", a.Data.ToString("dd/MM/yyyy"), "14:00h" },
            (IReadOnlyList<string>)args[3]!);

        var naoRespondeu = Guid.NewGuid();
        var (_, c2) = await ComunicacaoAsync(db, naoRespondeu, FinalidadeComunicacao.LembreteAgendamento);
        var whats2 = Whats();
        await Comunicacoes(db, whats2, naoRespondeu, avisos, verificado: true).ProcessarTentativaEnvioAsync(c2.Id);

        var primeira = Assert.Single(whats2.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync)).GetArguments();
        Assert.Equal(new ComunicacaoPacienteOptions().TemplateConfirmacaoConsulta, primeira[1]);
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral,
            (await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c2.Id)).Status);
    }

    [Fact]
    public async Task Lembrete_de_quem_disse_que_nao_vai_e_dispensado_no_envio()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (_, c) = await ComunicacaoAsync(db, paciente, FinalidadeComunicacao.LembreteAgendamento,
            confirmacao: StatusConfirmacaoAgendamento.Cancelada);
        var whats = Whats();

        await Comunicacoes(db, whats, paciente, Avisos(lembrete: true)).ProcessarTentativaEnvioAsync(c.Id);

        Assert.DoesNotContain(whats.ReceivedCalls(), x => x.GetMethodInfo().Name.StartsWith("Enviar"));
        Assert.Equal(StatusComunicacao.Dispensada,
            (await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id)).Status);
    }

    // ===================== resposta ao lembrete =====================

    private ManipuladorContexto Contexto(Guid pacienteId, string texto, string? contextoWamid)
    {
        var conversa = new Conversa
        {
            Id = Guid.NewGuid(), Canal = CanalConversa.WhatsApp, TelefoneCanonical = _telefone, PacienteId = pacienteId,
            Status = StatusConversa.Aberta, PrimeiroContatoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };
        var msg = new MensagemWhatsApp
        {
            Id = Guid.NewGuid(), Telefone = _telefone, Direcao = DirecaoMensagem.Entrada, Conteudo = texto,
            Status = StatusMensagemWhatsApp.Recebida, OcorridoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
            ContextoWaMessageId = contextoWamid,
        };
        return new ManipuladorContexto(conversa, msg, texto, pacienteId, null, null);
    }

    /// <summary>O lembrete com os dados já enviado, com a mensagem que a pessoa responde.</summary>
    private async Task<(AgendamentoExterno A, string Wamid)> LembreteEnviadoAsync(SmsMaisDbContext db, Guid paciente)
    {
        var (a, c) = await ComunicacaoAsync(db, paciente, FinalidadeComunicacao.LembreteAgendamento,
            confirmacao: StatusConfirmacaoAgendamento.Confirmada);
        var wamid = $"wamid.LEMBRETE.{Guid.NewGuid():N}";
        var msg = new MensagemWhatsApp
        {
            Id = Guid.NewGuid(), Telefone = _telefone, Direcao = DirecaoMensagem.Saida, WaMessageId = wamid,
            Status = StatusMensagemWhatsApp.Enviada, OcorridoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };
        db.MensagensWhatsApp.Add(msg);
        var linha = await db.ComunicacoesPaciente.SingleAsync(x => x.Id == c.Id);
        linha.Status = StatusComunicacao.Lida;
        linha.Telefone = _telefone;
        linha.EnviadoEm = DateTime.UtcNow;
        linha.MensagemWhatsAppId = msg.Id;
        await db.SaveChangesAsync();
        return (a, wamid);
    }

    [Fact]
    public async Task Nao_poderei_ir_no_lembrete_pergunta_se_cancela_o_agendamento_externo()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (a, wamid) = await LembreteEnviadoAsync(db, paciente);
        var whats = Whats();
        var handler = new ConfirmacaoAgendamentoWhatsAppHandler(db, whats, NullLogger<ConfirmacaoAgendamentoWhatsAppHandler>.Instance);

        var ctx = Contexto(paciente, "Não poderei ir.", wamid);
        await handler.TratarAsync(ctx, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.True(ctx.Consumido);
        var pergunta = Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarInterativoBotoesAsync)).GetArguments();
        Assert.Equal(new[] { $"cancext_sim:{a.Id}", $"cancext_nao:{a.Id}" },
            ((IReadOnlyList<BotaoInterativoWhatsApp>)pergunta[2]!).Select(b => b.Id));
    }

    [Fact]
    public async Task Sim_esta_confirmado_no_lembrete_mantem_o_agendamento_externo_confirmado()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (a, wamid) = await LembreteEnviadoAsync(db, paciente);
        var whats = Whats();
        var handler = new ConfirmacaoAgendamentoWhatsAppHandler(db, whats, NullLogger<ConfirmacaoAgendamentoWhatsAppHandler>.Instance);

        var ctx = Contexto(paciente, "Sim! Está confirmado!", wamid);
        await handler.TratarAsync(ctx, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.True(ctx.Consumido);
        Assert.Equal(StatusConfirmacaoAgendamento.Confirmada,
            (await db.AgendamentosExternos.AsNoTracking().SingleAsync(x => x.Id == a.Id)).StatusConfirmacao);
        var resposta = (string)Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTextoAsync)).GetArguments()[1]!;
        Assert.Contains("*CONFIRMADA*", resposta);
        Assert.DoesNotContain("guia", resposta, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== configuração =====================

    [Fact]
    public async Task Ligar_o_aviso_de_cancelamento_carimba_a_hora_e_o_sisreg_grava_nas_chaves_da_mensageria()
    {
        await using var db = fixture.CriarDbContext();
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        var geral = new ConfirmacaoConfiguracaoDto("08:00", "18:00", 100, true, true, null,
            LembreteDiasAntes: 2, LembreteHabilitado: false, ConciliacaoCancelamentoHabilitada: true);
        regras.ObterAsync(Arg.Any<CancellationToken>()).Returns(geral);
        regras.SalvarAsync(Arg.Any<SalvarConfirmacaoConfiguracaoRequest>(), Arg.Any<CancellationToken>())
            .Returns(geral with { LembreteHabilitado = true, LembreteDiasAntes = 3 });
        var servico = new AvisoRegulacaoConfiguracaoService(db, regras, new UsuarioAtualAccessorFake());

        // A tabela é uma só para a base de testes inteira: o que este teste grava, ele apaga.
        try
        {
            var antes = DateTime.UtcNow;
            var ser = await servico.SalvarAsync(SistemaRegulacao.Ser, new SalvarAvisosPacienteRegulacaoRequest(true, 3, true));
            Assert.True(ser.LembreteHabilitado);
            Assert.Equal(3, ser.LembreteDiasAntes);
            Assert.True(ser.AvisoCancelamentoLigadoEm >= antes.AddSeconds(-1));
            var ligadoEm = ser.AvisoCancelamentoLigadoEm;

            // Salvar de novo com o aviso ligado não move o corte.
            var deNovo = await servico.SalvarAsync(SistemaRegulacao.Ser, new SalvarAvisosPacienteRegulacaoRequest(false, 3, true));
            Assert.Equal(ligadoEm, deNovo.AvisoCancelamentoLigadoEm);

            var externas = await servico.RegrasExternasAsync();
            Assert.True(externas[SistemaRegulacao.Ser].AvisoCancelamentoHabilitado);
            Assert.False(externas[SistemaRegulacao.Sernit].AvisoCancelamentoHabilitado);

            var sisreg = await servico.SalvarAsync(SistemaRegulacao.Sisreg, new SalvarAvisosPacienteRegulacaoRequest(true, 3, false));
            Assert.Equal("sisreg", sisreg.Sistema);
            await regras.Received(1).SalvarAsync(
                Arg.Is<SalvarConfirmacaoConfiguracaoRequest>(r => r.LembreteHabilitado && r.LembreteDiasAntes == 3
                    && r.ConciliacaoCancelamentoHabilitada && r.HoraInicioEnvio == "08:00"),
                Arg.Any<CancellationToken>());

            await Assert.ThrowsAsync<SMSMais.Core.Common.Excecoes.ValidacaoException>(() =>
                servico.SalvarAsync(SistemaRegulacao.Sernit, new SalvarAvisosPacienteRegulacaoRequest(true, 31, false)));
        }
        finally
        {
            await db.AvisosRegulacaoConfiguracao.ExecuteDeleteAsync();
        }
    }
}

/// <summary>Os textos do aviso de cancelamento do SER/SERNIT/ESUS SG — sem banco.</summary>
public class TextosCancelamentoExternoTests
{
    [Fact]
    public void Cancelamento_e_anonimo_para_numero_nao_provado()
    {
        var a = new AgendamentoExterno
        {
            Tipo = TipoAgendamento.Consulta, Procedimento = "CONSULTA EM OFTALMOLOGIA - GERAL",
            Data = new DateOnly(2026, 10, 30), Hora = new TimeOnly(8, 30),
            MotivoDesmarque = MotivoDesmarqueExterno.Cancelado,
        };
        Assert.Equal("sua consulta foi cancelada", ComunicacaoPacienteService.FraseCancelamentoExterno(a, false));
        Assert.Equal("sua consulta de OFTALMOLOGIA - GERAL marcada para o dia 30/10/2026 às 08:30h foi cancelada",
            ComunicacaoPacienteService.FraseCancelamentoExterno(a, true));
    }

    [Fact]
    public void Devolvido_a_fila_diz_que_foi_desmarcado_e_voltou_para_a_fila()
    {
        var a = new AgendamentoExterno
        {
            Tipo = TipoAgendamento.Exame, Procedimento = "TRATAMENTO DE RETINA", Data = new DateOnly(2026, 10, 30),
            MotivoDesmarque = MotivoDesmarqueExterno.VoltouParaFila,
        };
        Assert.Equal("seu exame foi desmarcado e o pedido voltou para a fila da regulação",
            ComunicacaoPacienteService.FraseCancelamentoExterno(a, false));
        Assert.Equal(
            "seu exame de TRATAMENTO DE RETINA marcado para o dia 30/10/2026 foi desmarcado e o pedido voltou para a fila da regulação",
            ComunicacaoPacienteService.FraseCancelamentoExterno(a, true));
    }
}
