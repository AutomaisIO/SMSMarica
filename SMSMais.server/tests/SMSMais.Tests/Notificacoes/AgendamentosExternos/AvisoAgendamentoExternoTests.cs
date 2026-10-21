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
using SMSMais.Data.Entities.Sernit;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes.AgendamentosExternos;

/// <summary>
/// O aviso dos agendamentos do SER, do SERNIT e do ESUS SG pelo WhatsApp (10/10/2026). Até então só
/// o SISREG avisava: o paciente encaminhado para fora do município só sabia da data se alguém da
/// equipe mandasse a mensagem à mão (caso real: pedido do SER agendado para 19/10, ninguém avisado).
///
/// <para>O que estes testes guardam: a varredura acha o agendamento no espelho (com a hora do evento
/// "Agendar" do SER) e põe UM aviso na fila; a remarcação gera aviso novo e encerra o velho; o que
/// sai de "Agendada" não sai; a primeira mensagem é a mesma do SISREG (sem dado nenhum); depois da
/// identificação vão o procedimento, a data e o NOME do local — sem endereço (decisão do dono,
/// 11/10/2026) e sem mandar ao posto buscar guia; e a resposta do paciente fica no agendamento, não
/// no SER.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AvisoAgendamentoExternoTests(PostgresFixture fixture)
{
    private readonly string _telefone = SeedSolicitacao.TelefoneAleatorio();
    private static DateOnly Hoje => FusoBrasilia.HojeEmBrasilia();

    // ===================== infraestrutura =====================

    private static IConfirmacaoConfiguracaoService Regras(bool avisoExterno = true)
    {
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>()).Returns(new ConfirmacaoConfiguracaoDto(
            "08:00", "18:00", 100, SomenteSisreg: true, true, null,
            AvisoAgendamentoExternoHabilitado: avisoExterno));
        return regras;
    }

    private static IWhatsAppCliente Whats()
    {
        var w = Substitute.For<IWhatsAppCliente>();
        w.ListarTemplatesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TemplateWhatsApp>());
        w.EnviarTemplateAsync(default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.PRIMEIRA.{Guid.NewGuid():N}", null));
        w.EnviarTextoAsync(null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.TXT.{Guid.NewGuid():N}", null));
        w.EnviarInterativoBotoesAsync(null!, null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.INT.{Guid.NewGuid():N}", null));
        return w;
    }

    private ComunicacaoPacienteService Comunicacoes(
        SmsMaisDbContext db, IWhatsAppCliente whats, Guid pacienteId, bool verificado = false,
        bool avisoExterno = true, DateOnly? obito = null)
    {
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(pacienteId, Arg.Any<CancellationToken>()).Returns(
            PacienteDtoFabrica.Criar(pacienteId, "MONICA PEREIRA (EXEMPLO)", cpf: SeedSolicitacao.CpfAleatorio(),
                    nascimento: new DateOnly(2003, 1, 1), sexo: Sexo.Feminino)
                with { TelefoneCelular = _telefone, TelefoneVerificado = verificado ? _telefone : null, DataObito = obito });
        return new ComunicacaoPacienteService(
            db, pacientes, Substitute.For<ICidadaoLoginLinkService>(), whats,
            Options.Create(new ComunicacaoPacienteOptions()), new UsuarioAtualAccessorFake(),
            Substitute.For<IDispensaContatoService>(), Substitute.For<IContatoComprometidoService>(),
            Regras(avisoExterno), NullLogger<ComunicacaoPacienteService>.Instance,
            avisosRegulacao: AvisosDaRegulacao());
    }

    private AgendamentoExternoService Varredura(SmsMaisDbContext db, bool avisoExterno = true) =>
        new(db, Comunicacoes(db, Whats(), Guid.NewGuid(), avisoExterno: avisoExterno), Regras(avisoExterno),
            AvisosDaRegulacao(), NullLogger<AgendamentoExternoService>.Instance);

    /// <summary>As chaves de lembrete e cancelamento das regulações externas, sem passar pela tabela
    /// (que é uma só para a base de testes inteira). Padrão: tudo desligado, como nasce.</summary>
    internal static IAvisoRegulacaoConfiguracaoService AvisosDaRegulacao(
        bool lembrete = false, int dias = 2, bool cancelamento = false, DateTime? cancelamentoLigadoEm = null)
    {
        var regras = new[] { SistemaRegulacao.Ser, SistemaRegulacao.Sernit, SistemaRegulacao.EsusSg }
            .ToDictionary(s => s, s => new AvisoRegulacaoRegras(s, lembrete, dias, cancelamento,
                cancelamento ? cancelamentoLigadoEm ?? DateTime.UtcNow.AddDays(-1) : null));
        var avisos = Substitute.For<IAvisoRegulacaoConfiguracaoService>();
        avisos.RegrasExternasAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyDictionary<SistemaRegulacao, AvisoRegulacaoRegras>)regras);
        return avisos;
    }

    private static string Numero() => Random.Shared.Next(10_000_000, 99_999_999).ToString();

    private static async Task<SerSolicitacao> SerAsync(
        SmsMaisDbContext db, Guid pacienteId, DateOnly data, string? horaNoEvento = "08:30",
        DateTime? mudouEm = null, string? local = null)
    {
        var agora = DateTime.UtcNow;
        var s = new SerSolicitacao
        {
            Id = Guid.NewGuid(),
            IdSer = Numero(),
            Tipo = TipoRecursoSer.Consulta,
            Recurso = "CONSULTA EM ODONTOLOGIA  - CIRURGIA BUCO-MAXILO FACIAL",
            PacienteNome = "PACIENTE (EXEMPLO)",
            PacienteId = pacienteId,
            Situacao = SituacaoSer.Agendada,
            AgendadoParaTexto = data.ToString("dd/MM/yyyy"),
            UnidadeExecutora = local ?? $"HOSPITAL FEDERAL DE EXEMPLO {Guid.NewGuid():N}"[..40] + " (RIO DE JANEIRO)",
            SituacaoMudouEm = mudouEm ?? agora.AddHours(-2),
            SincronizadoEm = agora,
            CriadoEm = agora,
        };
        db.SerSolicitacoes.Add(s);
        if (horaNoEvento is not null)
        {
            db.SerEventos.Add(new SerEvento
            {
                Id = Guid.NewGuid(),
                SerSolicitacaoId = s.Id,
                DataEvento = agora.AddHours(-3),
                Evento = "Agendar",
                TipoEvento = TipoEventoExterno.Agendar,
                EstadoAnterior = "Em fila",
                EstadoAtual = "Agendada",
                Observacao = $"Data do agendamento: {data:dd/MM/yyyy} {horaNoEvento} |",
                CapturadoEm = agora,
            });
        }
        await db.SaveChangesAsync();
        return s;
    }

    private static Task<AgendamentoExterno?> VigenteAsync(SmsMaisDbContext db, Guid origemId) =>
        db.AgendamentosExternos.AsNoTracking()
            .FirstOrDefaultAsync(a => a.OrigemId == origemId && a.Situacao == SituacaoAgendamentoExterno.Ativo);

    private static Task<List<ComunicacaoPaciente>> AvisosAsync(SmsMaisDbContext db, Guid origemId) =>
        db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.AgendamentoExterno!.OrigemId == origemId)
            .OrderBy(c => c.CriadoEm)
            .ToListAsync();

    /// <summary>Um agendamento externo vigente com o aviso já na fila (ou no estado pedido).</summary>
    private async Task<(AgendamentoExterno A, ComunicacaoPaciente C)> AvisoAsync(
        SmsMaisDbContext db, Guid pacienteId, StatusComunicacao status = StatusComunicacao.Pendente,
        bool liberado = false, string? local = "MS HOSPITAL DE EXEMPLO - HEX (RIO DE JANEIRO)")
    {
        var a = new AgendamentoExterno
        {
            Id = Guid.CreateVersion7(),
            Sistema = SistemaRegulacao.Ser,
            OrigemId = Guid.NewGuid(),
            NumeroExterno = Numero(),
            PacienteId = pacienteId,
            Tipo = TipoAgendamento.Consulta,
            Procedimento = "CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL",
            Data = Hoje.AddDays(9),
            Hora = new TimeOnly(8, 30),
            LocalNome = local,
            CriadoEm = DateTime.UtcNow,
        };
        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Consulta,
            Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
            AgendamentoExternoId = a.Id,
            PacienteId = pacienteId,
            Status = status,
            ProximaTentativaEm = status == StatusComunicacao.Pendente ? DateTime.UtcNow : null,
            IgnorarJanelaHorario = true,
            IgnorarVerificacaoTelefone = liberado,
            Telefone = status == StatusComunicacao.Pendente ? null : _telefone,
            CriadoEm = DateTime.UtcNow,
        };
        db.AgendamentosExternos.Add(a);
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();
        return (a, c);
    }

    private ManipuladorContexto Contexto(Guid pacienteId, string? texto = null, string? interativo = null)
    {
        var conversa = new Conversa
        {
            Id = Guid.NewGuid(),
            Canal = CanalConversa.WhatsApp,
            TelefoneCanonical = _telefone,
            PacienteId = pacienteId,
            Status = StatusConversa.Aberta,
            PrimeiroContatoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        var msg = new MensagemWhatsApp
        {
            Id = Guid.NewGuid(),
            Telefone = _telefone,
            Direcao = DirecaoMensagem.Entrada,
            Conteudo = texto,
            Status = StatusMensagemWhatsApp.Recebida,
            OcorridoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        return new ManipuladorContexto(conversa, msg, texto, pacienteId, null, interativo);
    }

    // ===================== leitura dos textos dos espelhos =====================

    [Fact]
    public void Le_dia_hora_e_local_como_cada_sistema_escreve()
    {
        var (d1, h1, r1) = LeituraAgendamentoExterno.DataHora("30/10/2026 09:15 - APADA - ASSOCIAÇÃO DE  PAIS");
        Assert.Equal(new DateOnly(2026, 10, 30), d1);
        Assert.Equal(new TimeOnly(9, 15), h1);
        Assert.Equal("APADA - ASSOCIAÇÃO DE PAIS", r1);

        var (d2, h2, r2) = LeituraAgendamentoExterno.DataHora("13/10/2026 13:25:00");
        Assert.Equal(new DateOnly(2026, 10, 13), d2);
        Assert.Equal(new TimeOnly(13, 25), h2);
        Assert.Null(r2);

        var (d3, h3, _) = LeituraAgendamentoExterno.DataHora("19/10/2026");
        Assert.Equal(new DateOnly(2026, 10, 19), d3);
        Assert.Null(h3);

        Assert.Null(LeituraAgendamentoExterno.DataHora("-").Data);
    }

    [Fact]
    public void Hora_do_ser_vem_do_evento_agendar_so_quando_e_do_mesmo_dia()
    {
        Assert.Equal(new TimeOnly(8, 30),
            LeituraAgendamentoExterno.HoraDoEventoAgendar("Data do agendamento: 19/10/2026 08:30 |", new DateOnly(2026, 10, 19)));
        Assert.Null(LeituraAgendamentoExterno.HoraDoEventoAgendar(
            "Data do agendamento: 12/10/2026 08:30 |", new DateOnly(2026, 10, 19)));
        Assert.Null(LeituraAgendamentoExterno.HoraDoEventoAgendar("Paciente confirmou presença", new DateOnly(2026, 10, 19)));
    }

    [Fact]
    public void Tira_o_cnes_que_o_esus_cola_no_nome()
    {
        Assert.Equal("ABRAE", LeituraAgendamentoExterno.LocalEsusSg("ABRAE  2297523"));
        Assert.Equal("OFTALMOCLÍNICA SÃO GONÇALO", LeituraAgendamentoExterno.LocalEsusSg("OFTALMOCLÍNICA SÃO GONÇALO - 2291525"));
        Assert.Equal("CLÍNICA 24 HORAS", LeituraAgendamentoExterno.LocalEsusSg("CLÍNICA 24 HORAS"));
    }

    // ===================== varredura dos espelhos =====================

    [Fact]
    public async Task Agendamento_do_ser_entra_uma_vez_na_fila_com_a_hora_do_historico()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var data = Hoje.AddDays(9);
        var ser = await SerAsync(db, paciente, data);

        await Varredura(db).SincronizarAsync();
        await Varredura(db).SincronizarAsync(); // idempotente

        var a = await VigenteAsync(db, ser.Id);
        Assert.NotNull(a);
        Assert.Equal(data, a!.Data);
        Assert.Equal(new TimeOnly(8, 30), a.Hora);
        Assert.Equal(ser.UnidadeExecutora, a.LocalNome);
        Assert.Equal("CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL", a.Procedimento);
        Assert.Equal(paciente, a.PacienteId);

        var avisos = await AvisosAsync(db, ser.Id);
        var aviso = Assert.Single(avisos);
        Assert.Equal(FinalidadeComunicacao.ConfirmacaoAgendamento, aviso.Finalidade);
        Assert.Equal(StatusComunicacao.Pendente, aviso.Status);
        Assert.Null(aviso.SolicitacaoId);
    }

    [Fact]
    public async Task Sernit_e_esus_entram_com_dia_hora_e_local_do_texto()
    {
        await using var db = fixture.CriarDbContext();
        var data = Hoje.AddDays(6);
        var sernit = new SernitSolicitacao
        {
            Id = Guid.NewGuid(), IdSernit = Numero(), Tipo = TipoRecursoSernit.Exame, Recurso = "AUDIOMETRIA",
            PacienteNome = "PACIENTE (EXEMPLO)", PacienteId = Guid.NewGuid(), Situacao = SituacaoSernit.Agendada,
            AgendadoParaTexto = $"{data:dd/MM/yyyy} 09:15 - APADA - ASSOCIAÇÃO DE EXEMPLO",
            SincronizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };
        var esus = new EsusSgSolicitacao
        {
            Id = Guid.NewGuid(), IdEsusSg = Numero(), Tipo = TipoRecursoEsusSg.Exame, Recurso = "TRATAMENTO DE RETINA",
            PacienteNome = "PACIENTE (EXEMPLO)", PacienteId = Guid.NewGuid(), Situacao = SituacaoEsusSg.Agendada,
            DataAgendada = data, DataHoraAgendadaTexto = $"{data:dd/MM/yyyy} 13:25:00",
            UnidadeExecutora = "OFTALMOCLÍNICA DE EXEMPLO - 2291525",
            SincronizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };
        db.SernitSolicitacoes.Add(sernit);
        db.EsusSgSolicitacoes.Add(esus);
        await db.SaveChangesAsync();

        await Varredura(db).SincronizarAsync();

        var a = await VigenteAsync(db, sernit.Id);
        Assert.Equal(new TimeOnly(9, 15), a!.Hora);
        Assert.Equal("APADA - ASSOCIAÇÃO DE EXEMPLO", a.LocalNome);
        Assert.Equal(TipoAgendamento.Exame, a.Tipo);
        var e = await VigenteAsync(db, esus.Id);
        Assert.Equal(new TimeOnly(13, 25), e!.Hora);
        Assert.Equal("OFTALMOCLÍNICA DE EXEMPLO", e.LocalNome);
        Assert.Single(await AvisosAsync(db, sernit.Id));
        Assert.Single(await AvisosAsync(db, esus.Id));
    }

    [Fact]
    public async Task Hoje_nao_avisa_e_chave_desligada_nao_poe_nada_na_fila()
    {
        await using var db = fixture.CriarDbContext();
        var deHoje = await SerAsync(db, Guid.NewGuid(), Hoje);
        var futuro = await SerAsync(db, Guid.NewGuid(), Hoje.AddDays(5));

        await Varredura(db, avisoExterno: false).SincronizarAsync();
        Assert.Null(await VigenteAsync(db, futuro.Id));

        await Varredura(db).SincronizarAsync();
        Assert.Null(await VigenteAsync(db, deHoje.Id));
        Assert.NotNull(await VigenteAsync(db, futuro.Id));
    }

    [Fact]
    public async Task Ser_sem_a_hora_espera_o_historico_e_depois_avisa_so_com_o_dia()
    {
        await using var db = fixture.CriarDbContext();
        var recente = await SerAsync(db, Guid.NewGuid(), Hoje.AddDays(4), horaNoEvento: null);
        var antigo = await SerAsync(db, Guid.NewGuid(), Hoje.AddDays(4), horaNoEvento: null,
            mudouEm: DateTime.UtcNow.AddDays(-2));

        await Varredura(db).SincronizarAsync();

        Assert.Null(await VigenteAsync(db, recente.Id));
        var a = await VigenteAsync(db, antigo.Id);
        Assert.NotNull(a);
        Assert.Null(a!.Hora);
    }

    [Fact]
    public async Task Remarcacao_encerra_o_aviso_velho_e_poe_um_novo()
    {
        await using var db = fixture.CriarDbContext();
        var ser = await SerAsync(db, Guid.NewGuid(), Hoje.AddDays(9));
        await Varredura(db).SincronizarAsync();
        var primeiro = await VigenteAsync(db, ser.Id);

        var novaData = Hoje.AddDays(20);
        var linha = await db.SerSolicitacoes.SingleAsync(x => x.Id == ser.Id);
        linha.AgendadoParaTexto = novaData.ToString("dd/MM/yyyy");
        db.SerEventos.Add(new SerEvento
        {
            Id = Guid.NewGuid(), SerSolicitacaoId = ser.Id, DataEvento = DateTime.UtcNow, Evento = "Agendar",
            TipoEvento = TipoEventoExterno.Agendar, Observacao = $"Data do agendamento: {novaData:dd/MM/yyyy} 10:00 |",
            CapturadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await Varredura(db).SincronizarAsync();

        var velho = await db.AgendamentosExternos.AsNoTracking().SingleAsync(a => a.Id == primeiro!.Id);
        Assert.Equal(SituacaoAgendamentoExterno.Reagendado, velho.Situacao);
        var novo = await VigenteAsync(db, ser.Id);
        Assert.Equal(novaData, novo!.Data);
        Assert.Equal(new TimeOnly(10, 0), novo.Hora);

        var avisos = await AvisosAsync(db, ser.Id);
        Assert.Equal(2, avisos.Count);
        Assert.Equal(StatusComunicacao.Dispensada, avisos.Single(c => c.AgendamentoExternoId == velho.Id).Status);
        Assert.Equal(StatusComunicacao.Pendente, avisos.Single(c => c.AgendamentoExternoId == novo.Id).Status);
    }

    [Fact]
    public async Task Saiu_de_agendada_o_aviso_que_nao_saiu_nao_sai_mais()
    {
        await using var db = fixture.CriarDbContext();
        var ser = await SerAsync(db, Guid.NewGuid(), Hoje.AddDays(9));
        await Varredura(db).SincronizarAsync();

        var linha = await db.SerSolicitacoes.SingleAsync(x => x.Id == ser.Id);
        linha.Situacao = SituacaoSer.Cancelada;
        await db.SaveChangesAsync();
        await Varredura(db).SincronizarAsync();

        Assert.Null(await VigenteAsync(db, ser.Id));
        var a = await db.AgendamentosExternos.AsNoTracking().SingleAsync(x => x.OrigemId == ser.Id);
        Assert.Equal(SituacaoAgendamentoExterno.Desmarcado, a.Situacao);
        Assert.Equal(StatusComunicacao.Dispensada, Assert.Single(await AvisosAsync(db, ser.Id)).Status);

        // Voltou a "Agendada" com o MESMO dia: a mesma linha volta a valer, e o aviso volta para a fila.
        linha.Situacao = SituacaoSer.Agendada;
        await db.SaveChangesAsync();
        await Varredura(db).SincronizarAsync();

        Assert.Equal(a.Id, (await VigenteAsync(db, ser.Id))!.Id);
        Assert.Equal(StatusComunicacao.Pendente, Assert.Single(await AvisosAsync(db, ser.Id)).Status);
    }

    // ===================== envio =====================

    [Fact]
    public async Task Primeira_mensagem_e_a_mesma_do_sisreg_sem_dado_nenhum()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (_, c) = await AvisoAsync(db, paciente);
        var whats = Whats();

        // Até para número confirmado: o "Quero mais informações" dele entrega os dados sem perguntar CPF.
        await Comunicacoes(db, whats, paciente, verificado: true).ProcessarTentativaEnvioAsync(c.Id);

        var opts = new ComunicacaoPacienteOptions();
        var primeira = Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync)).GetArguments();
        Assert.Equal(opts.TemplateConfirmacaoConsulta, primeira[1]);
        Assert.Equal(new[] { "Sra. Monica" }, (IReadOnlyList<string>)primeira[3]!);
        Assert.DoesNotContain(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarInterativoBotoesAsync));

        var depois = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral, depois.Status);
        Assert.True(await db.VerificacoesCadastraisEstado.AnyAsync(e => e.ComunicacaoPacienteId == c.Id));
    }

    [Fact]
    public async Task Depois_da_identificacao_vao_procedimento_data_e_local_sem_mandar_ao_posto()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var local = "MS HOSPITAL DE EXEMPLO - HEX (RIO DE JANEIRO)";
        var (a, c) = await AvisoAsync(db, paciente, liberado: true, local: local);
        var whats = Whats();

        await Comunicacoes(db, whats, paciente).ProcessarTentativaEnvioAsync(c.Id);

        var chamada = Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarInterativoBotoesAsync));
        var args = chamada.GetArguments();
        var texto = (string)args[1]!;
        Assert.Contains("Consulta: *CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL*", texto);
        Assert.Contains($"Data: *{a.Data:dd/MM/yyyy} às 08:30h*", texto);
        Assert.Contains($"Local: *{local}*", texto);
        Assert.DoesNotContain("Endereço", texto);
        Assert.Contains("regulação do *SER*", texto);
        Assert.DoesNotContain("posto", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guia", texto, StringComparison.OrdinalIgnoreCase);
        var botoes = (IReadOnlyList<BotaoInterativoWhatsApp>)args[2]!;
        Assert.Equal(new[] { $"confext_sim:{a.Id}", $"confext_nao:{a.Id}" }, botoes.Select(b => b.Id));

        var depois = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Enviada, depois.Status);
    }

    [Fact]
    public void Sem_hora_o_aviso_pede_para_confirmar_o_horario_com_o_local()
    {
        var a = new AgendamentoExterno
        {
            Sistema = SistemaRegulacao.EsusSg, Tipo = TipoAgendamento.Exame, Procedimento = "TRATAMENTO DE RETINA",
            Data = new DateOnly(2026, 10, 13), LocalNome = "OFTALMOCLÍNICA DE EXEMPLO",
        };
        var texto = ComunicacaoPacienteService.TextoDetalhesExterno(a, "JOANA (EXEMPLO)");
        Assert.Contains("Exame: *TRATAMENTO DE RETINA*", texto);
        Assert.Contains("Data: *13/10/2026*", texto);
        Assert.Contains("Local: *OFTALMOCLÍNICA DE EXEMPLO*", texto);
        Assert.DoesNotContain("Endereço", texto);
        Assert.Contains("O horário não veio do sistema de regulação", texto);
        Assert.Contains("regulação do *ESUS*", texto);
    }

    [Fact]
    public async Task Desmarcado_antes_de_sair_nao_envia()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (a, c) = await AvisoAsync(db, paciente);
        var linha = await db.AgendamentosExternos.SingleAsync(x => x.Id == a.Id);
        linha.Situacao = SituacaoAgendamentoExterno.Desmarcado;
        await db.SaveChangesAsync();
        var whats = Whats();

        await Comunicacoes(db, whats, paciente).ProcessarTentativaEnvioAsync(c.Id);

        Assert.DoesNotContain(whats.ReceivedCalls(), x => x.GetMethodInfo().Name.StartsWith("Enviar"));
        Assert.Equal(StatusComunicacao.Dispensada,
            (await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id)).Status);
    }

    [Fact]
    public async Task Chave_desligada_segura_na_fila_sem_gastar_tentativa()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (_, c) = await AvisoAsync(db, paciente);
        var whats = Whats();

        await Comunicacoes(db, whats, paciente, avisoExterno: false).ProcessarTentativaEnvioAsync(c.Id);

        var depois = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Pendente, depois.Status);
        Assert.Equal(0, depois.Tentativas);
        Assert.True(depois.ProximaTentativaEm > DateTime.UtcNow.AddMinutes(30));
        Assert.DoesNotContain(whats.ReceivedCalls(), x => x.GetMethodInfo().Name.StartsWith("Enviar"));
    }

    [Fact]
    public async Task Obito_na_ficha_nao_recebe_aviso()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (_, c) = await AvisoAsync(db, paciente);
        var whats = Whats();

        await Comunicacoes(db, whats, paciente, obito: new DateOnly(2026, 9, 1)).ProcessarTentativaEnvioAsync(c.Id);

        Assert.Equal(StatusComunicacao.Dispensada,
            (await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id)).Status);
        Assert.DoesNotContain(whats.ReceivedCalls(), x => x.GetMethodInfo().Name.StartsWith("Enviar"));
    }

    // ===================== identificação e resposta =====================

    [Fact]
    public async Task Identificacao_libera_o_aviso_externo_e_os_dados_saem_na_conversa()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (_, c) = await AvisoAsync(db, paciente, StatusComunicacao.AguardandoVerificacaoCadastral);

        var liberacao = SMSMais.Tests.Notificacoes.VerificacaoCadastralHandlerTests.Liberacao(db);
        var resultado = await liberacao.LiberarAsync(paciente, c.Id);
        await db.SaveChangesAsync();

        Assert.Equal(DesfechoLiberacao.Liberou, resultado.Desfecho);
        Assert.Equal(new[] { c.Id }, resultado.LiberadasIds);

        var whats = Whats();
        Assert.True(await Comunicacoes(db, whats, paciente).EnviarDetalhesNaConversaAsync(c.Id, _telefone));
        var depois = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Enviada, depois.Status);
        Assert.Null(depois.LoginLinkId);
    }

    [Fact]
    public async Task Identificacao_de_agendamento_desmarcado_diz_que_foi_cancelado()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (a, c) = await AvisoAsync(db, paciente, StatusComunicacao.AguardandoVerificacaoCadastral);
        (await db.AgendamentosExternos.SingleAsync(x => x.Id == a.Id)).Situacao = SituacaoAgendamentoExterno.Desmarcado;
        await db.SaveChangesAsync();

        var resultado = await SMSMais.Tests.Notificacoes.VerificacaoCadastralHandlerTests.Liberacao(db)
            .LiberarAsync(paciente, c.Id);

        Assert.Equal(DesfechoLiberacao.AgendamentoCancelado, resultado.Desfecho);
        Assert.Equal(0, resultado.Liberadas);
    }

    [Fact]
    public async Task Confirmar_grava_no_agendamento_e_nao_fala_de_guia()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (a, _) = await AvisoAsync(db, paciente, StatusComunicacao.Enviada);
        var whats = Whats();
        var handler = new ConfirmacaoAgendamentoWhatsAppHandler(db, whats, NullLogger<ConfirmacaoAgendamentoWhatsAppHandler>.Instance);

        var ctx = Contexto(paciente, interativo: $"confext_sim:{a.Id}");
        await handler.TratarAsync(ctx, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.True(ctx.Consumido);
        var depois = await db.AgendamentosExternos.AsNoTracking().SingleAsync(x => x.Id == a.Id);
        Assert.Equal(StatusConfirmacaoAgendamento.Confirmada, depois.StatusConfirmacao);
        Assert.Equal("whatsapp-quickreply", depois.ConfirmadoCanal);
        var resposta = (string)Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTextoAsync)).GetArguments()[1]!;
        Assert.Contains("*CONFIRMADA*", resposta);
        Assert.DoesNotContain("guia", resposta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Nao_poderei_ir_pergunta_cancela_e_guarda_o_motivo()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var (a, _) = await AvisoAsync(db, paciente, StatusComunicacao.Enviada);
        var whats = Whats();
        var handler = new ConfirmacaoAgendamentoWhatsAppHandler(db, whats, NullLogger<ConfirmacaoAgendamentoWhatsAppHandler>.Instance);

        await handler.TratarAsync(Contexto(paciente, interativo: $"confext_nao:{a.Id}"), CancellationToken.None);
        await db.SaveChangesAsync();
        var pergunta = Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarInterativoBotoesAsync)).GetArguments();
        Assert.Equal(new[] { $"cancext_sim:{a.Id}", $"cancext_nao:{a.Id}" },
            ((IReadOnlyList<BotaoInterativoWhatsApp>)pergunta[2]!).Select(b => b.Id));
        Assert.Equal(StatusConfirmacaoAgendamento.Pendente,
            (await db.AgendamentosExternos.AsNoTracking().SingleAsync(x => x.Id == a.Id)).StatusConfirmacao);

        await handler.TratarAsync(Contexto(paciente, interativo: $"cancext_sim:{a.Id}"), CancellationToken.None);
        await db.SaveChangesAsync();
        await handler.TratarAsync(Contexto(paciente, texto: "Não tenho como ir ao Rio nesse dia"), CancellationToken.None);
        await db.SaveChangesAsync();

        var depois = await db.AgendamentosExternos.AsNoTracking().SingleAsync(x => x.Id == a.Id);
        Assert.Equal(StatusConfirmacaoAgendamento.Cancelada, depois.StatusConfirmacao);
        Assert.Equal("Não tenho como ir ao Rio nesse dia", depois.MotivoCancelamentoPaciente);
        Assert.Equal(SituacaoAgendamentoExterno.Ativo, depois.Situacao); // nada é desmarcado no SER
    }

    // ===================== quadro "Contato com o paciente" =====================

    [Fact]
    public async Task Quadro_do_pedido_do_ser_mostra_o_aviso_e_a_resposta()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var local = $"MS HOSPITAL DO QUADRO {Guid.NewGuid():N}"[..34] + " (RIO DE JANEIRO)";
        var ser = await SerAsync(db, paciente, Hoje.AddDays(9), local: local);
        await Varredura(db).SincronizarAsync();
        var a = await db.AgendamentosExternos.SingleAsync(x => x.OrigemId == ser.Id);
        a.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
        a.ConfirmadoCanal = "whatsapp-quickreply";
        a.ConfirmadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(PacienteDtoFabrica.Criar(paciente, "PACIENTE (EXEMPLO)"));
        var acessor = new UsuarioAtualAccessorFake();
        var quadro = new SMSMais.Core.Regulacao.ContatoPaciente.RegulacaoContatoPacienteService(
            db, new SMSMais.Core.Regulacao.Comum.RegulacaoEscopo(db, acessor, Substitute.For<SMSMais.Core.Identidade.IIdentidadeService>()),
            new SMSMais.Core.Solicitacoes.SolicitacaoHistoricoService(db, acessor), pacientes, Regras());

        var dto = await quadro.ObterPorEspelhoAsync(SistemaRegulacao.Ser, ser.Id, CancellationToken.None);

        Assert.True(dto.DestinoComAvisoAutomatico);
        Assert.Equal("Confirmada", dto.Confirmacao);
        Assert.Equal("Pendente", Assert.Single(dto.Avisos).Status);
        Assert.Equal(local, dto.Agendamento!.Local);
    }
}
