using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Cidadao.Push;
using SMSMais.Core.Cidadao.Push.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Cidadao.Push;

/// <summary>
/// Push do app do cidadão contra Postgres real. Sob teste, a regra de "quem recebe": o aparelho
/// pertence à SESSÃO — trocar de login no mesmo aparelho tira o token da sessão antiga, logout
/// apaga o token, e o envio só alcança sessão viva com token. E a trilha: com aparelho, o histórico
/// é sempre gravado; sem aparelho ou sem credencial, nada é gravado. O Firebase é dublado.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PushCidadaoServiceTests
{
    private static readonly string ContaJson = ContaServicoFcmFabrica.Gerar().Json;

    private readonly IIntegracaoCredencialService _credenciais = Substitute.For<IIntegracaoCredencialService>();
    private readonly IClienteFcm _fcm = Substitute.For<IClienteFcm>();
    private readonly Guid _usuarioId = Guid.CreateVersion7();
    private readonly PostgresFixture _fixture;

    public PushCidadaoServiceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _credenciais.ObterContextoAsync(PushCidadaoService.ProvedorFcm, Arg.Any<CancellationToken>())
            .Returns(new IntegracaoCredencialContexto(PushCidadaoService.ProvedorFcm, null, ContaJson, null,
                "{\"projectId\":\"projeto-teste\"}", true));
        _credenciais.ObterAsync(PushCidadaoService.ProvedorFcm, Arg.Any<CancellationToken>())
            .Returns(new IntegracaoCredencialDto(PushCidadaoService.ProvedorFcm, "Firebase", false, true, null,
                "{\"projectId\":\"projeto-teste\"}", true));
        _fcm.ObterAccessTokenAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("ya29.acesso");
        _fcm.EnviarAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<string>(), Arg.Any<MensagemFcm>(), Arg.Any<CancellationToken>())
            .Returns(DesfechoEnvioFcm.Aceito);
    }

    private PushCidadaoService Servico(SmsMaisDbContext db) =>
        new(db, _credenciais, _fcm, new UsuarioAtualAccessorFake(_usuarioId), NullLogger<PushCidadaoService>.Instance);

    private static string TokenUnico() => $"fcm-teste-{Guid.NewGuid():N}";

    private static async Task<(Guid PacienteId, Guid AcessoId)> SemearPacienteAsync(SmsMaisDbContext db, bool ativo = true)
    {
        var acesso = new CidadaoAcesso
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.CreateVersion7(),
            Cpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(),
            Ativo = ativo,
            CriadoEm = DateTime.UtcNow,
        };
        db.CidadaoAcessos.Add(acesso);
        await db.SaveChangesAsync();
        return (acesso.PatientId, acesso.Id);
    }

    private static async Task<Guid> SemearSessaoAsync(
        SmsMaisDbContext db, Guid acessoId, string? token, string plataforma = "android",
        bool revogada = false, bool expirada = false)
    {
        var agora = DateTime.UtcNow;
        var sessao = new CidadaoSessao
        {
            Id = Guid.NewGuid(),
            CidadaoAcessoId = acessoId,
            Canal = "otp-whatsapp",
            Dispositivo = "Teste/1.0",
            CriadaEm = agora.AddDays(-1),
            ExpiraEm = expirada ? agora.AddMinutes(-1) : agora.AddDays(29),
            RevogadaEm = revogada ? agora.AddHours(-1) : null,
            PushToken = token,
            PushPlataforma = token is null ? null : plataforma,
            PushRegistradoEm = token is null ? null : agora.AddHours(-2),
        };
        db.CidadaoSessoes.Add(sessao);
        await db.SaveChangesAsync();
        return sessao.Id;
    }

    private static Task<CidadaoSessao> LerSessaoAsync(SmsMaisDbContext db, Guid id) =>
        db.CidadaoSessoes.AsNoTracking().SingleAsync(s => s.Id == id);

    // ---------- registro do aparelho ----------

    [Fact]
    public async Task Registrar_grava_na_sessao_e_tira_o_mesmo_token_de_outra_sessao_mesmo_de_outro_paciente()
    {
        await using var db = _fixture.CriarDbContext();
        var token = TokenUnico();
        var (_, acessoAntigo) = await SemearPacienteAsync(db);
        var sessaoAntiga = await SemearSessaoAsync(db, acessoAntigo, token, "ios");
        var (paciente, acesso) = await SemearPacienteAsync(db);
        var sessaoNova = await SemearSessaoAsync(db, acesso, token: null);

        await Servico(db).RegistrarAparelhoAsync(paciente, sessaoNova, new RegistrarDispositivoRequest($" {token} ", "Android"));

        var antiga = await LerSessaoAsync(db, sessaoAntiga);
        Assert.Null(antiga.PushToken);
        Assert.Null(antiga.PushPlataforma);
        Assert.Null(antiga.PushRegistradoEm);
        Assert.Null(antiga.RevogadaEm);

        var nova = await LerSessaoAsync(db, sessaoNova);
        Assert.Equal(token, nova.PushToken);
        Assert.Equal("android", nova.PushPlataforma);
        Assert.NotNull(nova.PushRegistradoEm);
        Assert.InRange(nova.PushRegistradoEm!.Value, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Registrar_em_sessao_de_outro_paciente_ou_revogada_e_recusado()
    {
        await using var db = _fixture.CriarDbContext();
        var (_, acesso) = await SemearPacienteAsync(db);
        var sessaoRevogada = await SemearSessaoAsync(db, acesso, token: null, revogada: true);
        var (outroPaciente, _) = await SemearPacienteAsync(db);
        var sessaoValida = await SemearSessaoAsync(db, acesso, token: null);
        // Quem já tem o token não pode perdê-lo por um registro que foi recusado.
        var tokenDeTerceiro = TokenUnico();
        var (_, acessoTerceiro) = await SemearPacienteAsync(db);
        var sessaoTerceiro = await SemearSessaoAsync(db, acessoTerceiro, tokenDeTerceiro);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Servico(db).RegistrarAparelhoAsync(
            outroPaciente, sessaoValida, new RegistrarDispositivoRequest(tokenDeTerceiro, "ios")));
        Assert.Null((await LerSessaoAsync(db, sessaoValida)).PushToken);

        var pacienteDono = (await db.CidadaoAcessos.AsNoTracking().SingleAsync(a => a.Id == acesso)).PatientId;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Servico(db).RegistrarAparelhoAsync(
            pacienteDono, sessaoRevogada, new RegistrarDispositivoRequest(tokenDeTerceiro, "ios")));
        Assert.Null((await LerSessaoAsync(db, sessaoRevogada)).PushToken);

        Assert.Equal(tokenDeTerceiro, (await LerSessaoAsync(db, sessaoTerceiro)).PushToken);
    }

    // ---------- envio ----------

    [Fact]
    public async Task Envio_so_alcanca_sessao_viva_com_token_e_grava_o_historico()
    {
        await using var db = _fixture.CriarDbContext();
        var (paciente, acesso) = await SemearPacienteAsync(db);
        var tokenVivo = TokenUnico();
        await SemearSessaoAsync(db, acesso, tokenVivo, "ios");
        await SemearSessaoAsync(db, acesso, TokenUnico(), revogada: true);
        await SemearSessaoAsync(db, acesso, TokenUnico(), expirada: true);
        await SemearSessaoAsync(db, acesso, token: null);

        var r = await Servico(db).EnviarAsync(paciente,
            new EnviarNotificacaoAppRequest(" Aviso ", " Abra o app para ver a novidade. ", "/exames"));

        Assert.Equal(1, r.Aparelhos);
        Assert.Equal(1, r.Entregues);
        var resultado = Assert.Single(r.Resultados);
        Assert.Equal("ios", resultado.Plataforma);
        Assert.True(resultado.Entregue);

        await _fcm.Received(1).EnviarAsync(Arg.Any<ContaServicoFcm>(), "ya29.acesso",
            Arg.Is<MensagemFcm>(m => m.Token == tokenVivo
                && m.Titulo == "Aviso"
                && m.Corpo == "Abra o app para ver a novidade."
                && m.Dados["rota"] == "/exames"
                && m.Dados["notificacaoId"] == r.NotificacaoId.ToString()),
            Arg.Any<CancellationToken>());
        await _fcm.Received(1).EnviarAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<string>(), Arg.Any<MensagemFcm>(), Arg.Any<CancellationToken>());

        var h = await db.CidadaoNotificacoes.AsNoTracking().SingleAsync(n => n.PacienteId == paciente);
        Assert.Equal(r.NotificacaoId, h.Id);
        Assert.Equal("Aviso", h.Titulo);
        Assert.Equal("Abra o app para ver a novidade.", h.Mensagem);
        Assert.Equal("/exames", h.Rota);
        Assert.Equal("painel", h.Origem);
        Assert.Equal(_usuarioId, h.EnviadoPor);
        Assert.Equal(1, h.Aparelhos);
        Assert.Equal(1, h.Entregues);
        Assert.Null(h.Falha);
    }

    [Fact]
    public async Task Aparelho_desinstalado_perde_o_token_e_o_historico_registra_a_falha()
    {
        await using var db = _fixture.CriarDbContext();
        var (paciente, acesso) = await SemearPacienteAsync(db);
        var tokenBom = TokenUnico();
        var tokenMorto = TokenUnico();
        var sessaoBoa = await SemearSessaoAsync(db, acesso, tokenBom, "android");
        var sessaoMorta = await SemearSessaoAsync(db, acesso, tokenMorto, "ios");
        var removido = new DesfechoEnvioFcm(false, DesfechoEnvioFcm.DetalheAparelhoRemovido, true,
            GravidadeFalhaFcm.Nenhuma, "app desinstalado", "FCM HTTP 404 UNREGISTERED");
        _fcm.EnviarAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<string>(),
                Arg.Is<MensagemFcm>(m => m.Token == tokenMorto), Arg.Any<CancellationToken>())
            .Returns(removido);

        var r = await Servico(db).EnviarAsync(paciente, new EnviarNotificacaoAppRequest("Aviso", "Mensagem", null));

        Assert.Equal(2, r.Aparelhos);
        Assert.Equal(1, r.Entregues);
        var falhou = Assert.Single(r.Resultados, x => !x.Entregue);
        Assert.Equal("ios", falhou.Plataforma);
        Assert.True(falhou.AparelhoRemovido);
        Assert.Equal(DesfechoEnvioFcm.DetalheAparelhoRemovido, falhou.Detalhe);

        Assert.Null((await LerSessaoAsync(db, sessaoMorta)).PushToken);
        Assert.Equal(tokenBom, (await LerSessaoAsync(db, sessaoBoa)).PushToken);

        var h = await db.CidadaoNotificacoes.AsNoTracking().SingleAsync(n => n.Id == r.NotificacaoId);
        Assert.Null(h.Rota);
        Assert.Equal("1 de 2 aparelhos recusou: app desinstalado", h.Falha);
    }

    [Fact]
    public async Task Sender_id_mismatch_nao_apaga_o_token_e_o_historico_aponta_a_configuracao()
    {
        // App gerado com outro projeto do Firebase: corrigida a chave em Integrações, o mesmo token
        // volta a receber — apagá-lo deixaria o cidadão sem notificação até reabrir o app.
        await using var db = _fixture.CriarDbContext();
        var (paciente, acesso) = await SemearPacienteAsync(db);
        var token = TokenUnico();
        var sessao = await SemearSessaoAsync(db, acesso, token);
        _fcm.EnviarAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<string>(), Arg.Any<MensagemFcm>(), Arg.Any<CancellationToken>())
            .Returns(DesfechoEnvioFcm.OutroProjeto("FCM HTTP 403 SENDER_ID_MISMATCH"));

        var r = await Servico(db).EnviarAsync(paciente, new EnviarNotificacaoAppRequest("Aviso", "Mensagem", null));

        var resultado = Assert.Single(r.Resultados);
        Assert.False(resultado.Entregue);
        Assert.False(resultado.AparelhoRemovido);
        Assert.Equal(DesfechoEnvioFcm.DetalheOutroProjeto, resultado.Detalhe);
        Assert.Equal(token, (await LerSessaoAsync(db, sessao)).PushToken);
        var h = await db.CidadaoNotificacoes.AsNoTracking().SingleAsync(n => n.Id == r.NotificacaoId);
        Assert.Equal("1 de 1 aparelho recusou: app ligado a outro projeto do Firebase", h.Falha);
    }

    [Fact]
    public async Task Credencial_que_nao_decifra_no_cofre_real_nao_vira_500_em_lugar_nenhum()
    {
        // O cofre de verdade, com o segredo cifrado por outra chave do Data Protection.
        await using var db = _fixture.CriarDbContext();
        var protetor = Substitute.For<IProtetorSegredos>();
        protetor.Proteger(Arg.Any<string>()).Returns("cifrado-em-outro-ambiente");
        protetor.Revelar(Arg.Any<string>()).Throws(new System.Security.Cryptography.CryptographicException(
            "The key {0b8f8c5e-0000-0000-0000-000000000000} was not found in the key ring."));
        var cofre = new IntegracaoCredencialService(db, protetor, new UsuarioAtualAccessorFake());
        await cofre.AtualizarAsync("fcm", new AtualizarIntegracaoCredencialRequest(null, ContaJson, null, null, true));
        var (paciente, acesso) = await SemearPacienteAsync(db);
        await SemearSessaoAsync(db, acesso, TokenUnico());
        var servico = new PushCidadaoService(db, cofre, _fcm, new UsuarioAtualAccessorFake(_usuarioId),
            NullLogger<PushCidadaoService>.Instance);

        // A ficha não decifra: continua abrindo e mostrando "configurado".
        var status = await servico.ObterStatusAsync(paciente);
        Assert.True(status.Configurado);

        var teste = await servico.TestarCredencialAsync();
        Assert.False(teste.Ok);
        Assert.Equal(PushCidadaoService.MensagemCredencialIlegivel, teste.Mensagem);

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            servico.EnviarAsync(paciente, new EnviarNotificacaoAppRequest("Aviso", "Mensagem", null)));
        Assert.Equal(new[] { "push.credencial_ilegivel" }, ex.Erros.Keys);
        Assert.False(await db.CidadaoNotificacoes.AnyAsync(n => n.PacienteId == paciente));
        await _fcm.DidNotReceiveWithAnyArgs().ObterAccessTokenAsync(default!, default, default);

        // A linha do provedor é uma só no banco de testes: não deixa o segredo ilegível ligado.
        await cofre.LimparAsync("fcm");
    }

    [Fact]
    public async Task Credencial_recusada_no_token_nao_chama_o_envio_mas_grava_o_historico()
    {
        await using var db = _fixture.CriarDbContext();
        var (paciente, acesso) = await SemearPacienteAsync(db);
        var sessao = await SemearSessaoAsync(db, acesso, TokenUnico());
        _fcm.ObterAccessTokenAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Throws(new FalhaTokenFcmException("invalid_grant", DesfechoEnvioFcm.Credencial("token OAuth2 HTTP 400")));

        var r = await Servico(db).EnviarAsync(paciente, new EnviarNotificacaoAppRequest("Aviso", "Mensagem", "/"));

        Assert.Equal(0, r.Entregues);
        Assert.Equal(DesfechoEnvioFcm.DetalheCredencial, Assert.Single(r.Resultados).Detalhe);
        await _fcm.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default!, default);
        Assert.NotNull((await LerSessaoAsync(db, sessao)).PushToken);
        var h = await db.CidadaoNotificacoes.AsNoTracking().SingleAsync(n => n.Id == r.NotificacaoId);
        Assert.Equal("1 de 1 aparelho recusou: credencial do servidor recusada", h.Falha);
    }

    [Fact]
    public async Task Sem_aparelho_ativo_e_conflito_e_nada_e_gravado()
    {
        await using var db = _fixture.CriarDbContext();
        var (paciente, acesso) = await SemearPacienteAsync(db);
        await SemearSessaoAsync(db, acesso, TokenUnico(), revogada: true);
        await SemearSessaoAsync(db, acesso, token: null);
        var (inativo, acessoInativo) = await SemearPacienteAsync(db, ativo: false);
        await SemearSessaoAsync(db, acessoInativo, TokenUnico());

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            Servico(db).EnviarAsync(paciente, new EnviarNotificacaoAppRequest("Aviso", "Mensagem", null)));
        Assert.Equal("push.sem_aparelho", ex.Codigo);
        await Assert.ThrowsAsync<ConflitoException>(() =>
            Servico(db).EnviarAsync(inativo, new EnviarNotificacaoAppRequest("Aviso", "Mensagem", null)));

        Assert.False(await db.CidadaoNotificacoes.AnyAsync(n => n.PacienteId == paciente || n.PacienteId == inativo));
        await _fcm.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Sem_credencial_ativa_e_validacao_e_nada_e_gravado()
    {
        await using var db = _fixture.CriarDbContext();
        var (paciente, acesso) = await SemearPacienteAsync(db);
        await SemearSessaoAsync(db, acesso, TokenUnico());
        _credenciais.ObterContextoAsync(PushCidadaoService.ProvedorFcm, Arg.Any<CancellationToken>())
            .Throws(new ValidacaoException("integracao.inativa", "Provedor 'fcm' está desativado."));

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            Servico(db).EnviarAsync(paciente, new EnviarNotificacaoAppRequest("Aviso", "Mensagem", null)));

        Assert.Equal(new[] { "push.nao_configurado" }, ex.Erros.Keys);
        Assert.False(await db.CidadaoNotificacoes.AnyAsync(n => n.PacienteId == paciente));
    }

    // ---------- painel ----------

    [Fact]
    public async Task Status_lista_os_aparelhos_vivos_e_o_historico_mais_recente_primeiro()
    {
        await using var db = _fixture.CriarDbContext();
        var (paciente, acesso) = await SemearPacienteAsync(db);
        var sessao = await SemearSessaoAsync(db, acesso, TokenUnico(), "ios");
        await SemearSessaoAsync(db, acesso, TokenUnico(), expirada: true);
        var servico = Servico(db);
        var primeira = await servico.EnviarAsync(paciente, new EnviarNotificacaoAppRequest("Primeira", "Mensagem", null));
        var segunda = await servico.EnviarAsync(paciente, new EnviarNotificacaoAppRequest("Segunda", "Mensagem", "/chat"));

        var s = await servico.ObterStatusAsync(paciente);

        Assert.True(s.Configurado);
        var aparelho = Assert.Single(s.Aparelhos);
        Assert.Equal(sessao, aparelho.SessaoId);
        Assert.Equal("ios", aparelho.Plataforma);
        Assert.Equal("Teste/1.0", aparelho.Dispositivo);
        Assert.Equal(new[] { segunda.NotificacaoId, primeira.NotificacaoId }, s.Notificacoes.Select(n => n.Id));
        Assert.Equal("/chat", s.Notificacoes[0].Rota);
    }

    // ---------- revogação ----------

    [Fact]
    public async Task Logout_apaga_o_token_da_sessao()
    {
        await using var db = _fixture.CriarDbContext();
        var (_, acesso) = await SemearPacienteAsync(db);
        var sessao = await SemearSessaoAsync(db, acesso, TokenUnico());
        var sessoes = new CidadaoSessaoService(db, Substitute.For<IPacienteTokenService>(),
            new ConfigurationBuilder().Build(), NullLogger<CidadaoSessaoService>.Instance);

        await sessoes.RevogarAsync(sessao);

        var s = await LerSessaoAsync(db, sessao);
        Assert.NotNull(s.RevogadaEm);
        Assert.Null(s.PushToken);
        Assert.Null(s.PushPlataforma);
        Assert.Null(s.PushRegistradoEm);
    }

    // ---------- credencial em Integrações ----------

    [Fact]
    public async Task Gravar_fcm_com_json_invalido_e_recusado_antes_de_tocar_o_banco()
    {
        await using var db = _fixture.CriarDbContext();
        var protetor = Substitute.For<IProtetorSegredos>();
        var servico = new IntegracaoCredencialService(db, protetor, new UsuarioAtualAccessorFake());

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.AtualizarAsync("fcm",
            new AtualizarIntegracaoCredencialRequest(null, "{\"type\":\"authorized_user\"}", null, null, true)));

        Assert.Equal(new[] { "fcm.conta_servico_invalida" }, ex.Erros.Keys);
        protetor.DidNotReceiveWithAnyArgs().Proteger(default!);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Gravar_fcm_tira_o_project_id_do_json_e_mantem_sem_segredo_novo()
    {
        await using var db = _fixture.CriarDbContext();
        var protetor = Substitute.For<IProtetorSegredos>();
        protetor.Proteger(Arg.Any<string>()).Returns(c => "cifrado:" + c.Arg<string>().Length);
        var servico = new IntegracaoCredencialService(db, protetor, new UsuarioAtualAccessorFake());

        await servico.AtualizarAsync("fcm",
            new AtualizarIntegracaoCredencialRequest(null, ContaJson, null, "{\"projectId\":\"outro\"}", true));
        var gravada = await servico.ObterAsync("fcm");
        Assert.True(gravada.ClientSecretDefinido);
        Assert.Equal("projeto-teste", ProjectIdGravado(gravada));

        // Só ligar/desligar (sem colar o JSON de novo): segredo e projeto continuam.
        await servico.AtualizarAsync("fcm", new AtualizarIntegracaoCredencialRequest(null, null, null, null, false));
        var desligada = await servico.ObterAsync("fcm");
        Assert.True(desligada.ClientSecretDefinido);
        Assert.False(desligada.Ativo);
        Assert.Equal("projeto-teste", ProjectIdGravado(desligada));
    }

    // A coluna é jsonb: o Postgres devolve o JSON reformatado, então compara o valor, não o texto.
    private static string? ProjectIdGravado(IntegracaoCredencialDto c)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(c.ParametrosJson!);
        return doc.RootElement.GetProperty("projectId").GetString();
    }
}
