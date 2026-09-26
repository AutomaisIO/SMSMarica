using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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
/// Máquina de estados da verificação cadastral (dígitos do CPF → mês/ano → nome → envia a
/// comunicação PENDURADA). É o gate que substituiu o LLM depois do incidente de 26/08, então o
/// que está sob teste é justamente o que falhou lá: a régua das 3 chances tem de ser real
/// (reenviar dígitos não pode zerar o contador) e nenhum dado de agendamento pode ser liberado
/// antes dos três fatores. O handler não chama SaveChanges (contrato do webhook) — os testes
/// commitam após TratarAsync.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class VerificacaoCadastralHandlerTests(PostgresFixture fixture)
{
    // _telefone PRÓPRIO por teste: a bancada externa guarda linhas entre execuções e o índice
    // único por telefone faria um teste derrubar o outro (xUnit cria uma instância por teste).
    private readonly string _telefone = $"5521{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
    private const string Cpf = "04528822733";
    private const string Nome = "MARIA APARECIDA DA SILVA";
    private static readonly DateOnly Nascimento = new(1980, 3, 15);

    // ---------- infraestrutura do teste ----------

    private static PacienteDto Paciente(Guid id) =>
        new(id, Nome, Cpf, null, 0, 0, true, DateTime.UtcNow,
            null, Nascimento, default, default, default, default, null, null, "Brasileira",
            null, null, null, null,
            null, null, null, null, null,
            null, null, default, default, [], [], [], [], null,
            null, null);

    private sealed record Cenario(
        VerificacaoCadastralWhatsAppHandler Handler,
        IWhatsAppCliente Whats,
        SMSMais.Core.Notificacoes.Comunicacao.IComunicacaoPacienteService Comunicacoes,
        ComunicacaoPaciente Comunicacao,
        Conversa Conversa,
        Guid PacienteId);

    private static IWhatsAppCliente CriarWhatsAppMock()
    {
        var cliente = Substitute.For<IWhatsAppCliente>();
        cliente.EnviarTextoAsync(null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.OUT.{Guid.NewGuid():N}", null));
        cliente.EnviarInterativoBotoesAsync(null!, null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.OUT.{Guid.NewGuid():N}", null));
        return cliente;
    }

    /// <summary>A conversa é a MESMA em todas as mensagens (e existe no banco): a pendência de
    /// "número errado" tem FK para ela, e uma conversa solta faria o teste falhar por FK — não
    /// pela regra sob teste.</summary>
    private ManipuladorContexto Contexto(
        Conversa conversa, string? texto = null, string? interativoReplyId = null, string? botaoPayload = null)
    {
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
        return new ManipuladorContexto(conversa, msg, texto, null, botaoPayload, interativoReplyId);
    }

    /// <summary>Semeia solicitação + comunicação retida (status 8) + o estado no início do diálogo.</summary>
    private async Task<Cenario> PrepararAsync(SmsMaisDbContext db)
    {
        var pacienteId = Guid.NewGuid();
        var exame = await SeedSolicitacao.CriarAsync(db, pacienteId, dataAgendada: DateTime.UtcNow.AddDays(5));

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

        var comunicacao = new ComunicacaoPaciente
        {
            Id = Guid.NewGuid(),
            Tipo = TipoAgendamento.Exame,
            SolicitacaoId = exame.SolicitacaoId,
            PacienteId = pacienteId,
            Telefone = _telefone,
            Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
            Status = StatusComunicacao.AguardandoVerificacaoCadastral,
            CriadoEm = DateTime.UtcNow,
        };
        db.ComunicacoesPaciente.Add(comunicacao);
        db.VerificacoesCadastraisEstado.Add(new VerificacaoCadastralEstado
        {
            Id = Guid.NewGuid(),
            TelefoneCanonical = _telefone,
            ComunicacaoPacienteId = comunicacao.Id,
            Etapa = EtapaVerificacaoCadastral.AguardandoCpf,
            ExpiraEm = DateTime.UtcNow.AddDays(7),
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(pacienteId).ReturnsForAnyArgs(Paciente(pacienteId));
        var whats = CriarWhatsAppMock();
        var pendencias = new SMSMais.Core.PendenciasCadastro.PendenciaCadastroService(
            db, new UsuarioAtualAccessorFake(), pacientes,
            Substitute.For<SMSMais.Core.Pacientes.Fhir.IPacienteFhirClient>(),
            NullLogger<SMSMais.Core.PendenciasCadastro.PendenciaCadastroService>.Instance);
        var comunicacoes = Substitute.For<SMSMais.Core.Notificacoes.Comunicacao.IComunicacaoPacienteService>();
        var handler = new VerificacaoCadastralWhatsAppHandler(
            db, whats, pacientes, Substitute.For<ITelefoneValidacaoService>(), pendencias,
            new Lazy<SMSMais.Core.Notificacoes.Comunicacao.IComunicacaoPacienteService>(() => comunicacoes),
            NullLogger<VerificacaoCadastralWhatsAppHandler>.Instance);

        return new Cenario(handler, whats, comunicacoes, comunicacao, conversa, pacienteId);
    }

    private async Task ResponderAsync(SmsMaisDbContext db, Cenario c, string texto)
    {
        await c.Handler.TratarAsync(Contexto(c.Conversa, texto), default);
        await db.SaveChangesAsync();
    }

    private Task<VerificacaoCadastralEstado?> EstadoAsync(SmsMaisDbContext db) =>
        db.VerificacoesCadastraisEstado.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TelefoneCanonical == _telefone);

    // ---------- caminho feliz ----------

    [Fact]
    public async Task Tres_fatores_liberam_a_comunicacao_pendurada()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await ResponderAsync(db, c, "0452");            // dígitos do CPF
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNascimento, (await EstadoAsync(db))!.Etapa);

        await ResponderAsync(db, c, "03/1980");         // mês/ano
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNome, (await EstadoAsync(db))!.Etapa);

        // Nada foi liberado antes do 3º fator — é o vazamento que derrubou o robô.
        var antes = await db.ComunicacoesPaciente.AsNoTracking().FirstAsync(n => n.Id == c.Comunicacao.Id);
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral, antes.Status);

        await ResponderAsync(db, c, "sim");             // confirma o nome
        // 4º passo (LGPD): a que título recebe. Só aí a comunicação é liberada.
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoVinculo, (await EstadoAsync(db))!.Etapa);
        var noVinculo = await db.ComunicacoesPaciente.AsNoTracking().FirstAsync(n => n.Id == c.Comunicacao.Id);
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral, noVinculo.Status);

        await ResponderAsync(db, c, "sou a mãe dele");  // vínculo declarado
        var depois = await db.ComunicacoesPaciente.AsNoTracking().FirstAsync(n => n.Id == c.Comunicacao.Id);
        Assert.Equal(StatusComunicacao.Pendente, depois.Status);
        Assert.True(depois.IgnorarVerificacaoTelefone); // envia mesmo se o carimbo FHIR falhar
        Assert.Null(await EstadoAsync(db));             // diálogo encerrado
    }

    [Fact]
    public async Task Confirmacao_e_enviada_IMEDIATAMENTE_ao_concluir_a_verificacao()
    {
        // Regressão do "chegam em instantes" que nunca chegava: concluída a verificação, o envio das
        // informações do agendamento acontece AGORA (inline), não depende do commit único do webhook
        // (que reverte na corrida da IX_conversa) nem do worker.
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await ResponderAsync(db, c, "0452");            // dígitos do CPF
        await ResponderAsync(db, c, "03/1980");         // mês/ano
        await ResponderAsync(db, c, "sim");             // confirma o nome
        await ResponderAsync(db, c, "sou a mãe dele");  // vínculo → conclui

        await c.Comunicacoes.Received(1).ProcessarTentativaEnvioAsync(
            c.Comunicacao.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nascimento_com_mes_por_extenso_tambem_confere()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await ResponderAsync(db, c, "045.288");
        await ResponderAsync(db, c, "março de 1980");

        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNome, (await EstadoAsync(db))!.Etapa);
    }

    // ---------- as 3 chances ----------

    [Fact]
    public async Task Dados_errados_explicam_que_sao_do_paciente_e_oferecem_nova_tentativa()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await ResponderAsync(db, c, "1828"); // não confere

        var estado = (await EstadoAsync(db))!;
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNovaTentativa, estado.Etapa);
        Assert.Equal(1, estado.TentativasErradas);

        // Mensagem SEPARADA dizendo de quem são os dados, com o primeiro nome do paciente.
        await c.Whats.Received().EnviarTextoAsync(
            _telefone,
            Arg.Is<string>(t => t.Contains("Maria") && t.Contains("parente")),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>());
        // E a pergunta "quer tentar de novo?" em botões.
        await c.Whats.Received().EnviarInterativoBotoesAsync(
            _telefone, Arg.Is<string>(t => t.Contains("tentar novamente")),
            Arg.Any<IReadOnlyList<BotaoInterativoWhatsApp>>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>());
    }

    [Fact]
    public async Task Reenviar_os_digitos_ja_recomeca_o_ciclo_sem_precisar_do_botao()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await ResponderAsync(db, c, "1828");   // erra
        await ResponderAsync(db, c, "0452");   // manda os dígitos certos direto

        var estado = (await EstadoAsync(db))!;
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNascimento, estado.Etapa);
        Assert.Equal(1, estado.TentativasErradas); // a chance gasta continua contada
    }

    [Fact]
    public async Task Depois_de_tres_chances_o_dialogo_esgota_e_nao_reinicia()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await ResponderAsync(db, c, "1828");
        await ResponderAsync(db, c, "1829");
        await ResponderAsync(db, c, "1830");

        var estado = (await EstadoAsync(db))!;
        Assert.Equal(EtapaVerificacaoCadastral.Esgotado, estado.Etapa);
        Assert.Equal(3, estado.TentativasErradas);

        // Reenviar dígitos (mesmo os CERTOS) não pode ressuscitar o diálogo nem zerar o contador:
        // o estado FICA de propósito, senão o backfill recriaria com 3 chances novas.
        await ResponderAsync(db, c, "0452");
        var depois = (await EstadoAsync(db))!;
        Assert.Equal(EtapaVerificacaoCadastral.Esgotado, depois.Etapa);
        Assert.Equal(3, depois.TentativasErradas);

        var comunicacao = await db.ComunicacoesPaciente.AsNoTracking().FirstAsync(n => n.Id == c.Comunicacao.Id);
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral, comunicacao.Status);
    }

    // ---------- desvios ----------

    [Fact]
    public async Task Pedido_de_atendente_interrompe_sem_gastar_chance()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await ResponderAsync(db, c, "Prefiro falar com um atendente");

        var estado = (await EstadoAsync(db))!;
        Assert.Equal(0, estado.TentativasErradas);
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoCpf, estado.Etapa);
    }

    [Fact]
    public async Task Texto_nao_interpretavel_reorienta_sem_gastar_chance()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await ResponderAsync(db, c, "bom dia, tudo bem?");

        var estado = (await EstadoAsync(db))!;
        Assert.Equal(0, estado.TentativasErradas); // não é resposta errada, é resposta ininteligível
        Assert.Equal(1, estado.Reorientacoes);
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoCpf, estado.Etapa);
    }

    [Fact]
    public async Task Negar_o_nome_registra_pendencia_de_numero_errado()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await ResponderAsync(db, c, "0452");
        await ResponderAsync(db, c, "03/1980");
        await ResponderAsync(db, c, "não");

        Assert.True(await db.PendenciasCadastro.AsNoTracking()
            .AnyAsync(p => p.TelefoneCanonical == _telefone && p.Tipo == TipoPendenciaCadastro.NumeroErrado));

        var comunicacao = await db.ComunicacoesPaciente.AsNoTracking().FirstAsync(n => n.Id == c.Comunicacao.Id);
        // Nada liberado — e fica retida como número inválido, com o motivo explícito.
        Assert.Equal(StatusComunicacao.AguardandoCorrecaoContato, comunicacao.Status);
    }

    [Fact]
    public async Task Quero_mais_informacoes_e_o_que_destrava_o_pedido_do_CPF()
    {
        // A primeira mensagem não pede nada (pedir de cara não funcionou: a pessoa ia direto no
        // outro botão). O toque em "Quero mais informações" é que abre o pedido dos 4 dígitos.
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);
        var estadoInicial = (await EstadoAsync(db))!;
        estadoInicial.Etapa = EtapaVerificacaoCadastral.AguardandoInteresse;
        await db.SaveChangesAsync();

        await c.Handler.TratarAsync(
            Contexto(c.Conversa, "Quero mais informações", botaoPayload: "Quero mais informações"), default);
        await db.SaveChangesAsync();

        Assert.Equal(EtapaVerificacaoCadastral.AguardandoCpf, (await EstadoAsync(db))!.Etapa);
        await c.Whats.Received().EnviarTextoAsync(
            _telefone,
            Arg.Is<string>(t => t.Contains("4 primeiros números do CPF")),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>());

        // E os dígitos, agora, seguem o fluxo de sempre.
        await ResponderAsync(db, c, "0452");
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNascimento, (await EstadoAsync(db))!.Etapa);
    }

    [Fact]
    public async Task Na_primeira_mensagem_os_digitos_direto_tambem_valem()
    {
        // Quem responde os dígitos sem tocar no botão não pode ser barrado por formalidade.
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);
        var estadoInicial = (await EstadoAsync(db))!;
        estadoInicial.Etapa = EtapaVerificacaoCadastral.AguardandoInteresse;
        await db.SaveChangesAsync();

        await ResponderAsync(db, c, "0452");

        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNascimento, (await EstadoAsync(db))!.Etapa);
    }

    [Fact]
    public async Task Nao_sou_essa_pessoa_pergunta_antes_e_nao_conheco_marca_o_numero_invalido()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        // Toque no botão do template: a Meta devolve o texto do botão.
        await c.Handler.TratarAsync(Contexto(c.Conversa, "Não sou essa pessoa.", botaoPayload: "Não sou essa pessoa."), default);
        await db.SaveChangesAsync();

        // Ainda NÃO marcou nada: primeiro pergunta.
        Assert.False(await db.PendenciasCadastro.AsNoTracking().AnyAsync(p => p.TelefoneCanonical == _telefone));
        await c.Whats.ReceivedWithAnyArgs(1).EnviarInterativoBotoesAsync(null!, null!, null!);

        await c.Handler.TratarAsync(Contexto(c.Conversa, "Não conheço", interativoReplyId: $"vcad_naoconheco:{c.Comunicacao.Id}"), default);
        await db.SaveChangesAsync();

        Assert.True(await db.PendenciasCadastro.AsNoTracking()
            .AnyAsync(p => p.TelefoneCanonical == _telefone && p.Tipo == TipoPendenciaCadastro.NumeroErrado
                && p.PacienteId == c.PacienteId && p.Status == StatusPendenciaCadastro.Aberta));
        var comunicacao = await db.ComunicacoesPaciente.AsNoTracking().FirstAsync(n => n.Id == c.Comunicacao.Id);
        Assert.Equal(StatusComunicacao.AguardandoCorrecaoContato, comunicacao.Status);
        Assert.Null(await EstadoAsync(db)); // interrogatório encerrado
    }

    [Fact]
    public async Task Nao_sou_essa_pessoa_e_depois_conheco_segue_o_desafio_sem_marcar()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        await c.Handler.TratarAsync(Contexto(c.Conversa, "Não sou essa pessoa.", botaoPayload: "Não sou essa pessoa."), default);
        await db.SaveChangesAsync();
        await c.Handler.TratarAsync(Contexto(c.Conversa, "Conheço", interativoReplyId: $"vcad_conheco:{c.Comunicacao.Id}"), default);
        await db.SaveChangesAsync();

        Assert.False(await db.PendenciasCadastro.AsNoTracking().AnyAsync(p => p.TelefoneCanonical == _telefone));
        Assert.NotNull(await EstadoAsync(db));

        // E o desafio continua valendo: os dígitos seguem para a etapa do nascimento.
        await ResponderAsync(db, c, "0452");
        Assert.Equal(EtapaVerificacaoCadastral.AguardandoNascimento, (await EstadoAsync(db))!.Etapa);
    }

    [Fact]
    public async Task Sem_estado_e_sem_desafio_pendente_o_texto_passa_reto()
    {
        await using var db = fixture.CriarDbContext();
        var c = await PrepararAsync(db);

        // Some com o diálogo e com o desafio: o handler não pode sequestrar a conversa.
        var estado = await db.VerificacoesCadastraisEstado.FirstAsync(e => e.TelefoneCanonical == _telefone);
        db.VerificacoesCadastraisEstado.Remove(estado);
        var com = await db.ComunicacoesPaciente.FirstAsync(n => n.Id == c.Comunicacao.Id);
        com.Status = StatusComunicacao.Enviada;
        await db.SaveChangesAsync();

        var ctx = Contexto(c.Conversa, "0452");
        await c.Handler.TratarAsync(ctx, default);

        Assert.False(ctx.Consumido);
        Assert.Null(await EstadoAsync(db));
    }
}
