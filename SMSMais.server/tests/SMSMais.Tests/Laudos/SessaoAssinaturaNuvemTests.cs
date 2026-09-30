using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Core.Associacoes;
using SMSMais.Core.Associacoes.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Laudos.Assinatura.Dtos;
using SMSMais.Core.Laudos.Assinatura.Nuvem;
using SMSMais.Core.Laudos.Pdf;
using SMSMais.Core.Laudos.Verificacao;
using SMSMais.Core.Medicos;
using SMSMais.Core.Medicos.Assinatura;
using SMSMais.Core.Medicos.Fhir;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;
using Task = System.Threading.Tasks.Task;

namespace SMSMais.Tests.Laudos;

/// <summary>
/// ADR-0061 §2.1 — sessão VIDaaS do médico: aprova uma vez no aplicativo e assina os laudos
/// seguintes sem voltar a ele, enquanto durar o LOGIN no SMSMais. Tudo o que é externo (IntegraICP,
/// Assinador, hub FHIR, PDF) é dublê; a assinatura RAW é feita de verdade com uma chave RSA de
/// teste, para a conferência PKCS#1 do serviço passar como passaria com o provedor.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SessaoAssinaturaNuvemTests(PostgresFixture fixture)
{
    private const string Cpf = "52998224725";

    private readonly Guid _medicoId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly RSA _chave = RSA.Create(2048);
    private readonly IIntegraIcpClient _integra = Substitute.For<IIntegraIcpClient>();
    private readonly List<string> _statesEmitidos = [];
    private readonly List<int> _vidasPedidas = [];
    private readonly IAssinaturaMedicoService _assinaturaMedico = Substitute.For<IAssinaturaMedicoService>();

    // Os cenários de sessão partem de quem já escolheu manter; os do modal trocam isto.
    private PreferenciaSessaoNuvem _preferencia = PreferenciaSessaoNuvem.Manter;

    [Fact]
    public async Task Primeira_aprovacao_abre_a_sessao_e_o_laudo_seguinte_nao_volta_ao_app()
    {
        var (laudoA, laudoB) = await CriarDoisLaudosAsync();
        ConfigurarIntegraIcp();
        var login = Login("login-a", horasRestantes: 8);

        // 1º laudo: sem sessão → URL de autorização, com vida = o que resta do login.
        var r1 = await Servico(login).IniciarAsync(laudoA, _usuarioId, null);
        r1.UrlAutorizacao.Should().NotBeNull();
        _vidasPedidas.Should().ContainSingle().Which.Should().BeInRange(8 * 3600 - 120, 8 * 3600);

        // Retorno anônimo do provedor (outro escopo, sem login).
        await Servico(Login(null, null)).ConcluirNuvemAsync(_statesEmitidos.Single(), "CRED-1");

        await using (var db = fixture.CriarDbContext())
        {
            (await StatusDoJobAsync(db, laudoA)).Should().Be(StatusAssinatura.AguardandoAprovacao);
            var sessao = await db.SessoesAssinaturaNuvem.SingleAsync(s => s.MedicoId == _medicoId);
            sessao.SessaoLoginId.Should().Be("login-a");
            sessao.CredencialId.Should().Be("CRED-1");
            sessao.CodeVerifier.Should().NotBeNull();
            sessao.EncerradaEm.Should().BeNull();
            sessao.ExpiraEm.Should().BeCloseTo(DateTime.UtcNow.AddHours(8), TimeSpan.FromMinutes(3));
        }

        // 2º laudo, mesmo login: assina direto, sem nova autorização.
        var r2 = await Servico(login).IniciarAsync(laudoB, _usuarioId, null);
        r2.UrlAutorizacao.Should().BeNull();
        await _integra.Received(1).IniciarAutorizacaoAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        await using (var db = fixture.CriarDbContext())
        {
            var jobB = await db.LaudoAssinaturas.SingleAsync(a => a.LaudoId == laudoB);
            jobB.Status.Should().Be(StatusAssinatura.AguardandoAprovacao);
            jobB.NuvemSessaoId.Should().NotBeNull();
            jobB.SessaoLoginId.Should().Be("login-a");
        }

        (await Servico(login).ObterSessaoNuvemAsync(_usuarioId)).Ativa.Should().BeTrue();
    }

    [Fact]
    public async Task Outro_login_nao_herda_a_sessao_e_o_logout_a_encerra()
    {
        var (laudoA, laudoB) = await CriarDoisLaudosAsync();
        ConfigurarIntegraIcp();
        var loginA = Login("login-a", horasRestantes: 8);
        await Servico(loginA).IniciarAsync(laudoA, _usuarioId, null);
        await Servico(Login(null, null)).ConcluirNuvemAsync(_statesEmitidos.Single(), "CRED-1");

        // Saiu e entrou de novo (jti novo): a sessão do login anterior não vale.
        var loginB = Login("login-b", horasRestantes: 8);
        (await Servico(loginB).ObterSessaoNuvemAsync(_usuarioId)).Ativa.Should().BeFalse();
        var r = await Servico(loginB).IniciarAsync(laudoB, _usuarioId, null);
        r.UrlAutorizacao.Should().NotBeNull();

        // Logout do login A: o verificador some, a credencial não assina mais.
        await Servico(loginA).EncerrarSessaoNuvemAsync(_usuarioId);
        await using var db = fixture.CriarDbContext();
        var sessao = await db.SessoesAssinaturaNuvem.SingleAsync(s => s.MedicoId == _medicoId);
        sessao.EncerradaEm.Should().NotBeNull();
        sessao.CodeVerifier.Should().BeNull();
        sessao.EncerradaPorUsuarioId.Should().Be(_usuarioId);
        (await Servico(loginA).ObterSessaoNuvemAsync(_usuarioId)).Ativa.Should().BeFalse();
    }

    [Fact]
    public async Task Credencial_recusada_pelo_provedor_pede_nova_aprovacao_sem_falhar_o_laudo()
    {
        var (laudoA, laudoB) = await CriarDoisLaudosAsync();
        ConfigurarIntegraIcp();
        var login = Login("login-a", horasRestantes: 8);
        await Servico(login).IniciarAsync(laudoA, _usuarioId, null);
        await Servico(Login(null, null)).ConcluirNuvemAsync(_statesEmitidos.Single(), "CRED-1");

        // Revogada no app antes do previsto: o provedor responde 403.
        _integra.ObterCertificadoAsync("CRED-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflitoException(IntegraIcpClient.CodigoCredencialInvalida, "expirada"));

        var r = await Servico(login).IniciarAsync(laudoB, _usuarioId, null);
        r.UrlAutorizacao.Should().NotBeNull();

        await using var db = fixture.CriarDbContext();
        var sessao = await db.SessoesAssinaturaNuvem.SingleAsync(s => s.MedicoId == _medicoId);
        sessao.MotivoEncerramento.Should().Be("recusada pelo provedor");
        sessao.CodeVerifier.Should().BeNull();
        var jobB = await db.LaudoAssinaturas.SingleAsync(a => a.LaudoId == laudoB);
        jobB.Status.Should().Be(StatusAssinatura.Iniciada);
        jobB.NuvemStateHash.Should().NotBeNull();
        jobB.NuvemSessaoId.Should().BeNull();
    }

    [Fact]
    public async Task Sessao_nunca_passa_do_fim_do_login()
    {
        var (laudoA, _) = await CriarDoisLaudosAsync();
        ConfigurarIntegraIcp();

        // Login com 40 min restantes e teto de 12 h: a autorização vale 40 min.
        await Servico(Login("login-curto", horasRestantes: 40 / 60d)).IniciarAsync(laudoA, _usuarioId, null);
        _vidasPedidas.Should().ContainSingle().Which.Should().BeInRange(40 * 60 - 120, 40 * 60);
    }

    // ---------------- modal "Manter a autorização?" ----------------

    [Fact]
    public async Task Perguntar_e_responder_so_este_laudo_nao_abre_sessao()
    {
        _preferencia = PreferenciaSessaoNuvem.Perguntar;
        var (laudoA, _) = await CriarDoisLaudosAsync();
        ConfigurarIntegraIcp();

        var r = await Servico(Login("login-a", 8)).IniciarAsync(
            laudoA, _usuarioId, null, new EscolhaSessaoNuvemDto(Manter: false, NaoPerguntarDeNovo: false));
        r.UrlAutorizacao.Should().NotBeNull();
        _vidasPedidas.Should().ContainSingle().Which.Should().Be(IntegraIcpOptions.CredencialVidaUmLaudoSegundos);

        await Servico(Login(null, null)).ConcluirNuvemAsync(_statesEmitidos.Single(), "CRED-1");
        await using var db = fixture.CriarDbContext();
        (await StatusDoJobAsync(db, laudoA)).Should().Be(StatusAssinatura.AguardandoAprovacao);
        (await db.SessoesAssinaturaNuvem.AnyAsync(s => s.MedicoId == _medicoId)).Should().BeFalse();
        await _assinaturaMedico.DidNotReceiveWithAnyArgs().DefinirSessaoNuvemAsync(default, default);
    }

    [Theory]
    [InlineData(true, PreferenciaSessaoNuvem.Manter)]
    [InlineData(false, PreferenciaSessaoNuvem.CadaLaudo)]
    public async Task Nao_perguntar_de_novo_grava_a_resposta_como_preferencia(bool manter, PreferenciaSessaoNuvem gravada)
    {
        _preferencia = PreferenciaSessaoNuvem.Perguntar;
        var (laudoA, _) = await CriarDoisLaudosAsync();
        ConfigurarIntegraIcp();

        await Servico(Login("login-a", 8)).IniciarAsync(
            laudoA, _usuarioId, null, new EscolhaSessaoNuvemDto(manter, NaoPerguntarDeNovo: true));

        await _assinaturaMedico.Received(1).DefinirSessaoNuvemAsync(_medicoId, gravada, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aprovar_cada_laudo_ignora_a_resposta_e_a_sessao_que_sobrou()
    {
        var (laudoA, laudoB) = await CriarDoisLaudosAsync();
        ConfigurarIntegraIcp();
        var login = Login("login-a", 8);
        await Servico(login).IniciarAsync(laudoA, _usuarioId, null);
        await Servico(Login(null, null)).ConcluirNuvemAsync(_statesEmitidos.Single(), "CRED-1");

        // O administrador trocou para "aprovar cada laudo" com a sessão ainda aberta.
        _preferencia = PreferenciaSessaoNuvem.CadaLaudo;
        (await Servico(login).ObterSessaoNuvemAsync(_usuarioId)).Ativa.Should().BeFalse();
        var r = await Servico(login).IniciarAsync(
            laudoB, _usuarioId, null, new EscolhaSessaoNuvemDto(Manter: true, NaoPerguntarDeNovo: false));

        r.UrlAutorizacao.Should().NotBeNull();
        _vidasPedidas.Last().Should().Be(IntegraIcpOptions.CredencialVidaUmLaudoSegundos);
    }

    // ---------------- montagem ----------------

    private static UsuarioAtualAccessorFake Login(string? jti, double? horasRestantes) =>
        new(sessaoId: jti ?? string.Empty,
            sessaoExpiraEm: horasRestantes is { } h ? DateTime.UtcNow.AddHours(h) : null);

    private LaudoAssinaturaService Servico(UsuarioAtualAccessorFake login)
    {
        var db = fixture.CriarDbContext();

        var practitioner = new Practitioner
        {
            Id = _medicoId.ToString(),
            Identifier = [new Identifier("https://fhir.saude.gov.br/sid/cpf", Cpf)],
            Name = [new HumanName { Text = "DRA TESTE" }],
        };
        var fhir = Substitute.For<IPractitionerFhirClient>();
        fhir.ObterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(practitioner);

        _assinaturaMedico.ObterAsync(_medicoId, Arg.Any<CancellationToken>())
            .Returns(new AssinaturaMedicoDto(_medicoId, Convert.ToBase64String([1, 2, 3]), "image/png",
                FormatoAssinaturaMedico.Quadrada, DateTime.UtcNow));
        _assinaturaMedico.ObterModoAsync(_medicoId, Arg.Any<CancellationToken>())
            .Returns(_ => new ModoAssinaturaMedicoDto(_medicoId, ModoAssinaturaMedico.Nuvem, true, DateTime.UtcNow, _preferencia));

        var associacao = Substitute.For<IExameAssociacaoService>();
        associacao.ResolverVinculoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VinculoExame(Guid.NewGuid(), Guid.NewGuid()));

        var verificacao = Substitute.For<ILaudoVerificacaoService>();
        verificacao.ObterSeloAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new SeloVerificacaoLaudo(Guid.NewGuid(), "https://x/v", [1], true));
        var pdf = Substitute.For<ILaudoPdfRenderer>();
        pdf.GerarOficialAsync(Arg.Any<Guid>(), Arg.Any<SeloVerificacaoLaudo>(), Arg.Any<CancellationToken>())
            .Returns("%PDF-base"u8.ToArray());
        var carimbo = Substitute.For<ICarimboAssinaturaRenderer>();
        carimbo.Renderizar(Arg.Any<CarimboDados>()).Returns([0x89, 0x50]);

        // Assinador: devolve um hash aleatório para assinar e "embute" qualquer RAW, dizendo que
        // o titular é o CPF do médico (a trava de autoria roda de verdade no serviço).
        var assinador = Substitute.For<IAssinadorPdfPades>();
        assinador.PrepararAsync(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<byte[]>>(), Arg.Any<DadosVisualAssinatura>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new PreparacaoAssinatura(RandomNumberGenerator.GetBytes(32), "SHA-256", [9]));
        assinador.ConcluirAsync(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoAssinatura("%PDF-assinado"u8.ToArray(), "PAdES_AD_RB", "DRA TESTE:" + Cpf, "AC TESTE", Cpf));

        var cadeia = Substitute.For<ICadeiaIcpBrasil>();
        cadeia.MontarAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyList<byte[]>)[c.Arg<byte[]>()]);

        // Proteção reversível e legível: o teste só precisa que ida e volta batam.
        var protetor = Substitute.For<IProtetorSegredos>();
        protetor.Proteger(Arg.Any<string>()).Returns(c => "cif:" + c.Arg<string>());
        protetor.Revelar(Arg.Any<string>()).Returns(c => c.Arg<string>()[4..]);

        return new LaudoAssinaturaService(
            db, pdf, assinador, fhir, _assinaturaMedico, Substitute.For<IMedicosService>(), associacao, carimbo,
            new Lazy<IComunicacaoPacienteService>(() => Substitute.For<IComunicacaoPacienteService>()),
            verificacao, _integra, cadeia, protetor, login,
            new ConfigurationBuilder().Build(), NullLogger<LaudoAssinaturaService>.Instance,
            Options.Create(new AssinaturaOptions()),
            Options.Create(new IntegraIcpOptions { Canal = "canal-teste" }));
    }

    /// <summary>
    /// IntegraICP de mentira: guarda o state e a vida de cada autorização, devolve um certificado
    /// autoassinado e assina o hash com a chave dele (PKCS#1 v1.5/SHA-256, como o provedor real).
    /// </summary>
    private void ConfigurarIntegraIcp()
    {
        var req = new CertificateRequest("CN=DRA TESTE:" + Cpf, _chave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var der = cert.Export(X509ContentType.Cert);

        _integra.IniciarAutorizacaoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                var url = c.ArgAt<string>(1);
                _statesEmitidos.Add(Uri.UnescapeDataString(url[(url.IndexOf("state=", StringComparison.Ordinal) + 6)..]));
                _vidasPedidas.Add(c.ArgAt<int>(3));
                return "https://psc.teste/autorizar";
            });
        _integra.ObterCertificadoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(der);
        _integra.AssinarHashAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(c => _chave.SignHash(c.ArgAt<byte[]>(2), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    private async Task<(Guid A, Guid B)> CriarDoisLaudosAsync()
    {
        await using var db = fixture.CriarDbContext();
        Laudo Novo() => new()
        {
            Id = Guid.NewGuid(),
            StudyInstanceUID = "1.2.826.0.1." + Random.Shared.NextInt64(1, long.MaxValue),
            Versao = 1,
            MedicoId = _medicoId,
            MedicoNomeSnapshot = "DRA TESTE",
            MedicoCrmSnapshot = "000000",
            MedicoUfCrmSnapshot = "RJ",
            MedicoRqeSnapshot = "123",
            Titulo = "RX de tórax",
            ConteudoHtml = "<p>laudo de teste</p>",
            Status = StatusLaudo.Finalizado,
            FinalizadoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        var a = Novo();
        var b = Novo();
        db.Laudos.AddRange(a, b);
        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    private static async Task<StatusAssinatura> StatusDoJobAsync(SmsMaisDbContext db, Guid laudoId) =>
        (await db.LaudoAssinaturas.SingleAsync(a => a.LaudoId == laudoId)).Status;
}
