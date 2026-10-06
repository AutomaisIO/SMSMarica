using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.AgenteIa.WhatsApp;
using SMSMais.Core.Alertas;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.AgenteIa;
using SMSMais.Data.Entities.Alertas;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.AgenteIa;

/// <summary>
/// Fluxo do Agente IA pelo WhatsApp (ADR-0068) contra o banco: pedido → turno → andamento →
/// resposta, comandos e o interruptor. Motor e WhatsApp são falsos.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AgenteWhatsAppProcessadorTests(PostgresFixture fixture)
{
    private readonly IAgenteIaMotorWhatsApp _motor = Substitute.For<IAgenteIaMotorWhatsApp>();
    private readonly IWhatsAppCliente _whats = Substitute.For<IWhatsAppCliente>();
    private readonly ITelefonesAgenteIa _telefones = Substitute.For<ITelefonesAgenteIa>();
    private readonly List<string> _enviados = [];

    private AgenteWhatsAppProcessador Criar(SmsMaisDbContext db)
    {
        _whats.EnviarTextoAsync(Arg.Any<string>(), Arg.Do<string>(_enviados.Add), Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>())
            .Returns(new EnvioWhatsAppResultado(true, "wamid", null));
        return new AgenteWhatsAppProcessador(db, _telefones, _motor, _whats, NullLogger<AgenteWhatsAppProcessador>.Instance);
    }

    private static string NovoTelefone() => "55219" + Random.Shared.Next(10000000, 99999999);

    private void Ligado(string telefone, Guid usuarioId) =>
        _telefones.ObterAsync(telefone, Arg.Any<CancellationToken>())
            .Returns(new TelefoneAgenteIa(telefone, usuarioId, "Operador"));

    private static async Task<AgenteWhatsAppPedido> PedidoAsync(SmsMaisDbContext db, string telefone, string texto)
    {
        var p = new AgenteWhatsAppPedido
        {
            Id = Guid.CreateVersion7(),
            Telefone = telefone,
            UsuarioId = Guid.NewGuid(),
            MensagemId = Guid.NewGuid(),
            Texto = texto,
            Situacao = SituacaoPedidoAgente.Pendente,
            CriadoEm = DateTime.UtcNow,
        };
        db.AgenteWhatsAppPedidos.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    private static Task<AgenteWhatsAppPedido> RelerAsync(SmsMaisDbContext db, Guid id) =>
        db.AgenteWhatsAppPedidos.AsNoTracking().SingleAsync(p => p.Id == id);

    [Fact]
    public async Task Pedido_abre_turno_manda_andamento_no_ritmo_e_a_resposta_no_fim()
    {
        await using var db = fixture.CriarDbContext();
        var fone = NovoTelefone();
        var usuario = Guid.NewGuid();
        Ligado(fone, usuario);
        var pedido = await PedidoAsync(db, fone, "por que o robô parou?");
        _motor.IniciarTurnoAsync(fone, "por que o robô parou?", usuario, "Operador", Arg.Any<CancellationToken>())
            .Returns(new TurnoIniciado("s1", "t1", NovaSessao: true));
        var proc = Criar(db);

        // 1. Abre o turno e confirma o recebimento.
        (await proc.ProcessarAsync(default)).Should().BeTrue();
        (await RelerAsync(db, pedido.Id)).Situacao.Should().Be(SituacaoPedidoAgente.EmAndamento);
        _enviados.Should().ContainSingle().Which.Should().Contain("Sessão nova");

        // 2. Texto do agente antes dos 30 s: acumula, não manda.
        _motor.LerTurnoAsync("t1", 0, Arg.Any<CancellationToken>())
            .Returns(new LeituraTurno("running", null, [new EventoTurno("text", "Olhando os logs do robô.", false)], 1));
        await proc.ProcessarAsync(default);
        _enviados.Should().HaveCount(1);

        // 3. Passados 30 s: o andamento sai.
        await db.AgenteWhatsAppPedidos.Where(p => p.Id == pedido.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UltimoAndamentoEm, DateTime.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear();
        _motor.LerTurnoAsync("t1", 1, Arg.Any<CancellationToken>())
            .Returns(new LeituraTurno("running", null, [], 1));
        await proc.ProcessarAsync(default);
        _enviados.Should().HaveCount(2);
        _enviados[1].Should().Be("⏳ Olhando os logs do robô.");

        // 4. Terminou: vai a resposta final, só ela.
        _motor.LerTurnoAsync("t1", 1, Arg.Any<CancellationToken>())
            .Returns(new LeituraTurno("done", null,
                [new EventoTurno("text", "Crédito da IA acabou.", false), new EventoTurno("result", "Crédito da IA acabou.", false)], 3));
        (await proc.ProcessarAsync(default)).Should().BeFalse();
        _enviados.Should().HaveCount(3);
        _enviados[2].Should().Be("Crédito da IA acabou.");
        (await RelerAsync(db, pedido.Id)).Situacao.Should().Be(SituacaoPedidoAgente.Concluido);
    }

    [Fact]
    public async Task Mensagem_que_chega_com_turno_rodando_espera_e_e_avisada_uma_vez()
    {
        await using var db = fixture.CriarDbContext();
        var fone = NovoTelefone();
        Ligado(fone, Guid.NewGuid());
        var primeiro = await PedidoAsync(db, fone, "primeiro");
        _motor.IniciarTurnoAsync(fone, "primeiro", Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TurnoIniciado("s1", "t1", false));
        _motor.LerTurnoAsync("t1", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LeituraTurno("running", null, [], 0));
        var proc = Criar(db);
        await proc.ProcessarAsync(default);

        var segundo = await PedidoAsync(db, fone, "segundo");
        await proc.ProcessarAsync(default);
        await proc.ProcessarAsync(default);

        _enviados.Count(e => e.StartsWith("📥")).Should().Be(1);
        (await RelerAsync(db, segundo.Id)).Situacao.Should().Be(SituacaoPedidoAgente.Pendente);
        await _motor.DidNotReceive().IniciarTurnoAsync(fone, "segundo", Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await RelerAsync(db, primeiro.Id)).Situacao.Should().Be(SituacaoPedidoAgente.EmAndamento);
    }

    [Fact]
    public async Task Parar_interrompe_o_turno_em_andamento()
    {
        await using var db = fixture.CriarDbContext();
        var fone = NovoTelefone();
        Ligado(fone, Guid.NewGuid());
        var pedido = await PedidoAsync(db, fone, "faz o deploy");
        _motor.IniciarTurnoAsync(fone, "faz o deploy", Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TurnoIniciado("s1", "t9", false));
        var proc = Criar(db);
        await proc.ProcessarAsync(default);

        await PedidoAsync(db, fone, "parar");
        await proc.ProcessarAsync(default);

        await _motor.Received(1).CancelarTurnoAsync("t9", Arg.Any<CancellationToken>());
        (await RelerAsync(db, pedido.Id)).Situacao.Should().Be(SituacaoPedidoAgente.Cancelado);
        _enviados.Last().Should().StartWith("⏹");
    }

    /// <summary>Desligar a chave na tela é o interruptor: corta o turno e não manda mais nada.</summary>
    [Fact]
    public async Task Chave_desligada_cancela_tudo_em_silencio()
    {
        await using var db = fixture.CriarDbContext();
        var fone = NovoTelefone();
        Ligado(fone, Guid.NewGuid());
        var pedido = await PedidoAsync(db, fone, "investiga");
        _motor.IniciarTurnoAsync(fone, "investiga", Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TurnoIniciado("s1", "t5", false));
        var proc = Criar(db);
        await proc.ProcessarAsync(default);
        var enviadosAntes = _enviados.Count;

        _telefones.ObterAsync(fone, Arg.Any<CancellationToken>()).Returns((TelefoneAgenteIa?)null);
        await proc.ProcessarAsync(default);

        await _motor.Received(1).CancelarTurnoAsync("t5", Arg.Any<CancellationToken>());
        (await RelerAsync(db, pedido.Id)).Situacao.Should().Be(SituacaoPedidoAgente.Cancelado);
        _enviados.Should().HaveCount(enviadosAntes);
    }

    [Fact]
    public async Task Mensagem_sem_texto_responde_que_so_le_texto()
    {
        await using var db = fixture.CriarDbContext();
        var fone = NovoTelefone();
        Ligado(fone, Guid.NewGuid());
        var pedido = await PedidoAsync(db, fone, "");
        await Criar(db).ProcessarAsync(default);

        _enviados.Should().ContainSingle().Which.Should().Contain("só leio texto");
        (await RelerAsync(db, pedido.Id)).Situacao.Should().Be(SituacaoPedidoAgente.Concluido);
        await _motor.DidNotReceiveWithAnyArgs().IniciarTurnoAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Telefone_do_agente_e_reconhecido_sem_o_nono_digito_e_some_quando_desligado()
    {
        await using var db = fixture.CriarDbContext();
        var usuario = await UsuarioAsync(db);
        // Celular no formato antigo começa em 6–9 (fixo começa em 2–5 e não ganha o nono dígito).
        var local = "9" + Random.Shared.Next(1000000, 9999999);
        var destinatario = new AlertaDestinatario
        {
            Id = Guid.CreateVersion7(), Telefone = $"219{local}", Ativo = true, CriadoEm = DateTime.UtcNow,
            AgenteIa = true, AgenteUsuarioId = usuario,
        };
        db.AlertaDestinatarios.Add(destinatario);
        await db.SaveChangesAsync();
        var servico = new TelefonesAgenteIa(db);

        (await servico.ObterAsync($"5521{local}"))!.UsuarioId.Should().Be(usuario);
        (await servico.GrafiasOcultasAsync()).Should().Contain($"5521{local}");

        destinatario.AgenteIa = false;
        destinatario.AgenteUsuarioId = null;
        await db.SaveChangesAsync();
        (await servico.ObterAsync($"5521{local}")).Should().BeNull();
    }

    /// <summary>
    /// A mensagem do celular do agente vira pedido com o aviso citado, encerra a cadeia (robô e
    /// confirmação não a veem) e uma foto não vai para a fila de mídia do acervo.
    /// </summary>
    [Fact]
    public async Task Manipulador_grava_pedido_com_o_aviso_citado_e_encerra_a_cadeia()
    {
        await using var db = fixture.CriarDbContext();
        var fone = NovoTelefone();
        var usuario = Guid.NewGuid();
        Ligado(fone, usuario);
        var aviso = new SMSMais.Data.Entities.Notificacoes.MensagemWhatsApp
        {
            Id = Guid.CreateVersion7(), Telefone = fone, Direcao = SMSMais.Data.Entities.Enums.DirecaoMensagem.Saida,
            Conteudo = "*⚠ Erro 500 — ERRO-7F3A21*", WaMessageId = $"wamid.{Guid.NewGuid():N}",
            OcorridoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };
        db.MensagensWhatsApp.Add(aviso);
        await db.SaveChangesAsync();

        var conversa = new SMSMais.Data.Entities.Conversas.Conversa { Id = Guid.NewGuid(), TelefoneCanonical = fone, NaoLidas = 1 };
        var recebida = new SMSMais.Data.Entities.Notificacoes.MensagemWhatsApp
        {
            Id = Guid.NewGuid(), ContextoWaMessageId = aviso.WaMessageId,
            MidiaSituacao = SMSMais.Data.Entities.Enums.SituacaoMidiaWhatsApp.Recebendo,
        };
        var ctx = new SMSMais.Core.Notificacoes.WhatsApp.Manipuladores.ManipuladorContexto(conversa, recebida, " resolve ", null);

        await new AgenteIaWhatsAppHandler(db, _telefones, new SinalAgenteWhatsApp()).TratarAsync(ctx, default);

        ctx.Encerrado.Should().BeTrue();
        ctx.Consumido.Should().BeTrue();
        conversa.NaoLidas.Should().Be(0);
        recebida.MidiaSituacao.Should().BeNull();
        var pedido = db.ChangeTracker.Entries<AgenteWhatsAppPedido>().Single().Entity;
        pedido.Texto.Should().Be("resolve");
        pedido.Citado.Should().Contain("ERRO-7F3A21");
        pedido.UsuarioId.Should().Be(usuario);
        pedido.Telefone.Should().Be(TelefonesAgenteIa.Chave(fone));
    }

    /// <summary>Quem só cuida dos avisos não liga o agente no próprio celular em nome de outra pessoa.</summary>
    [Fact]
    public async Task So_o_proprio_usuario_liga_o_agente_mas_qualquer_um_desliga()
    {
        await using var db = fixture.CriarDbContext();
        var dono = await UsuarioAsync(db);
        var outro = await UsuarioAsync(db);
        var telefone = "219" + Random.Shared.Next(10000000, 99999999);

        AlertaPlataformaService Servico(Guid quem) => new(
            db, null!, _whats, null!, new UsuarioAtualAccessorFake(quem), Substitute.For<SMSMais.Core.Identidade.IIdentidadeService>());

        var criar = () => Servico(outro).AdicionarDestinatarioAsync(
            new SalvarAlertaDestinatarioRequest(telefone, null, true, AgenteIa: true, AgenteUsuarioId: dono));
        (await criar.Should().ThrowAsync<ValidacaoException>()).Which.Message.Should().Contain("próprio usuário");

        // Ligado direto no banco (como se o dono tivesse ligado); outro usuário desliga sem restrição.
        var d = new AlertaDestinatario
        {
            Id = Guid.CreateVersion7(), Telefone = telefone, Ativo = true, CriadoEm = DateTime.UtcNow,
            AgenteIa = true, AgenteUsuarioId = dono,
        };
        db.AlertaDestinatarios.Add(d);
        await db.SaveChangesAsync();

        var dto = await Servico(outro).AtualizarDestinatarioAsync(d.Id, new SalvarAlertaDestinatarioRequest(telefone, null));
        dto.AgenteIa.Should().BeFalse();
        dto.AgenteUsuarioId.Should().BeNull();
    }

    private static async Task<Guid> UsuarioAsync(SmsMaisDbContext db)
    {
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "OPERADOR DO AGENTE",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }
}
