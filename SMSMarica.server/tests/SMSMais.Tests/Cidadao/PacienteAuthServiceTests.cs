using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Dtos;
using SMSMarica.Core.Integracoes.Proxy;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Core.Telefones;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMais.Tests.Cidadao;

/// <summary>
/// Travas do login do cidadão no PWA. O que está sob teste é a régua de segurança: código só
/// sai para contato VERIFICADO, e quem não tem verificação precisa provar quem é antes de
/// receber qualquer coisa. Nenhum caso aqui toca o banco (as travas do nascimento/Receita
/// decidem antes) — a conferência do nº da solicitação, que consulta <c>solicitacao</c>, é
/// exercitada nos testes de integração.
/// </summary>
public sealed class PacienteAuthServiceTests
{
    private const string Cpf = "52998224725";
    private static readonly DateOnly Nascimento = new(1980, 3, 10);

    private readonly IPacientesService _pacientes = Substitute.For<IPacientesService>();
    private readonly ICidadaoSessaoService _sessoes = Substitute.For<ICidadaoSessaoService>();
    private readonly IWhatsAppCliente _whatsapp = Substitute.For<IWhatsAppCliente>();
    private readonly ITelefoneValidacaoService _telefones = Substitute.For<ITelefoneValidacaoService>();
    private readonly IConsultaCpfService _receita = Substitute.For<IConsultaCpfService>();

    private PacienteAuthService CriarServico(bool modoTeste = true)
    {
        // Conexão nunca é aberta: os casos deste arquivo não chegam a consultar solicitações.
        var db = new SmsMaricaDbContext(new DbContextOptionsBuilder<SmsMaricaDbContext>()
            .UseNpgsql("Host=localhost;Database=nao-usado")
            .Options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tfd:Otp:ModoTeste"] = modoTeste ? "true" : "false",
            })
            .Build();

        _whatsapp
            .EnviarTemplateAutenticacaoAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new EnvioWhatsAppResultado(true, "wamid.123", null));

        _sessoes
            .AbrirSessaoAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(("token-jwt", DateTime.UtcNow.AddDays(1)));

        return new PacienteAuthService(
            db, _pacientes, _sessoes, _whatsapp, new MemoryCache(new MemoryCacheOptions()),
            config, _telefones, _receita, NullLogger<PacienteAuthService>.Instance);
    }

    [Fact]
    public async Task SolicitarOtp_SemContatoVerificado_NaoEnviaCodigoEPedeVerificacao()
    {
        var id = Guid.NewGuid();
        _pacientes.ObterPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(id, "MARIA DA SILVA", Cpf, Ativo: true));
        _pacientes.ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Paciente(id, telefoneCelular: "21999990000", telefoneVerificado: null));

        var r = await CriarServico().SolicitarOtpAsync(new SolicitarOtpRequest(Cpf));

        r.Situacao.Should().Be(SituacaoLoginCidadao.Verificacao);
        r.Enviado.Should().BeFalse();
        r.CodigoTeste.Should().BeNull();
        // Dica dos últimos 4 dígitos do número do cadastro — sem revelar o número inteiro.
        r.TelefoneMascarado.Should().Be("***-0000");
        await _whatsapp.DidNotReceiveWithAnyArgs().EnviarTemplateAutenticacaoAsync(
            default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task SolicitarOtp_CpfSemCadastro_PedeCadastroSemVazarCodigo()
    {
        _pacientes.ObterPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);

        var r = await CriarServico().SolicitarOtpAsync(new SolicitarOtpRequest(Cpf));

        r.Situacao.Should().Be(SituacaoLoginCidadao.Cadastro);
        r.Enviado.Should().BeFalse();
        await _whatsapp.DidNotReceiveWithAnyArgs().EnviarTemplateAutenticacaoAsync(
            default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task SolicitarOtp_ContatoVerificado_EnviaParaONumeroVerificado()
    {
        var id = Guid.NewGuid();
        _pacientes.ObterPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(id, "MARIA DA SILVA", Cpf, Ativo: true));
        _pacientes.ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Paciente(
                id,
                telefoneCelular: "21988887777",       // não verificado: NÃO pode ser o destino
                telefoneVerificado: "5521999991234"));

        var r = await CriarServico(modoTeste: false).SolicitarOtpAsync(new SolicitarOtpRequest(Cpf));

        r.Situacao.Should().Be(SituacaoLoginCidadao.Otp);
        r.Enviado.Should().BeTrue();
        r.Canal.Should().Be("whatsapp");
        r.TelefoneMascarado.Should().Be("***-1234");
        await _whatsapp.Received(1).EnviarTemplateAutenticacaoAsync(
            "5521999991234", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SolicitarOtpVerificacao_NascimentoNaoConfere_Recusa()
    {
        var id = Guid.NewGuid();
        _pacientes.ObterPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(id, "MARIA DA SILVA", Cpf, Ativo: true));
        _pacientes.ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Paciente(id, nascimento: Nascimento));

        var acao = () => CriarServico().SolicitarOtpVerificacaoAsync(
            new SolicitarOtpVerificacaoRequest(Cpf, new DateOnly(1981, 3, 10), "998877", "21999990000"));

        (await acao.Should().ThrowAsync<ValidacaoException>())
            .Which.Erros.Should().ContainKey("nascimento.nao_confere");
        await _whatsapp.DidNotReceiveWithAnyArgs().EnviarTemplateAutenticacaoAsync(
            default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task SolicitarOtpVerificacao_CadastroNovo_ReceitaNega_NaoEnvia()
    {
        _pacientes.ObterPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);
        _receita.ConsultarCpfAsync(Cpf, Nascimento, Arg.Any<CancellationToken>())
            .Throws(new ValidacaoException("proxy.cpf.nao_encontrado", "CPF não confere na Receita."));

        var acao = () => CriarServico().SolicitarOtpVerificacaoAsync(
            new SolicitarOtpVerificacaoRequest(Cpf, Nascimento, null, "21999990000"));

        (await acao.Should().ThrowAsync<ValidacaoException>())
            .Which.Erros.Should().ContainKey("identidade.nao_confere");
        await _whatsapp.DidNotReceiveWithAnyArgs().EnviarTemplateAutenticacaoAsync(
            default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task CadastroNovo_SoNasceDepoisDoCodigoConfirmado_ComTelefoneVerificado()
    {
        var novoId = Guid.NewGuid();
        _pacientes.ObterPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);
        _receita.ConsultarCpfAsync(Cpf, Nascimento, Arg.Any<CancellationToken>())
            .Returns(new HubCpfRespostaDto(Cpf, "MARIA DA SILVA", "10/03/1980", "REGULAR", "Feminino"));
        _pacientes.CadastrarAsync(Arg.Any<CadastrarPacienteRequest>(), Arg.Any<CancellationToken>())
            .Returns(novoId);

        var servico = CriarServico();
        var emitido = await servico.SolicitarOtpVerificacaoAsync(
            new SolicitarOtpVerificacaoRequest(Cpf, Nascimento, null, "(21) 99999-0000"));

        emitido.CodigoTeste.Should().NotBeNullOrEmpty();
        // Nada de cadastro órfão: até aqui o telefone é só uma alegação.
        await _pacientes.DidNotReceiveWithAnyArgs().CadastrarAsync(default!, default);
        await _telefones.Received(1).GarantirNumeroLivreAsync(
            Cpf, "5521999990000", Arg.Any<CancellationToken>());

        var login = await servico.ValidarOtpAsync(
            new ValidarOtpRequest(Cpf, emitido.CodigoTeste!), "PWA", "127.0.0.1");

        login.Token.Should().Be("token-jwt");
        login.Paciente.Id.Should().Be(novoId);
        login.Paciente.Nome.Should().Be("MARIA DA SILVA");
        await _pacientes.Received(1).CadastrarAsync(
            Arg.Is<CadastrarPacienteRequest>(r =>
                r.Cpf == Cpf
                && r.NomeCompleto == "MARIA DA SILVA"
                && r.DataNascimento == Nascimento
                && r.Sexo == Sexo.Feminino
                && r.TelefonePrincipal == "21999990000"),
            Arg.Any<CancellationToken>());
        // O número que recebeu o código é o que vira verificado.
        await _telefones.Received(1).MarcarValidadoAsync(
            Cpf, "5521999990000", "pwa-cidadao", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidarOtp_CodigoErrado_NaoAbreSessaoNemCriaCadastro()
    {
        _pacientes.ObterPorCpfAsync(Cpf, Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);
        _receita.ConsultarCpfAsync(Cpf, Nascimento, Arg.Any<CancellationToken>())
            .Returns(new HubCpfRespostaDto(Cpf, "MARIA DA SILVA", "10/03/1980", "REGULAR", "Feminino"));

        var servico = CriarServico();
        var emitido = await servico.SolicitarOtpVerificacaoAsync(
            new SolicitarOtpVerificacaoRequest(Cpf, Nascimento, null, "21999990000"));
        var errado = emitido.CodigoTeste == "000000" ? "111111" : "000000";

        var acao = () => servico.ValidarOtpAsync(new ValidarOtpRequest(Cpf, errado), null, null);

        (await acao.Should().ThrowAsync<ValidacaoException>())
            .Which.Erros.Should().ContainKey("otp.invalido");
        await _pacientes.DidNotReceiveWithAnyArgs().CadastrarAsync(default!, default);
        await _sessoes.DidNotReceiveWithAnyArgs().AbrirSessaoAsync(
            default, default!, default!, default!, default, default, default);
    }

    private static PacienteDto Paciente(
        Guid id,
        string? telefoneCelular = null,
        string? telefoneVerificado = null,
        DateOnly? nascimento = null) =>
        new(
            Id: id,
            NomeCompleto: "MARIA DA SILVA",
            Cpf: Cpf,
            Cns: null,
            Latitude: 0,
            Longitude: 0,
            Ativo: true,
            CadastradoEm: DateTime.UtcNow,
            Rg: null,
            DataNascimento: nascimento,
            Sexo: Sexo.Feminino,
            EstadoCivil: EstadoCivil.NaoInformado,
            RacaCor: RacaCor.NaoInformado,
            Escolaridade: Escolaridade.NaoInformado,
            Ocupacao: null,
            Naturalidade: null,
            Nacionalidade: "Brasileira",
            NomeDaMae: null,
            NomeDoPai: null,
            ResponsavelLegal: null,
            Endereco: null,
            TelefonePrincipal: null,
            TelefoneCelular: telefoneCelular,
            TelefoneResidencial: null,
            Email: null,
            ContatoEmergencia: null,
            AlturaCm: null,
            PesoKg: null,
            TipoSanguineo: TipoSanguineo.NaoInformado,
            FatorRh: FatorRh.NaoInformado,
            Alergias: [],
            MedicamentosContinuos: [],
            Comorbidades: [],
            Deficiencias: [],
            PlanoSaude: null,
            Observacoes: null,
            FotoBase64: null,
            TelefoneVerificado: telefoneVerificado);
}
