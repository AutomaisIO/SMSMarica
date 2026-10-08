using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.PendenciasCadastro;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Régua de reforço da confirmação no banco: o ENFILEIRADOR (quem entra na fila, com os tetos por
/// número) e o ENVIO (o que sai, e o que a reconferência barra).
///
/// <para><b>O relógio do enfileirador é fictício</b>: uma segunda-feira num ano distante, sorteada
/// por teste. A régua varre a base inteira e a bancada guarda as linhas de execuções anteriores —
/// com o tempo de cada teste só dele, o que ficou para trás está fora da janela (data já passada
/// ou primeira mensagem "no futuro") e não interfere.</para>
///
/// <para>O envio usa o relógio real (é o do worker); ali a linha da régua é marcada para ignorar a
/// janela, que é o que também a libera do domingo — senão o teste dependeria do dia em que roda.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ReguaReforcoConfirmacaoTests(PostgresFixture fixture)
{
    private const string NomePaciente = "MARIA APARECIDA DA SILVA";

    // ===================== infraestrutura =====================

    private static ConfirmacaoConfiguracaoDto Regras(bool reforco = true, bool orientacao = true) => new(
        // Janela degenerada (início = fim) = sem restrição de horário: o teste não depende da hora.
        HoraInicioEnvio: "00:00", HoraFimEnvio: "00:00", MaximoPorPassagem: 100, SomenteSisreg: false,
        JanelaAbertaAgora: true, AtualizadoEm: null, LembreteDiasAntes: 2,
        ReforcoConfirmacaoHabilitado: reforco, OrientacaoPostoHabilitada: orientacao);

    private static IConfirmacaoConfiguracaoService RegrasSub(ConfirmacaoConfiguracaoDto? cfg = null)
    {
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>()).Returns(cfg ?? Regras());
        return regras;
    }

    /// <summary>Uma segunda-feira, 10h de Brasília, num ano distante e sorteado por teste.</summary>
    private static DateTime AgoraFicticio() =>
        new DateTime(2031, 1, 6, 13, 0, 0, DateTimeKind.Utc).AddDays(7 * Random.Shared.Next(0, 40_000));

    private static ComunicacaoPacienteService CriarComunicacao(
        SmsMaisDbContext db, IConfirmacaoConfiguracaoService regras,
        IPacientesService? pacientes = null, IWhatsAppCliente? whats = null) =>
        new(db,
            pacientes ?? Substitute.For<IPacientesService>(),
            Substitute.For<ICidadaoLoginLinkService>(),
            whats ?? Substitute.For<IWhatsAppCliente>(),
            Options.Create(new ComunicacaoPacienteOptions()),
            new UsuarioAtualAccessorFake(),
            Substitute.For<SMSMais.Core.Telefones.IDispensaContatoService>(),
            Substitute.For<IContatoComprometidoService>(),
            regras,
            NullLogger<ComunicacaoPacienteService>.Instance);

    /// <summary>
    /// O enfileirador com o hub FHIR de mentira: só os pacientes DESTE teste existem (nenhum com
    /// contato verificado). Os de execuções anteriores não são devolvidos — e paciente que o hub
    /// não devolve fica fora da passada, que é a regra de verdade.
    /// </summary>
    private static ReforcoConfirmacaoService CriarRegua(
        SmsMaisDbContext db, IEnumerable<Guid> pacientesDoTeste, ConfirmacaoConfiguracaoDto? cfg = null)
    {
        var regras = RegrasSub(cfg);
        var conhecidos = pacientesDoTeste.ToHashSet();
        var resolver = Substitute.For<IPacienteResolver>();
        resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci => (IReadOnlyDictionary<Guid, PacienteResumo>)ci.Arg<IEnumerable<Guid>>()
                .Where(conhecidos.Contains)
                .ToDictionary(id => id, id => new PacienteResumo(id, NomePaciente, null, null, null, Sexo.Feminino)));

        return new ReforcoConfirmacaoService(
            db, regras, CriarComunicacao(db, regras), resolver,
            Substitute.For<IContatoNegadoService>(),
            Options.Create(new ComunicacaoPacienteOptions()),
            NullLogger<ReforcoConfirmacaoService>.Instance);
    }

    /// <summary>Solicitação + a PRINCIPAL: primeira mensagem entregue e esperando identificação.</summary>
    private static async Task<(Solicitacao Solicitacao, ComunicacaoPaciente Principal)> SemearPrincipalAsync(
        SmsMaisDbContext db, Guid pacienteId, string telefone, DateTime dataAgendada, DateTime enviadoEm,
        DateTime? lidoEm = null)
    {
        var exame = await SeedSolicitacao.CriarAsync(db, pacienteId, dataAgendada: dataAgendada);
        var principal = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
            SolicitacaoId = exame.SolicitacaoId,
            PacienteId = pacienteId,
            Telefone = telefone,
            Status = StatusComunicacao.AguardandoVerificacaoCadastral,
            Tentativas = 1,
            EnviadoEm = enviadoEm,
            EntregueEm = enviadoEm.AddMinutes(1),
            LidoEm = lidoEm,
            MotivoFalha = "Primeira mensagem entregue; aguardando o paciente se identificar.",
            CriadoEm = enviadoEm,
        };
        db.ComunicacoesPaciente.Add(principal);
        await db.SaveChangesAsync();
        return (exame.Solicitacao!, principal);
    }

    private static async Task<ComunicacaoPaciente> SemearLinhaAsync(
        SmsMaisDbContext db, Solicitacao s, FinalidadeComunicacao finalidade, StatusComunicacao status,
        DateTime? enviadoEm = null, string? telefone = null, bool ignorarJanela = false)
    {
        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = finalidade,
            SolicitacaoId = s.Id,
            PacienteId = s.PacienteId,
            Telefone = telefone,
            Status = status,
            EnviadoEm = enviadoEm,
            ProximaTentativaEm = status == StatusComunicacao.Pendente ? DateTime.UtcNow : null,
            IgnorarJanelaHorario = ignorarJanela,
            CriadoEm = enviadoEm ?? DateTime.UtcNow,
        };
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    private static Task<List<FinalidadeComunicacao>> FinalidadesDaSolicitacaoAsync(SmsMaisDbContext db, Guid solicitacaoId) =>
        db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.SolicitacaoId == solicitacaoId)
            .Select(c => c.Finalidade)
            .ToListAsync();

    // ===================== (f) enfileirador =====================

    [Fact]
    public async Task Reforco_entra_na_fila_tres_dias_depois_para_quem_nao_respondeu()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var paciente = Guid.NewGuid();
        var (s, _) = await SemearPrincipalAsync(db, paciente, SeedSolicitacao.TelefoneAleatorio(),
            dataAgendada: agora.AddDays(10), enviadoEm: agora.AddHours(-80));

        await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        var reforco = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(c =>
            c.SolicitacaoId == s.Id && c.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao);
        Assert.Equal(StatusComunicacao.Pendente, reforco.Status);
        Assert.DoesNotContain(FinalidadeComunicacao.OrientacaoPosto, await FinalidadesDaSolicitacaoAsync(db2, s.Id));
    }

    [Fact]
    public async Task Reforco_nao_sai_quando_o_numero_escreveu_depois_da_primeira_mensagem()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var paciente = Guid.NewGuid();
        var telefone = SeedSolicitacao.TelefoneAleatorio();
        var (s, _) = await SemearPrincipalAsync(db, paciente, telefone,
            dataAgendada: agora.AddDays(10), enviadoEm: agora.AddHours(-80));

        // A resposta chega com o wa_id ANTIGO (sem o nono dígito) — é assim que o WhatsApp
        // identifica muito celular brasileiro. Tem de contar do mesmo jeito.
        db.MensagensWhatsApp.Add(new MensagemWhatsApp
        {
            Id = Guid.NewGuid(),
            Telefone = telefone[..4] + telefone[5..],
            Direcao = DirecaoMensagem.Entrada,
            Conteudo = "oi, que exame?",
            Status = StatusMensagemWhatsApp.Recebida,
            OcorridoEm = agora.AddHours(-20),
            CriadoEm = agora.AddHours(-20),
        });
        await db.SaveChangesAsync();

        await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        Assert.DoesNotContain(FinalidadeComunicacao.ReforcoConfirmacao, await FinalidadesDaSolicitacaoAsync(db2, s.Id));
    }

    [Fact]
    public async Task Reforco_nao_sai_quando_o_lembrete_ja_fez_o_toque_2()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var paciente = Guid.NewGuid();
        var telefone = SeedSolicitacao.TelefoneAleatorio();
        var (s, _) = await SemearPrincipalAsync(db, paciente, telefone,
            dataAgendada: agora.AddDays(10), enviadoEm: agora.AddHours(-120));
        await SemearLinhaAsync(db, s, FinalidadeComunicacao.LembreteAgendamento, StatusComunicacao.Enviada,
            enviadoEm: agora.AddHours(-60), telefone: telefone);

        await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        var finalidades = await FinalidadesDaSolicitacaoAsync(db2, s.Id);
        Assert.DoesNotContain(FinalidadeComunicacao.ReforcoConfirmacao, finalidades);
        // E a orientação ainda não: o lembrete tem só 60h e a data está longe.
        Assert.DoesNotContain(FinalidadeComunicacao.OrientacaoPosto, finalidades);
    }

    [Fact]
    public async Task Domingo_nada_entra_na_fila_e_na_segunda_entra()
    {
        await using var db = fixture.CriarDbContext();
        var segunda = AgoraFicticio();
        var domingo = segunda.AddDays(-1);
        var paciente = Guid.NewGuid();
        var (s, _) = await SemearPrincipalAsync(db, paciente, SeedSolicitacao.TelefoneAleatorio(),
            dataAgendada: segunda.AddDays(10), enviadoEm: domingo.AddHours(-80));

        Assert.Equal(0, await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(domingo, default));
        Assert.DoesNotContain(FinalidadeComunicacao.ReforcoConfirmacao, await FinalidadesDaSolicitacaoAsync(db, s.Id));

        await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(segunda, default);
        Assert.Contains(FinalidadeComunicacao.ReforcoConfirmacao, await FinalidadesDaSolicitacaoAsync(db, s.Id));
    }

    [Fact]
    public async Task Com_as_chaves_desligadas_nada_entra()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var paciente = Guid.NewGuid();
        var (s, _) = await SemearPrincipalAsync(db, paciente, SeedSolicitacao.TelefoneAleatorio(),
            dataAgendada: agora.AddDays(10), enviadoEm: agora.AddHours(-80));

        Assert.Equal(0, await CriarRegua(db, [paciente], Regras(reforco: false, orientacao: false))
            .EnfileirarDevidosAsync(agora, default));
        Assert.Single(await FinalidadesDaSolicitacaoAsync(db, s.Id));
    }

    [Fact]
    public async Task Orientacao_ao_posto_sai_perto_da_data_para_quem_nunca_respondeu()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var paciente = Guid.NewGuid();
        // Faltam 36h (menos que os 2 dias do lembrete) e a primeira mensagem já tem 4 dias.
        var (s, _) = await SemearPrincipalAsync(db, paciente, SeedSolicitacao.TelefoneAleatorio(),
            dataAgendada: agora.AddHours(36), enviadoEm: agora.AddHours(-96));

        await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        var finalidades = await FinalidadesDaSolicitacaoAsync(db2, s.Id);
        Assert.Contains(FinalidadeComunicacao.OrientacaoPosto, finalidades);
        // O reforço não é pré-requisito, e perto da data ele nem cabe mais.
        Assert.DoesNotContain(FinalidadeComunicacao.ReforcoConfirmacao, finalidades);
    }

    [Fact]
    public async Task Orientacao_ao_posto_nao_sai_para_quem_esgotou_as_chances()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var paciente = Guid.NewGuid();
        var telefone = SeedSolicitacao.TelefoneAleatorio();
        var (s, principal) = await SemearPrincipalAsync(db, paciente, telefone,
            dataAgendada: agora.AddHours(36), enviadoEm: agora.AddHours(-96));
        db.VerificacoesCadastraisEstado.Add(new VerificacaoCadastralEstado
        {
            Id = Guid.NewGuid(),
            TelefoneCanonical = telefone,
            ComunicacaoPacienteId = principal.Id,
            PacienteId = paciente,
            Etapa = EtapaVerificacaoCadastral.Esgotado,
            TentativasErradas = 3,
            ExpiraEm = agora.AddDays(3),
            CriadoEm = agora.AddHours(-90),
        });
        await db.SaveChangesAsync();

        await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        Assert.DoesNotContain(FinalidadeComunicacao.OrientacaoPosto, await FinalidadesDaSolicitacaoAsync(db2, s.Id));
    }

    [Fact]
    public async Task Orientacao_ao_posto_nao_sai_a_menos_de_24h_do_agendamento()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var paciente = Guid.NewGuid();
        var (s, _) = await SemearPrincipalAsync(db, paciente, SeedSolicitacao.TelefoneAleatorio(),
            dataAgendada: agora.AddHours(20), enviadoEm: agora.AddHours(-96));

        await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        Assert.Single(await FinalidadesDaSolicitacaoAsync(db2, s.Id)); // só a principal
    }

    [Fact]
    public async Task Numero_com_dois_pacientes_recebe_um_reforco_so_e_o_outro_fica_coberto()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var telefone = SeedSolicitacao.TelefoneAleatorio(); // a mãe, com dois filhos agendados
        var filho1 = Guid.NewGuid();
        var filho2 = Guid.NewGuid();
        var (s1, _) = await SemearPrincipalAsync(db, filho1, telefone,
            dataAgendada: agora.AddDays(8), enviadoEm: agora.AddHours(-80));
        var (s2, principal2) = await SemearPrincipalAsync(db, filho2, telefone,
            dataAgendada: agora.AddDays(12), enviadoEm: agora.AddHours(-80));

        await CriarRegua(db, [filho1, filho2]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        // O agendamento mais próximo leva o reforço; o outro só ganha o carimbo.
        Assert.Contains(FinalidadeComunicacao.ReforcoConfirmacao, await FinalidadesDaSolicitacaoAsync(db2, s1.Id));
        Assert.DoesNotContain(FinalidadeComunicacao.ReforcoConfirmacao, await FinalidadesDaSolicitacaoAsync(db2, s2.Id));
        var coberta = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(c => c.Id == principal2.Id);
        Assert.StartsWith("Reforço coberto", coberta.MotivoFalha);
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral, coberta.Status);

        // Na passada seguinte o teto por número segura: o segundo filho continua sem reforço.
        await CriarRegua(db2, [filho1, filho2]).EnfileirarDevidosAsync(agora.AddHours(1), default);
        await using var db3 = fixture.CriarDbContext();
        Assert.DoesNotContain(FinalidadeComunicacao.ReforcoConfirmacao, await FinalidadesDaSolicitacaoAsync(db3, s2.Id));
    }

    [Fact]
    public async Task Numero_que_recebeu_outro_automatico_ha_pouco_espera_o_silencio_minimo()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var telefone = SeedSolicitacao.TelefoneAleatorio();
        var paciente = Guid.NewGuid();
        var (s, _) = await SemearPrincipalAsync(db, paciente, telefone,
            dataAgendada: agora.AddDays(10), enviadoEm: agora.AddHours(-80));
        // Outro paciente do mesmo número recebeu a primeira mensagem ontem.
        await SemearPrincipalAsync(db, Guid.NewGuid(), telefone,
            dataAgendada: agora.AddDays(20), enviadoEm: agora.AddHours(-24));

        await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        Assert.DoesNotContain(FinalidadeComunicacao.ReforcoConfirmacao, await FinalidadesDaSolicitacaoAsync(db2, s.Id));
    }

    /// <summary>
    /// O telefone do cadastro mudou, o reforço foi dispensado e a primeira mensagem voltou a sair
    /// para o número novo: a régua RECOMEÇA dela — a linha do ciclo anterior é rearmada (o índice
    /// único não deixa criar outra). Já a dispensada DEPOIS da primeira mensagem atual (a pessoa
    /// respondeu) continua valendo e segura o reforço.
    /// </summary>
    [Fact]
    public async Task Reforco_dispensado_num_ciclo_anterior_volta_quando_a_primeira_mensagem_sai_de_novo()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var pacienteNovoCiclo = Guid.NewGuid();
        var pacienteRespondeu = Guid.NewGuid();
        var (sNovo, _) = await SemearPrincipalAsync(db, pacienteNovoCiclo, SeedSolicitacao.TelefoneAleatorio(),
            dataAgendada: agora.AddDays(10), enviadoEm: agora.AddHours(-80));
        var (sRespondeu, _) = await SemearPrincipalAsync(db, pacienteRespondeu, SeedSolicitacao.TelefoneAleatorio(),
            dataAgendada: agora.AddDays(10), enviadoEm: agora.AddHours(-80));

        var antiga = await SemearLinhaAsync(db, sNovo, FinalidadeComunicacao.ReforcoConfirmacao, StatusComunicacao.Dispensada);
        var recente = await SemearLinhaAsync(db, sRespondeu, FinalidadeComunicacao.ReforcoConfirmacao, StatusComunicacao.Dispensada);
        await db.ComunicacoesPaciente.Where(c => c.Id == antiga.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.AtualizadoEm, agora.AddHours(-100)));
        await db.ComunicacoesPaciente.Where(c => c.Id == recente.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.AtualizadoEm, agora.AddHours(-10)));

        // Contexto novo: o de cima ainda rastreia as linhas com a versão (xmin) de antes do
        // ExecuteUpdate, e o rearme esbarraria na concorrência otimista.
        await using var dbRegua = fixture.CriarDbContext();
        await CriarRegua(dbRegua, [pacienteNovoCiclo, pacienteRespondeu]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        var rearmada = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(c =>
            c.SolicitacaoId == sNovo.Id && c.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao);
        Assert.Equal(antiga.Id, rearmada.Id);
        Assert.Equal(StatusComunicacao.Pendente, rearmada.Status);
        var mantida = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(c => c.Id == recente.Id);
        Assert.Equal(StatusComunicacao.Dispensada, mantida.Status);
    }

    // ===================== (a)(b) envio =====================

    private sealed record CenarioEnvio(
        ComunicacaoPacienteService Servico, IWhatsAppCliente Whats, Solicitacao Solicitacao,
        ComunicacaoPaciente Principal, ComunicacaoPaciente Linha, string Telefone, string Unidade);

    /// <summary>Uma linha da régua na fila, de uma mamografia, para a Sra. Maria.</summary>
    private async Task<CenarioEnvio> PrepararEnvioAsync(
        SmsMaisDbContext db, FinalidadeComunicacao finalidade, bool principalLida = true,
        IReadOnlyList<TemplateWhatsApp>? catalogo = null, string? telefoneVerificado = null,
        string? telefoneDoCadastro = null, TipoVaga? vaga = null)
    {
        var agora = DateTime.UtcNow;
        var pacienteId = Guid.NewGuid();
        var telefone = SeedSolicitacao.TelefoneAleatorio();
        var (s, principal) = await SemearPrincipalAsync(db, pacienteId, telefone,
            dataAgendada: agora.AddDays(10), enviadoEm: agora.AddHours(-80),
            lidoEm: principalLida ? agora.AddHours(-79) : null);
        if (vaga is not null)
        {
            var sol = await db.Solicitacoes.SingleAsync(x => x.Id == s.Id);
            sol.TipoVaga = vaga;
            await db.SaveChangesAsync();
            s.TipoVaga = vaga;
        }

        // O exame é uma MAMOGRAFIA: o nome do procedimento não pode aparecer em lugar nenhum.
        var exame = await db.ExamesImagem.Include(e => e.TipoExame).SingleAsync(e => e.SolicitacaoId == s.Id);
        exame.TipoExame!.Nome = $"MAMOGRAFIA BILATERAL {Guid.NewGuid():N}"[..28];
        await db.SaveChangesAsync();
        var unidade = await db.Unidades.AsNoTracking()
            .Where(u => u.Id == s.UnidadeExecutanteId).Select(u => u.Nome).SingleAsync();

        var linha = await SemearLinhaAsync(db, s, finalidade, StatusComunicacao.Pendente, ignorarJanela: true);

        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(pacienteId, Arg.Any<CancellationToken>())
            .Returns(PacienteDtoFabrica.Criar(pacienteId, NomePaciente, sexo: Sexo.Feminino) with
            {
                TelefoneCelular = telefoneDoCadastro ?? telefone,
                TelefoneVerificado = telefoneVerificado,
            });

        var whats = Substitute.For<IWhatsAppCliente>();
        whats.ListarTemplatesAsync(Arg.Any<CancellationToken>()).Returns(catalogo ?? []);
        whats.EnviarTemplateAsync(default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.REGUA.{Guid.NewGuid():N}", null));

        return new CenarioEnvio(
            CriarComunicacao(db, RegrasSub(), pacientes, whats), whats, s, principal, linha, telefone, unidade);
    }

    private static (string Modelo, IReadOnlyList<string> Parametros, string? Conteudo) EnvioFeito(IWhatsAppCliente whats)
    {
        var chamada = Assert.Single(whats.ReceivedCalls(),
            c => c.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync));
        var a = chamada.GetArguments();
        return ((string)a[1]!, (IReadOnlyList<string>)a[3]!, (string?)a[5]);
    }

    [Theory]
    [InlineData(FinalidadeComunicacao.ReforcoConfirmacao, "agendamento_aguardando_resposta", "sobre seu exame")]
    [InlineData(FinalidadeComunicacao.OrientacaoPosto, "agendamento_procure_posto", "do seu exame")]
    public async Task Envio_leva_so_o_tratamento_e_a_constante_nunca_o_exame_nem_a_unidade(
        FinalidadeComunicacao finalidade, string modelo, string complemento)
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararEnvioAsync(db, finalidade);

        await c.Servico.ProcessarTentativaEnvioAsync(c.Linha.Id);

        var (modeloUsado, parametros, conteudo) = EnvioFeito(c.Whats);
        Assert.Equal(modelo, modeloUsado);
        Assert.Equal(new[] { "Sra. Maria", complemento }, parametros);
        Assert.NotNull(conteudo);
        Assert.DoesNotContain("MAMOGRAFIA", conteudo!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(c.Unidade, conteudo!, StringComparison.OrdinalIgnoreCase);

        await using var db2 = fixture.CriarDbContext();
        var linha = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Linha.Id);
        Assert.Equal(StatusComunicacao.Enviada, linha.Status);
        Assert.NotNull(linha.EnviadoEm);

        // A principal conta o que aconteceu e continua esperando a identificação.
        var principal = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Principal.Id);
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral, principal.Status);
        Assert.StartsWith(finalidade == FinalidadeComunicacao.OrientacaoPosto ? "Orientado a procurar o posto" : "Reforço enviado",
            principal.MotivoFalha);

        // A porta da identificação fica aberta, apontando para a PRINCIPAL.
        var estado = await db2.VerificacoesCadastraisEstado.AsNoTracking().SingleAsync(e => e.TelefoneCanonical == c.Telefone);
        Assert.Equal(c.Principal.Id, estado.ComunicacaoPacienteId);
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoInteresse, estado.Etapa);
    }

    [Fact]
    public async Task Reforco_para_quem_nao_leu_usa_o_A_quando_aprovado()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararEnvioAsync(db, FinalidadeComunicacao.ReforcoConfirmacao, principalLida: false,
            catalogo:
            [
                new TemplateWhatsApp("agendamento_aguardando_resposta", "pt_BR", "UTILITY", null, 2, []),
                new TemplateWhatsApp("agendamento_aviso_pendente", "pt_BR", "UTILITY", null, 2, []),
            ]);

        await c.Servico.ProcessarTentativaEnvioAsync(c.Linha.Id);

        var (modelo, parametros, _) = EnvioFeito(c.Whats);
        Assert.Equal("agendamento_aviso_pendente", modelo);
        Assert.Equal(new[] { "Sra. Maria", "do seu exame" }, parametros);
    }

    // ===== Retorno (08/10/2026): quem volta já tem a guia — nada da régua fala dela =====

    private static readonly TemplateWhatsApp ModeloSemGuia = new("agendamento_aviso_pendente", "pt_BR", "UTILITY", null, 2, []);
    private static readonly TemplateWhatsApp ModeloComGuia = new("agendamento_aguardando_resposta", "pt_BR", "UTILITY", null, 2, []);

    [Fact]
    public async Task Retorno_recebe_o_reforco_sem_a_guia_mesmo_tendo_lido()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararEnvioAsync(db, FinalidadeComunicacao.ReforcoConfirmacao, principalLida: true,
            catalogo: [ModeloComGuia, ModeloSemGuia], vaga: TipoVaga.Retorno);

        await c.Servico.ProcessarTentativaEnvioAsync(c.Linha.Id);

        var (modelo, _, conteudo) = EnvioFeito(c.Whats);
        Assert.Equal("agendamento_aviso_pendente", modelo);
        Assert.DoesNotContain("guia", conteudo!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Retorno_sem_o_modelo_sem_guia_aprovado_nao_recebe_reforco()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararEnvioAsync(db, FinalidadeComunicacao.ReforcoConfirmacao,
            catalogo: [ModeloComGuia], vaga: TipoVaga.Retorno);

        await c.Servico.ProcessarTentativaEnvioAsync(c.Linha.Id);

        Assert.DoesNotContain(c.Whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync));
        await using var db2 = fixture.CriarDbContext();
        var linha = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Linha.Id);
        Assert.Equal(StatusComunicacao.Dispensada, linha.Status);
    }

    [Fact]
    public async Task Retorno_nao_recebe_a_orientacao_ao_posto()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararEnvioAsync(db, FinalidadeComunicacao.OrientacaoPosto,
            catalogo: [ModeloComGuia, ModeloSemGuia], vaga: TipoVaga.Retorno);

        await c.Servico.ProcessarTentativaEnvioAsync(c.Linha.Id);

        Assert.DoesNotContain(c.Whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync));
        await using var db2 = fixture.CriarDbContext();
        Assert.Equal(StatusComunicacao.Dispensada,
            (await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Linha.Id)).Status);
    }

    [Fact]
    public async Task Retorno_perto_da_data_nao_entra_na_fila_da_orientacao()
    {
        await using var db = fixture.CriarDbContext();
        var agora = AgoraFicticio();
        var paciente = Guid.NewGuid();
        // O mesmo cenário em que a primeira vez recebe a orientação (faltam 36h, mensagem de 4 dias).
        var (s, _) = await SemearPrincipalAsync(db, paciente, SeedSolicitacao.TelefoneAleatorio(),
            dataAgendada: agora.AddHours(36), enviadoEm: agora.AddHours(-96));
        var sol = await db.Solicitacoes.SingleAsync(x => x.Id == s.Id);
        sol.TipoVaga = TipoVaga.Retorno;
        await db.SaveChangesAsync();

        await CriarRegua(db, [paciente]).EnfileirarDevidosAsync(agora, default);

        await using var db2 = fixture.CriarDbContext();
        Assert.DoesNotContain(FinalidadeComunicacao.OrientacaoPosto, await FinalidadesDaSolicitacaoAsync(db2, s.Id));
    }

    [Fact]
    public async Task Paciente_que_verificou_por_outro_caminho_recebe_a_confirmacao_e_nao_o_reforco()
    {
        await using var db = fixture.CriarDbContext();
        var verificado = SeedSolicitacao.TelefoneAleatorio();
        var c = await PrepararEnvioAsync(db, FinalidadeComunicacao.ReforcoConfirmacao,
            telefoneVerificado: verificado);

        await c.Servico.ProcessarTentativaEnvioAsync(c.Linha.Id);

        Assert.DoesNotContain(c.Whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync));
        await using var db2 = fixture.CriarDbContext();
        var linha = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Linha.Id);
        Assert.Equal(StatusComunicacao.Dispensada, linha.Status);
        var principal = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Principal.Id);
        Assert.Equal(StatusComunicacao.Pendente, principal.Status);
        Assert.True(principal.IgnorarVerificacaoTelefone);
        Assert.Equal(0, principal.Tentativas);
        Assert.NotNull(principal.ProximaTentativaEm);
    }

    [Fact]
    public async Task Telefone_do_cadastro_mudou_a_primeira_mensagem_volta_para_o_numero_novo()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararEnvioAsync(db, FinalidadeComunicacao.ReforcoConfirmacao,
            telefoneDoCadastro: SeedSolicitacao.TelefoneAleatorio());

        await c.Servico.ProcessarTentativaEnvioAsync(c.Linha.Id);

        Assert.DoesNotContain(c.Whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync));
        await using var db2 = fixture.CriarDbContext();
        var linha = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Linha.Id);
        Assert.Equal(StatusComunicacao.Dispensada, linha.Status);
        Assert.Contains("telefone do cadastro mudou", linha.MotivoFalha);
        var principal = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Principal.Id);
        Assert.Equal(StatusComunicacao.Pendente, principal.Status);
        Assert.Null(principal.Telefone); // re-resolvido no envio: sai para o número novo
        Assert.Null(principal.EnviadoEm);
    }

    [Fact]
    public async Task Reforco_na_fila_de_quem_respondeu_depois_e_dispensado_no_envio()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararEnvioAsync(db, FinalidadeComunicacao.ReforcoConfirmacao);
        db.MensagensWhatsApp.Add(new MensagemWhatsApp
        {
            Id = Guid.NewGuid(),
            Telefone = c.Telefone,
            Direcao = DirecaoMensagem.Entrada,
            Conteudo = "Quero mais informações",
            Status = StatusMensagemWhatsApp.Recebida,
            OcorridoEm = DateTime.UtcNow.AddMinutes(-5),
            CriadoEm = DateTime.UtcNow.AddMinutes(-5),
        });
        await db.SaveChangesAsync();

        await c.Servico.ProcessarTentativaEnvioAsync(c.Linha.Id);

        Assert.DoesNotContain(c.Whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync));
        await using var db2 = fixture.CriarDbContext();
        var linha = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Linha.Id);
        Assert.Equal(StatusComunicacao.Dispensada, linha.Status);
        Assert.StartsWith(ReguaReforcoConfirmacao.PrefixoSuperadoReforco, linha.MotivoFalha);
    }
}
