using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Core.Notificacoes.VerificacaoCadastral;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Telefones;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// O lado do CIDADÃO da régua de reforço (25/09/2026): o que acontece quando a pessoa toca um
/// botão do lembrete, do reforço ou da orientação ao posto, e o que ela recebe depois de se
/// identificar.
///
/// <para>O que está sob teste é o que falhou em produção: o toque no reforço de um número com
/// dois pacientes abrindo o desafio do paciente errado; a janela de 20 dias contada da primeira
/// mensagem, deixando sem resposta quem responde à orientação de um agendamento antigo; o
/// "Quero mais informações" pedindo CPF a quem estava parado no nascimento; e o "Já estou
/// enviando" dito com zero mensagens liberadas (878 casos entre 20 e 25/09).</para>
///
/// <para>O handler não chama SaveChanges (contrato do webhook): os testes commitam depois.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RespostaAoDesafioTests(PostgresFixture fixture)
{
    // Telefone PRÓPRIO por teste: a bancada guarda as linhas entre execuções e o estado da
    // verificação é único por telefone.
    private readonly string _telefone = SeedSolicitacao.TelefoneAleatorio();

    // ===================== infraestrutura =====================

    private sealed record Pessoa(Guid Id, string Nome, string Cpf);

    private static Pessoa NovaPessoa(string nome, string cpf) => new(Guid.NewGuid(), nome, cpf);

    private static PacienteDto Dto(Pessoa p, string? celular = null) =>
        PacienteDtoFabrica.Criar(p.Id, p.Nome, cpf: p.Cpf, nascimento: new DateOnly(1980, 3, 15), sexo: Sexo.Feminino)
            with { TelefoneCelular = celular };

    private sealed record Cenario(VerificacaoCadastralWhatsAppHandler Handler, IWhatsAppCliente Whats, Conversa Conversa);

    private async Task<Cenario> CriarAsync(SmsMaisDbContext db, params Pessoa[] pessoas)
    {
        // A conversa existe no banco: a pendência de número errado tem FK para ela.
        var conversa = new Conversa
        {
            Id = Guid.NewGuid(),
            Canal = CanalConversa.WhatsApp,
            TelefoneCanonical = _telefone,
            Status = StatusConversa.Aberta,
            PrimeiroContatoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.Conversas.Add(conversa);
        await db.SaveChangesAsync();

        var pacientes = Substitute.For<IPacientesService>();
        foreach (var p in pessoas)
            pacientes.ObterPorIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(Dto(p));

        var whats = Substitute.For<IWhatsAppCliente>();
        whats.EnviarTextoAsync(null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.OUT.{Guid.NewGuid():N}", null));
        whats.EnviarInterativoBotoesAsync(null!, null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.OUT.{Guid.NewGuid():N}", null));

        var pendencias = new SMSMais.Core.PendenciasCadastro.PendenciaCadastroService(
            db, new UsuarioAtualAccessorFake(), pacientes,
            Substitute.For<SMSMais.Core.Pacientes.Fhir.IPacienteFhirClient>(),
            NullLogger<SMSMais.Core.PendenciasCadastro.PendenciaCadastroService>.Instance);
        var handler = new VerificacaoCadastralWhatsAppHandler(
            db, whats, pacientes, Substitute.For<ITelefoneValidacaoService>(), pendencias,
            new Lazy<SMSMais.Core.Notificacoes.Comunicacao.IComunicacaoPacienteService>(
                () => Substitute.For<SMSMais.Core.Notificacoes.Comunicacao.IComunicacaoPacienteService>()),
            VerificacaoCadastralHandlerTests.Liberacao(db),
            NullLogger<VerificacaoCadastralWhatsAppHandler>.Instance);
        return new Cenario(handler, whats, conversa);
    }

    /// <summary>Solicitação + uma comunicação retida esperando a identificação (a principal, ou o
    /// lembrete quando <paramref name="finalidade"/> pede).</summary>
    private async Task<(Solicitacao S, ComunicacaoPaciente Retida)> RetidaAsync(
        SmsMaisDbContext db, Pessoa p, DateTime? dataAgendada = null, DateTime? criadoEm = null,
        FinalidadeComunicacao finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento)
    {
        var exame = await SeedSolicitacao.CriarAsync(db, p.Id, dataAgendada: dataAgendada ?? DateTime.UtcNow.AddDays(10));
        var quando = criadoEm ?? DateTime.UtcNow.AddHours(-1);
        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = finalidade,
            SolicitacaoId = exame.SolicitacaoId,
            PacienteId = p.Id,
            Telefone = _telefone,
            Status = StatusComunicacao.AguardandoVerificacaoCadastral,
            Tentativas = 1,
            EnviadoEm = quando,
            EntregueEm = quando.AddMinutes(1),
            MotivoFalha = "Primeira mensagem entregue; aguardando o paciente se identificar.",
            CriadoEm = quando,
        };
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();
        return (exame.Solicitacao!, c);
    }

    /// <summary>Outra linha da mesma solicitação; com <paramref name="enviada"/>, leva a mensagem
    /// enviada (é pelo wamid dela que a resposta do cidadão chega como contexto).</summary>
    private async Task<(ComunicacaoPaciente Linha, string? Wamid)> LinhaAsync(
        SmsMaisDbContext db, Solicitacao s, FinalidadeComunicacao finalidade, StatusComunicacao status,
        DateTime? enviadoEm = null)
    {
        string? wamid = null;
        Guid? mensagemId = null;
        if (enviadoEm is { } quando)
        {
            wamid = $"wamid.TOQUE.{Guid.NewGuid():N}";
            var msg = new MensagemWhatsApp
            {
                Id = Guid.NewGuid(),
                PacienteId = s.PacienteId,
                Telefone = _telefone,
                Direcao = DirecaoMensagem.Saida,
                Conteudo = "toque da régua",
                Status = StatusMensagemWhatsApp.Entregue,
                WaMessageId = wamid,
                OcorridoEm = quando,
                CriadoEm = quando,
            };
            db.MensagensWhatsApp.Add(msg);
            mensagemId = msg.Id;
        }
        var linha = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = finalidade,
            SolicitacaoId = s.Id,
            PacienteId = s.PacienteId,
            Telefone = _telefone,
            Status = status,
            EnviadoEm = enviadoEm,
            MensagemWhatsAppId = mensagemId,
            CriadoEm = enviadoEm ?? DateTime.UtcNow,
        };
        db.ComunicacoesPaciente.Add(linha);
        await db.SaveChangesAsync();
        return (linha, wamid);
    }

    private async Task<VerificacaoCadastralEstado> EstadoAsync(
        SmsMaisDbContext db, ComunicacaoPaciente alvo, EtapaVerificacaoCadastral etapa,
        DateTime? atualizadoEm = null, Guid? pacienteId = null, int tentativasErradas = 0, string? cpfDigitos = null)
    {
        var estado = new VerificacaoCadastralEstado
        {
            Id = Guid.NewGuid(),
            TelefoneCanonical = _telefone,
            ComunicacaoPacienteId = alvo.Id,
            PacienteId = pacienteId,
            Etapa = etapa,
            CpfDigitosInformados = cpfDigitos,
            TentativasErradas = tentativasErradas,
            ExpiraEm = DateTime.UtcNow.AddDays(5),
            CriadoEm = atualizadoEm ?? DateTime.UtcNow,
            AtualizadoEm = atualizadoEm,
        };
        db.VerificacoesCadastraisEstado.Add(estado);
        await db.SaveChangesAsync();
        return estado;
    }

    private ManipuladorContexto Contexto(
        Conversa conversa, string texto, string? respondendo = null, string? botao = null, string? interativo = null) =>
        new(conversa,
            new MensagemWhatsApp
            {
                Id = Guid.NewGuid(),
                Telefone = _telefone,
                Direcao = DirecaoMensagem.Entrada,
                Conteudo = texto,
                Status = StatusMensagemWhatsApp.Recebida,
                ContextoWaMessageId = respondendo,
                OcorridoEm = DateTime.UtcNow,
                CriadoEm = DateTime.UtcNow,
            },
            texto, null, botao, interativo);

    /// <summary>Tudo o que o handler respondeu (texto simples ou com botões), na ordem.</summary>
    private static List<string> Respostas(IWhatsAppCliente whats) =>
        [.. whats.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name is nameof(IWhatsAppCliente.EnviarTextoAsync)
                or nameof(IWhatsAppCliente.EnviarInterativoBotoesAsync))
            .Select(c => (string)c.GetArguments()[1]!)];

    private Task<VerificacaoCadastralEstado?> LerEstadoAsync(SmsMaisDbContext db) =>
        db.VerificacoesCadastraisEstado.AsNoTracking().FirstOrDefaultAsync(e => e.TelefoneCanonical == _telefone);

    private static Task<ComunicacaoPaciente> LerAsync(SmsMaisDbContext db, Guid id) =>
        db.ComunicacoesPaciente.AsNoTracking().SingleAsync(c => c.Id == id);

    // ===================== (d) o toque no reforço acha a principal certa =====================

    [Fact]
    public async Task Quero_mais_informacoes_no_reforco_abre_o_desafio_do_paciente_do_reforco_num_numero_com_dois()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var carla = NovaPessoa("CARLA PEREIRA", "55566677788");
        var c = await CriarAsync(db, joana, carla);

        // Mesmo número, dois pacientes. O estado aponta para a primeira mensagem MAIS NOVA (Carla);
        // a pessoa responde ao reforço da Joana.
        var (sJoana, principalJoana) = await RetidaAsync(db, joana, criadoEm: DateTime.UtcNow.AddDays(-5));
        var (_, principalCarla) = await RetidaAsync(db, carla, criadoEm: DateTime.UtcNow.AddDays(-1));
        await EstadoAsync(db, principalCarla, EtapaVerificacaoCadastral.AguardandoInteresse);
        var (_, wamidReforco) = await LinhaAsync(db, sJoana, FinalidadeComunicacao.ReforcoConfirmacao,
            StatusComunicacao.Entregue, enviadoEm: DateTime.UtcNow.AddHours(-2));

        var ctx = Contexto(c.Conversa, "Quero mais informações", respondendo: wamidReforco, botao: "Quero mais informações");
        await c.Handler.TratarAsync(ctx, default);
        await db.SaveChangesAsync();

        Assert.True(ctx.Consumido);
        var estado = (await LerEstadoAsync(db))!;
        Assert.Equal(principalJoana.Id, estado.ComunicacaoPacienteId);
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoCpf, estado.Etapa);
        var resposta = Assert.Single(Respostas(c.Whats));
        Assert.Contains("Joana", resposta);
        Assert.DoesNotContain("Carla", resposta);
    }

    [Fact]
    public async Task Resposta_ao_reforco_de_agendamento_que_nao_espera_mais_nada_nao_cai_no_outro_paciente()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var carla = NovaPessoa("CARLA PEREIRA", "55566677788");
        var c = await CriarAsync(db, joana, carla);

        // A primeira mensagem da Joana já saiu do "aguardando" (foi liberada por outro caminho). A
        // da Carla ainda espera, e o estado do número aponta para ela.
        var (sJoana, principalJoana) = await RetidaAsync(db, joana, criadoEm: DateTime.UtcNow.AddDays(-5));
        principalJoana.Status = StatusComunicacao.Enviada;
        await db.SaveChangesAsync();
        var (_, principalCarla) = await RetidaAsync(db, carla);
        await EstadoAsync(db, principalCarla, EtapaVerificacaoCadastral.AguardandoInteresse);
        var (_, wamidReforco) = await LinhaAsync(db, sJoana, FinalidadeComunicacao.ReforcoConfirmacao,
            StatusComunicacao.Entregue, enviadoEm: DateTime.UtcNow.AddHours(-2));

        // "Não sou essa pessoa" no reforço da JOANA não pode virar "Você conhece Carla?".
        var ctx = Contexto(c.Conversa, "Não sou essa pessoa", respondendo: wamidReforco, botao: "Não sou essa pessoa");
        await c.Handler.TratarAsync(ctx, default);
        await db.SaveChangesAsync();

        Assert.False(ctx.Consumido);
        Assert.Empty(Respostas(c.Whats));
        Assert.Equal(principalCarla.Id, (await LerEstadoAsync(db))!.ComunicacaoPacienteId);
    }

    // ===================== (e) janela de 20 dias pelo último toque =====================

    [Fact]
    public async Task Janela_de_20_dias_conta_do_ultimo_toque_e_nao_da_primeira_mensagem()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var c = await CriarAsync(db, joana);

        // Primeira mensagem de um mês atrás; a orientação ao posto saiu ontem.
        var (s, principal) = await RetidaAsync(db, joana, criadoEm: DateTime.UtcNow.AddDays(-30));
        await LinhaAsync(db, s, FinalidadeComunicacao.OrientacaoPosto, StatusComunicacao.Entregue,
            enviadoEm: DateTime.UtcNow.AddDays(-1));

        // Sem estado: os dígitos direto recriam o diálogo pelo desafio pendente do número.
        var ctx = Contexto(c.Conversa, "1112");
        await c.Handler.TratarAsync(ctx, default);
        await db.SaveChangesAsync();

        Assert.True(ctx.Consumido);
        var estado = (await LerEstadoAsync(db))!;
        Assert.Equal(principal.Id, estado.ComunicacaoPacienteId);
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNascimento, estado.Etapa);
    }

    [Fact]
    public async Task Sem_toque_recente_a_primeira_mensagem_de_um_mes_atras_nao_conta()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var c = await CriarAsync(db, joana);
        await RetidaAsync(db, joana, criadoEm: DateTime.UtcNow.AddDays(-30));

        var ctx = Contexto(c.Conversa, "1112");
        await c.Handler.TratarAsync(ctx, default);

        Assert.False(ctx.Consumido);
        Assert.Null(await LerEstadoAsync(db));
    }

    [Fact]
    public async Task Desafio_pendente_e_achado_tambem_pelo_numero_sem_o_nono_digito()
    {
        // O robô pergunta "há desafio pendente neste número?" com o telefone da CONVERSA, que é o
        // wa_id da Meta — sem o nono dígito para muito celular antigo. A comunicação guarda o
        // formato atual.
        await using var db = fixture.CriarDbContext();
        var telefone = $"552199{Random.Shared.Next(1_000_000, 9_999_999)}";
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var exame = await SeedSolicitacao.CriarAsync(db, joana.Id, dataAgendada: DateTime.UtcNow.AddDays(10));
        var principal = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
            SolicitacaoId = exame.SolicitacaoId,
            PacienteId = joana.Id,
            Telefone = telefone,
            Status = StatusComunicacao.AguardandoVerificacaoCadastral,
            CriadoEm = DateTime.UtcNow,
        };
        db.ComunicacoesPaciente.Add(principal);
        await db.SaveChangesAsync();

        var semNono = telefone[..4] + telefone[5..];
        var achados = await DesafiosCadastraisPendentes.DoTelefoneAsync(db, semNono, default);

        Assert.Contains(achados, a => a.Id == principal.Id);
    }

    // ===================== "Vou ao posto" =====================

    [Fact]
    public async Task Vou_ao_posto_na_orientacao_carimba_a_principal_e_encerra_o_dialogo()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var c = await CriarAsync(db, joana);
        var (s, principal) = await RetidaAsync(db, joana, criadoEm: DateTime.UtcNow.AddDays(-7));
        await EstadoAsync(db, principal, EtapaVerificacaoCadastral.AguardandoInteresse);
        var (_, wamidOrientacao) = await LinhaAsync(db, s, FinalidadeComunicacao.OrientacaoPosto,
            StatusComunicacao.Entregue, enviadoEm: DateTime.UtcNow.AddHours(-3));
        // Um lembrete que já estava na fila: a resposta promete não insistir, então ele não sai.
        var (lembreteNaFila, _) = await LinhaAsync(db, s, FinalidadeComunicacao.LembreteAgendamento,
            StatusComunicacao.Pendente);

        var ctx = Contexto(c.Conversa, "Vou ao posto", respondendo: wamidOrientacao, botao: "Vou ao posto");
        await c.Handler.TratarAsync(ctx, default);
        await db.SaveChangesAsync();

        Assert.True(ctx.Consumido);
        var depois = await LerAsync(db, principal.Id);
        // O carimbo é o que faz a régua e o lembrete não insistirem mais.
        Assert.StartsWith(ReguaReforcoConfirmacao.CarimboVaiAoPosto, depois.MotivoFalha);
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral, depois.Status);
        Assert.Equal(StatusComunicacao.Dispensada, (await LerAsync(db, lembreteNaFila.Id)).Status);
        Assert.Null(await LerEstadoAsync(db));
        var resposta = Assert.Single(Respostas(c.Whats));
        Assert.StartsWith("Combinado!", resposta);
        Assert.Contains("Não vamos mais insistir", resposta);
        // Ir ao posto não diz nada sobre o número: nenhuma pendência de cadastro.
        Assert.False(await db.PendenciasCadastro.AsNoTracking().AnyAsync(p => p.TelefoneCanonical == _telefone));
    }

    [Fact]
    public async Task Vou_ao_posto_sem_agendamento_esperando_segue_para_o_robo()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CriarAsync(db);

        var ctx = Contexto(c.Conversa, "vou ao posto");
        await c.Handler.TratarAsync(ctx, default);

        Assert.False(ctx.Consumido);
        Assert.Empty(Respostas(c.Whats));
    }

    // ===================== (h) "Quero mais informações" no meio do diálogo =====================

    [Fact]
    public async Task Verificacao_parada_no_nascimento_ha_dias_recomeca_pelo_cpf_sem_devolver_as_chances()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var c = await CriarAsync(db, joana);
        var (_, principal) = await RetidaAsync(db, joana);
        await EstadoAsync(db, principal, EtapaVerificacaoCadastral.AguardandoNascimento,
            atualizadoEm: DateTime.UtcNow.AddDays(-3), pacienteId: joana.Id, tentativasErradas: 1, cpfDigitos: "1112");

        await c.Handler.TratarAsync(
            Contexto(c.Conversa, "Quero mais informações", botao: "Quero mais informações"), default);
        await db.SaveChangesAsync();

        var estado = (await LerEstadoAsync(db))!;
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoCpf, estado.Etapa);
        Assert.Equal(1, estado.TentativasErradas); // a chance gasta continua contada
        Assert.Null(estado.PacienteId);
        Assert.Null(estado.CpfDigitosInformados);
        Assert.Equal(principal.Id, estado.ComunicacaoPacienteId);
        Assert.Contains("4 primeiros números do CPF", Assert.Single(Respostas(c.Whats)));
    }

    [Fact]
    public async Task Verificacao_recente_no_nascimento_repete_a_pergunta_do_nascimento_e_nao_pede_cpf()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var c = await CriarAsync(db, joana);
        var (_, principal) = await RetidaAsync(db, joana);
        await EstadoAsync(db, principal, EtapaVerificacaoCadastral.AguardandoNascimento,
            atualizadoEm: DateTime.UtcNow.AddHours(-1), pacienteId: joana.Id, cpfDigitos: "1112");

        await c.Handler.TratarAsync(
            Contexto(c.Conversa, "Quero mais informações", botao: "Quero mais informações"), default);
        await db.SaveChangesAsync();

        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNascimento, (await LerEstadoAsync(db))!.Etapa);
        var resposta = Assert.Single(Respostas(c.Whats));
        Assert.Contains("mês e do ano de nascimento", resposta);
        Assert.DoesNotContain("CPF", resposta);
    }

    [Fact]
    public async Task Verificacao_esgotada_orienta_o_posto_e_nao_reabre_as_chances()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var c = await CriarAsync(db, joana);
        var (_, principal) = await RetidaAsync(db, joana);
        await EstadoAsync(db, principal, EtapaVerificacaoCadastral.Esgotado,
            atualizadoEm: DateTime.UtcNow.AddDays(-2), tentativasErradas: 3);

        await c.Handler.TratarAsync(
            Contexto(c.Conversa, "Quero mais informações", botao: "Quero mais informações"), default);
        await db.SaveChangesAsync();

        var estado = (await LerEstadoAsync(db))!;
        Assert.Equal(EtapaVerificacaoCadastral.Esgotado, estado.Etapa);
        Assert.Equal(3, estado.TentativasErradas);
        var resposta = Assert.Single(Respostas(c.Whats));
        Assert.Contains("nas tentativas anteriores", resposta);
        Assert.Contains("posto de saúde onde o paciente tem cadastro", resposta);
        Assert.DoesNotContain("CPF", resposta);
    }

    // ===================== (g) liberação honesta =====================

    [Fact]
    public async Task Lembrete_pendurado_e_liberado_e_sai_como_a_confirmacao_completa()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var c = await CriarAsync(db, joana);

        // O LEMBRETE de quem não respondeu passou pelo desafio e ficou pendurado — era o que a
        // liberação antiga (só finalidade 1) deixava para trás.
        var (s, lembrete) = await RetidaAsync(db, joana, finalidade: FinalidadeComunicacao.LembreteAgendamento);
        var estado = await EstadoAsync(db, lembrete, EtapaVerificacaoCadastral.AguardandoVinculo, pacienteId: joana.Id);

        await c.Handler.TratarAsync(
            Contexto(c.Conversa, "Sou o paciente", interativo: $"vcad_vinc_proprio:{estado.Id}"), default);
        await db.SaveChangesAsync();

        await using var db2 = fixture.CriarDbContext();
        var liberado = await LerAsync(db2, lembrete.Id);
        Assert.Equal(StatusComunicacao.Pendente, liberado.Status);
        Assert.True(liberado.IgnorarVerificacaoTelefone);
        Assert.True(liberado.IgnorarJanelaHorario);
        Assert.Contains("Já estou enviando", Assert.Single(Respostas(c.Whats)));

        // E ele sai com os DADOS (confirmacao_regulacao) — não a primeira mensagem curta de novo.
        var link = new CidadaoLoginLink
        {
            Id = Guid.CreateVersion7(),
            PatientId = joana.Id,
            Cpf = joana.Cpf,
            SolicitacaoId = s.Id,
            ExpiraEm = DateTime.UtcNow.AddDays(3),
            CriadoEm = DateTime.UtcNow,
        };
        db2.CidadaoLoginLinks.Add(link);
        await db2.SaveChangesAsync();

        var links = Substitute.For<ICidadaoLoginLinkService>();
        links.GerarParaSolicitacaoAsync(default, default, default, default)
            .ReturnsForAnyArgs(new MagicLinkDto(link.Id, "https://app.exemplo/entrar", link.ExpiraEm));
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(joana.Id, Arg.Any<CancellationToken>()).Returns(Dto(joana, celular: _telefone));
        var whats = Substitute.For<IWhatsAppCliente>();
        whats.ListarTemplatesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TemplateWhatsApp>());
        whats.EnviarTemplateComBotoesAsync(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.CONF.{Guid.NewGuid():N}", null));
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>())
            .Returns(new ConfirmacaoConfiguracaoDto("00:00", "00:00", 100, SomenteSisreg: false, true, null));
        var servico = new ComunicacaoPacienteService(
            db2, pacientes, links, whats, Options.Create(new ComunicacaoPacienteOptions()),
            new UsuarioAtualAccessorFake(), Substitute.For<IDispensaContatoService>(),
            Substitute.For<IContatoComprometidoService>(), regras, NullLogger<ComunicacaoPacienteService>.Instance);

        await servico.ProcessarTentativaEnvioAsync(lembrete.Id);

        var envio = Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateComBotoesAsync));
        Assert.Equal("confirmacao_regulacao", envio.GetArguments()[1]);
        Assert.DoesNotContain(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync));
        await using var db3 = fixture.CriarDbContext();
        Assert.Equal(StatusComunicacao.Enviada, (await LerAsync(db3, lembrete.Id)).Status);
    }

    [Fact]
    public async Task Confirmacao_e_lembrete_do_mesmo_agendamento_saem_como_uma_mensagem_so()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var (s, principal) = await RetidaAsync(db, joana);
        var (lembrete, _) = await LinhaAsync(db, s, FinalidadeComunicacao.LembreteAgendamento,
            StatusComunicacao.AguardandoVerificacaoCadastral);

        var r = await VerificacaoCadastralHandlerTests.Liberacao(db).LiberarAsync(joana.Id, lembrete.Id);
        await db.SaveChangesAsync();

        Assert.Equal(1, r.Liberadas);
        Assert.Equal(DesfechoLiberacao.Liberou, r.Desfecho);
        await using var db2 = fixture.CriarDbContext();
        // A confirmação vence; o lembrete fica coberto — senão a pessoa receberia a mesma
        // confirmação completa duas vezes seguidas.
        Assert.Equal(StatusComunicacao.Pendente, (await LerAsync(db2, principal.Id)).Status);
        var coberto = await LerAsync(db2, lembrete.Id);
        Assert.Equal(StatusComunicacao.Dispensada, coberto.Status);
        Assert.Equal(LiberacaoAposIdentificacao.MotivoCoberta, coberto.MotivoFalha);
    }

    [Fact]
    public async Task Agendamento_que_ja_passou_nao_promete_envio_e_diz_por_que()
    {
        await using var db = fixture.CriarDbContext();
        var joana = NovaPessoa("JOANA DE SOUZA", "11122233344");
        var c = await CriarAsync(db, joana);
        var dataPassada = DateTime.UtcNow.AddDays(-2);
        var (_, principal) = await RetidaAsync(db, joana, dataAgendada: dataPassada, criadoEm: DateTime.UtcNow.AddDays(-6));
        var estado = await EstadoAsync(db, principal, EtapaVerificacaoCadastral.AguardandoVinculo, pacienteId: joana.Id);

        await c.Handler.TratarAsync(
            Contexto(c.Conversa, "Sou o paciente", interativo: $"vcad_vinc_proprio:{estado.Id}"), default);
        await db.SaveChangesAsync();

        var resposta = Assert.Single(Respostas(c.Whats));
        Assert.DoesNotContain("Já estou enviando", resposta);
        Assert.Contains("já passou", resposta);
        Assert.Contains(FusoBrasilia.ParaExibicao(dataPassada).ToString("dd/MM", CultureInfo.GetCultureInfo("pt-BR")), resposta);

        await using var db2 = fixture.CriarDbContext();
        var depois = await LerAsync(db2, principal.Id);
        Assert.Equal(StatusComunicacao.Falha, depois.Status);
        Assert.StartsWith("Identificação concluída em", depois.MotivoFalha);
    }
}

/// <summary>
/// A frase depois da identificação, uma por desfecho (25/09/2026). Testes puros: a regra é
/// "dizer o que aconteceu" — o "Já estou enviando" só pode aparecer quando algo foi para a fila.
/// </summary>
public class FraseDepoisDaIdentificacaoTests
{
    private const string Telefone = "5521987654321";

    private static readonly PacienteDto Joana = PacienteDtoFabrica.Criar(Guid.NewGuid(), "JOANA DE SOUZA");

    private static string Frase(
        LiberacaoResultado r, bool trocariaVerificado = false, PacienteDto? paciente = null,
        VinculoContatoVerificado vinculo = VinculoContatoVerificado.Proprio) =>
        VerificacaoCadastralWhatsAppHandler.FraseDepoisDaIdentificacao(
            r, paciente ?? Joana, vinculo, Telefone, trocariaVerificado);

    private static LiberacaoResultado Zero(DesfechoLiberacao d, DateTime? data = null, string? telefone = null) =>
        new(0, d, data, telefone, "motivo");

    [Fact]
    public void Liberou_diz_que_esta_enviando()
        => Assert.Equal(
            "Perfeito, Joana! Cadastro confirmado. Já estou enviando as informações do agendamento — chegam aqui em instantes.",
            Frase(new LiberacaoResultado(1, DesfechoLiberacao.Liberou, null, null, "1")));

    [Fact]
    public void Com_outro_numero_verificado_diz_para_onde_vai()
    {
        var comOutro = Joana with { TelefoneVerificado = "5521999991234" };
        var frase = Frase(new LiberacaoResultado(1, DesfechoLiberacao.Liberou, null, null, "1"),
            trocariaVerificado: true, paciente: comOutro);
        Assert.Equal(
            "Cadastro confirmado, obrigado! As informações vão para o WhatsApp já cadastrado de *Joana* (final …1234). "
            + "Para trocar o número, procure o posto com documento.", frase);
    }

    [Fact]
    public void Atendente_assumiu_nao_promete_envio()
        => Assert.Equal(
            "Perfeito, Joana! Cadastro confirmado. Sobre esse agendamento, uma atendente da nossa equipe já está "
            + "cuidando e fala com você por aqui em horário de atendimento.",
            Frase(Zero(DesfechoLiberacao.AtendenteAssumiu)));

    [Fact]
    public void Agendamento_passado_diz_o_dia()
    {
        // 21/09 às 13h em Brasília.
        var frase = Frase(Zero(DesfechoLiberacao.AgendamentoPassou, new DateTime(2026, 9, 21, 16, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(
            "Cadastro confirmado, obrigado! O agendamento de *Joana* era no dia 21/09 e já passou, então não tenho "
            + "informação nova para enviar. Se precisar remarcar, procure o posto de saúde onde o paciente é atendido "
            + "— ou escreva *atendente*.", frase);
    }

    [Fact]
    public void Agendamento_cancelado_diz_que_foi_cancelado()
        => Assert.Contains("O agendamento de *Joana* foi cancelado, então não tenho informação nova",
            Frase(Zero(DesfechoLiberacao.AgendamentoCancelado)));

    [Fact]
    public void Ja_enviado_para_este_numero_aponta_a_mensagem_acima()
        => Assert.Equal(
            "Perfeito, Joana! Cadastro confirmado. As informações desse agendamento já foram enviadas para este "
            + "número — é a mensagem com data e local, logo acima.",
            Frase(Zero(DesfechoLiberacao.JaEnviado, telefone: Telefone)));

    [Fact]
    public void Ja_enviado_para_OUTRO_numero_nao_diz_logo_acima()
        => Assert.Contains("No momento não há aviso pendente para *Joana*",
            Frase(Zero(DesfechoLiberacao.JaEnviado, telefone: "5521911112222")));

    [Fact]
    public void Nada_pendente_diz_que_chega_quando_houver()
        => Assert.Equal(
            "Perfeito! Cadastro confirmado. No momento não há aviso pendente para *Joana*. Quando houver "
            + "novidade, chega por aqui.",
            Frase(Zero(DesfechoLiberacao.NadaPendente), vinculo: VinculoContatoVerificado.MaeOuPaiOuResponsavel));
}
