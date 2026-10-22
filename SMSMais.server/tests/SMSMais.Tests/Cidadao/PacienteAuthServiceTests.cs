using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Cidadao.Entrada;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Dtos;
using SMSMais.Core.Integracoes.Proxy;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Sandbox;
using SMSMais.Core.Telefones;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;
using Task = System.Threading.Tasks.Task;

namespace SMSMais.Tests.Cidadao;

/// <summary>
/// As travas da entrada no app do cidadão (ADR-0078), contra Postgres real (o freio de tentativas mora
/// no banco). Sob teste: CPF + nascimento conferidos antes de qualquer número aparecer, a mesma resposta
/// para todo "não confere", o bloqueio do CPF depois de 5 erros, o código só para um número da lista —
/// nunca digitado —, quem tem verificado só vê o verificado, e o e-SUS só quando o canal permite.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PacienteAuthServiceTests(PostgresFixture fixture)
{
    private const string Cpf = "52998224725";
    private static readonly DateOnly Nascimento = new(1980, 3, 10);

    /// <summary>CPF próprio de cada teste: o freio de tentativas é por CPF e o banco é compartilhado.</summary>
    private readonly string _cpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    private readonly IPacientesService _pacientes = Substitute.For<IPacientesService>();
    private readonly ICidadaoSessaoService _sessoes = Substitute.For<ICidadaoSessaoService>();
    private readonly IWhatsAppCliente _whatsapp = Substitute.For<IWhatsAppCliente>();
    private readonly ITelefoneValidacaoService _telefones = Substitute.For<ITelefoneValidacaoService>();
    private readonly IConsultaCpfService _receita = Substitute.For<IConsultaCpfService>();
    private readonly IPersonificacaoPacienteService _personificacoes = Substitute.For<IPersonificacaoPacienteService>();
    private readonly IPacienteFhirClient _fhir = Substitute.For<IPacienteFhirClient>();
    private readonly IConfiguracaoAppCidadaoService _configuracao = Substitute.For<IConfiguracaoAppCidadaoService>();
    private readonly ITelefonesEsusNaEntrada _esus = Substitute.For<ITelefonesEsusNaEntrada>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private PacienteAuthService CriarServico(bool modoTeste = true)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tfd:Otp:ModoTeste"] = modoTeste ? "true" : "false",
            })
            .Build();

        _whatsapp
            .EnviarTemplateAutenticacaoAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Guid?>(), Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>())
            .Returns(new EnvioWhatsAppResultado(true, "wamid.123", null));

        _sessoes
            .AbrirSessaoAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(("token-jwt", DateTime.UtcNow.AddDays(1)));

        return new PacienteAuthService(
            fixture.CriarDbContext(), _pacientes, _sessoes, _whatsapp, _cache,
            config, _telefones, _receita, _personificacoes, _fhir, _configuracao, _esus,
            NullLogger<PacienteAuthService>.Instance);
    }

    /// <summary>Paciente com ficha: nascimento, verificado e os telefones do Patient (FHIR).</summary>
    private Guid ComFicha(string? telefoneVerificado = null, params ContactPoint[] telefones)
    {
        var id = Guid.NewGuid();
        _pacientes.ObterPorCpfAsync(_cpf, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(id, "MARIA DA SILVA", _cpf, Ativo: true));
        _pacientes.ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Paciente(id, nascimento: Nascimento, telefoneVerificado: telefoneVerificado, cpf: _cpf));
        _fhir.ObterAsync(id, Arg.Any<CancellationToken>()).Returns(new Patient { Telecom = [.. telefones] });
        return id;
    }

    private static ContactPoint Celular(string numero, int? rank = null) =>
        new(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Mobile, numero) { Rank = rank };

    private Task<EntradaCidadaoDto> Entrar(PacienteAuthService s, DateOnly? nascimento = null, string? canal = null) =>
        s.IniciarEntradaAsync(new IniciarEntradaRequest(_cpf, nascimento ?? Nascimento), canal);

    private async Task<int?> FalhasNoBanco()
    {
        await using var db = fixture.CriarDbContext();
        return (await db.CidadaoEntradaBloqueios.AsNoTracking().FirstOrDefaultAsync(b => b.Cpf == _cpf))?.Falhas;
    }

    private async Task NenhumCodigoSaiu() =>
        await _whatsapp.DidNotReceiveWithAnyArgs().EnviarTemplateAutenticacaoAsync(
            default!, default!, default!, default!, default, default, default);

    // ---------- CPF + nascimento ----------

    [Fact]
    public async Task Nascimento_errado_recusa_com_a_mensagem_unica_e_conta_a_tentativa()
    {
        ComFicha(telefones: Celular("21988887777"));

        var acao = () => Entrar(CriarServico(), new DateOnly(1981, 3, 10));

        (await acao.Should().ThrowAsync<ValidacaoException>())
            .Which.Erros.Should().ContainKey("entrada.nao_confere");
        (await FalhasNoBanco()).Should().Be(1);
        await NenhumCodigoSaiu();
    }

    [Fact]
    public async Task Cinco_erros_bloqueiam_o_cpf_ate_com_a_data_certa()
    {
        ComFicha(telefones: Celular("21988887777"));
        var servico = CriarServico();

        for (var i = 0; i < PacienteAuthService.FalhasAteBloquear; i++)
        {
            var errada = () => Entrar(servico, new DateOnly(1990, 1, 1 + i));
            (await errada.Should().ThrowAsync<ValidacaoException>())
                .Which.Erros.Should().ContainKey("entrada.nao_confere");
        }

        var certa = () => Entrar(servico);
        (await certa.Should().ThrowAsync<ValidacaoException>())
            .Which.Erros.Should().ContainKey("entrada.bloqueada");
    }

    [Fact]
    public async Task Acertar_depois_de_errar_zera_a_contagem()
    {
        ComFicha(telefones: Celular("21988887777"));
        var servico = CriarServico();
        var errada = () => Entrar(servico, new DateOnly(1990, 1, 1));
        await errada.Should().ThrowAsync<ValidacaoException>();
        (await FalhasNoBanco()).Should().Be(1);

        await Entrar(servico);

        (await FalhasNoBanco()).Should().BeNull();
    }

    [Fact]
    public async Task Sem_cadastro_a_receita_confere_e_a_negativa_tem_a_mesma_mensagem()
    {
        _pacientes.ObterPorCpfAsync(_cpf, Arg.Any<CancellationToken>()).Returns((PacienteExistenciaDto?)null);
        _receita.ConsultarCpfAsync(_cpf, Nascimento, Arg.Any<CancellationToken>())
            .Throws(new ValidacaoException("proxy.cpf.nao_encontrado", "CPF não confere na Receita."));

        var acao = () => Entrar(CriarServico());

        (await acao.Should().ThrowAsync<ValidacaoException>())
            .Which.Erros.Should().ContainKey("entrada.nao_confere");
        (await FalhasNoBanco()).Should().Be(1);
    }

    // ---------- os números da lista ----------

    [Fact]
    public async Task Com_verificado_so_o_verificado_aparece_e_o_esus_nao()
    {
        ComFicha("5521999991234", Celular("21988887777"));
        _configuracao.ConsultarEsusNoCanalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _esus.DisponivelAsync(Arg.Any<CancellationToken>()).Returns(true);

        var r = await Entrar(CriarServico());

        var unica = r.Opcoes.Should().ContainSingle().Subject;
        unica.Final.Should().Be("1234");
        unica.Verificado.Should().BeTrue();
        r.PodeBuscarEsus.Should().BeFalse();
    }

    [Fact]
    public async Task Sem_verificado_lista_os_celulares_da_ficha_sem_fixo_negado_repetido_ou_de_outro_cpf()
    {
        var negado = Celular("21977776666");
        negado.AddExtension(PatientMergeFhir.ExtContatoNegado, new FhirBoolean(true));
        ComFicha(null,
            Celular("21966665555", rank: 2),
            Celular("21988887777", rank: 1),
            new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Home, "2126330000"),
            negado,
            Celular("(21) 98888-7777"),
            Celular("21955554444"));
        _telefones.GarantirNumeroLivreAsync(_cpf, "5521955554444", Arg.Any<CancellationToken>())
            .Throws(new ConflitoException("telefone.duplicado", "Número de outra pessoa."));

        var r = await Entrar(CriarServico());

        r.Opcoes.Select(o => o.Final).Should().Equal("7777", "5555");
        r.Opcoes.Should().OnlyContain(o => o.Origem == "cadastro" && !o.Verificado);
    }

    [Fact]
    public async Task O_codigo_vai_para_o_numero_escolhido_e_e_ele_que_vira_verificado()
    {
        var id = ComFicha(null, Celular("21988887777"), Celular("21966665555"));
        var servico = CriarServico(modoTeste: false);
        var entrada = await Entrar(servico);
        var escolhida = entrada.Opcoes.Single(o => o.Final == "5555");

        var emitido = await servico.EnviarCodigoEntradaAsync(entrada.DesafioId, new EnviarCodigoEntradaRequest(escolhida.Id));

        emitido.Enviado.Should().BeTrue();
        emitido.TelefoneMascarado.Should().Be("***-5555");
        await _whatsapp.Received(1).EnviarTemplateAutenticacaoAsync(
            "5521966665555", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            id, Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>());
        await _whatsapp.Received(1).EnviarTemplateAutenticacaoAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>());
    }

    [Fact]
    public async Task Opcao_que_nao_e_da_lista_ou_desafio_vencido_nao_mandam_nada()
    {
        ComFicha(null, Celular("21988887777"));
        var servico = CriarServico();
        var entrada = await Entrar(servico);

        var forDaLista = () => servico.EnviarCodigoEntradaAsync(entrada.DesafioId, new EnviarCodigoEntradaRequest("99"));
        (await forDaLista.Should().ThrowAsync<ValidacaoException>())
            .Which.Erros.Should().ContainKey("entrada.opcao_invalida");

        var vencido = () => servico.EnviarCodigoEntradaAsync("nao-existe", new EnviarCodigoEntradaRequest("1"));
        (await vencido.Should().ThrowAsync<ValidacaoException>())
            .Which.Erros.Should().ContainKey("entrada.expirada");
        await NenhumCodigoSaiu();
    }

    [Fact]
    public async Task No_maximo_tres_codigos_por_entrada()
    {
        ComFicha(null, Celular("21988887777"));
        var servico = CriarServico();
        var entrada = await Entrar(servico);
        var opcao = entrada.Opcoes.Single().Id;

        for (var i = 0; i < 3; i++)
            await servico.EnviarCodigoEntradaAsync(entrada.DesafioId, new EnviarCodigoEntradaRequest(opcao));

        var quarto = () => servico.EnviarCodigoEntradaAsync(entrada.DesafioId, new EnviarCodigoEntradaRequest(opcao));
        (await quarto.Should().ThrowAsync<ValidacaoException>())
            .Which.Erros.Should().ContainKey("entrada.muitos_envios");
    }

    // ---------- e-SUS ----------

    [Theory]
    [InlineData("nativo", true)]
    [InlineData("pwa", false)]
    [InlineData(null, false)]
    public async Task O_esus_so_aparece_no_canal_que_esta_ligado(string? canal, bool esperado)
    {
        ComFicha(null, Celular("21988887777"));
        _configuracao.ConsultarEsusNoCanalAsync(CanalAppCidadao.Nativo, Arg.Any<CancellationToken>()).Returns(true);
        _configuracao.ConsultarEsusNoCanalAsync(CanalAppCidadao.Pwa, Arg.Any<CancellationToken>()).Returns(false);
        _esus.DisponivelAsync(Arg.Any<CancellationToken>()).Returns(true);

        var r = await Entrar(CriarServico(), canal: canal);

        r.PodeBuscarEsus.Should().Be(esperado);
    }

    [Fact]
    public async Task Esus_acrescenta_so_numeros_novos_e_nao_deixa_consultar_de_novo()
    {
        ComFicha(null, Celular("21988887777"));
        _configuracao.ConsultarEsusNoCanalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _esus.DisponivelAsync(Arg.Any<CancellationToken>()).Returns(true);
        _esus.BuscarAsync(Arg.Any<PessoaNaEntrada>(), Arg.Any<CancellationToken>())
            .Returns(["5521988887777", "5521944443333"]);
        var servico = CriarServico();
        var entrada = await Entrar(servico);

        var r = await servico.BuscarNoEsusAsync(entrada.DesafioId);

        r.Opcoes.Select(o => (o.Final, o.Origem)).Should().Equal(("7777", "cadastro"), ("3333", "esus"));
        r.PodeBuscarEsus.Should().BeFalse();
        await _esus.Received(1).BuscarAsync(
            Arg.Is<PessoaNaEntrada>(p => p.Cpf == _cpf && p.Nascimento == Nascimento), Arg.Any<CancellationToken>());

        await servico.BuscarNoEsusAsync(entrada.DesafioId);
        await _esus.Received(1).BuscarAsync(Arg.Any<PessoaNaEntrada>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Esus_indisponivel_nao_gasta_a_consulta_da_entrada()
    {
        ComFicha(null);
        _configuracao.ConsultarEsusNoCanalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _esus.DisponivelAsync(Arg.Any<CancellationToken>()).Returns(true);
        _esus.BuscarAsync(Arg.Any<PessoaNaEntrada>(), Arg.Any<CancellationToken>())
            .Throws(new ConflitoException("esus.indisponivel", "Tente de novo em instantes."));
        var servico = CriarServico();
        var entrada = await Entrar(servico);
        entrada.Opcoes.Should().BeEmpty();

        var acao = () => servico.BuscarNoEsusAsync(entrada.DesafioId);
        (await acao.Should().ThrowAsync<ConflitoException>()).Which.Codigo.Should().Be("esus.indisponivel");

        _esus.BuscarAsync(Arg.Any<PessoaNaEntrada>(), Arg.Any<CancellationToken>()).Returns(["5521944443333"]);
        var r = await servico.BuscarNoEsusAsync(entrada.DesafioId);
        r.Opcoes.Should().ContainSingle(o => o.Final == "3333" && o.Origem == "esus");
    }

    [Fact]
    public async Task Cadastro_novo_pelo_numero_do_esus_so_nasce_quando_o_codigo_e_confirmado()
    {
        var novoId = Guid.NewGuid();
        _pacientes.ObterPorCpfAsync(_cpf, Arg.Any<CancellationToken>()).Returns((PacienteExistenciaDto?)null);
        _receita.ConsultarCpfAsync(_cpf, Nascimento, Arg.Any<CancellationToken>())
            .Returns(new HubCpfRespostaDto(_cpf, "MARIA DA SILVA", "10/03/1980", "REGULAR", "Feminino"));
        _pacientes.CadastrarAsync(Arg.Any<CadastrarPacienteRequest>(), Arg.Any<CancellationToken>()).Returns(novoId);
        _configuracao.ConsultarEsusNoCanalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _esus.DisponivelAsync(Arg.Any<CancellationToken>()).Returns(true);
        _esus.BuscarAsync(Arg.Any<PessoaNaEntrada>(), Arg.Any<CancellationToken>()).Returns(["5521999990000"]);
        var servico = CriarServico();

        var entrada = await Entrar(servico);
        entrada.Opcoes.Should().BeEmpty();
        entrada.PodeBuscarEsus.Should().BeTrue();
        var comEsus = await servico.BuscarNoEsusAsync(entrada.DesafioId);
        var emitido = await servico.EnviarCodigoEntradaAsync(
            entrada.DesafioId, new EnviarCodigoEntradaRequest(comEsus.Opcoes.Single().Id));

        await _pacientes.DidNotReceiveWithAnyArgs().CadastrarAsync(default!, default);

        var login = await servico.ValidarOtpAsync(new ValidarOtpRequest(_cpf, emitido.CodigoTeste!), "PWA", "127.0.0.1");

        login.Paciente.Id.Should().Be(novoId);
        await _pacientes.Received(1).CadastrarAsync(
            Arg.Is<CadastrarPacienteRequest>(r =>
                r.Cpf == _cpf && r.NomeCompleto == "MARIA DA SILVA" && r.DataNascimento == Nascimento
                && r.Sexo == Sexo.Feminino && r.TelefonePrincipal == "21999990000"),
            Arg.Any<CancellationToken>());
        await _telefones.Received(1).MarcarValidadoAsync(
            _cpf, "5521999990000", "pwa-cidadao", null, Arg.Any<CancellationToken>());
    }

    // ---------- código e sessão ----------

    [Fact]
    public async Task Codigo_errado_nao_abre_sessao()
    {
        ComFicha(null, Celular("21988887777"));
        var servico = CriarServico();
        var entrada = await Entrar(servico);
        var emitido = await servico.EnviarCodigoEntradaAsync(entrada.DesafioId, new EnviarCodigoEntradaRequest(entrada.Opcoes[0].Id));
        var errado = emitido.CodigoTeste == "000000" ? "111111" : "000000";

        var acao = () => servico.ValidarOtpAsync(new ValidarOtpRequest(_cpf, errado), null, null);

        (await acao.Should().ThrowAsync<ValidacaoException>()).Which.Erros.Should().ContainKey("otp.invalido");
        await _sessoes.DidNotReceiveWithAnyArgs().AbrirSessaoAsync(default, default!, default!, default!, default, default, default);
    }

    [Fact]
    public async Task Operador_com_personificacao_entra_com_os_proprios_dados_e_abre_como_o_paciente()
    {
        // O operador entra com o PRÓPRIO CPF e nascimento — e o código vai para o WhatsApp DELE.
        ComFicha("5521999991234");
        var alvo = new PersonificacaoAlvo(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "JOANA ALVO", "11144477735", DateTime.UtcNow.AddHours(12));
        _personificacoes.ResolverNoLoginAsync(_cpf, Arg.Any<CancellationToken>()).Returns(alvo);
        _sessoes
            .AbrirSessaoPersonificadaAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<DateTime>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(("token-personificado", alvo.ExpiraEm));
        var servico = CriarServico();

        var entrada = await Entrar(servico);
        var otp = await servico.EnviarCodigoEntradaAsync(entrada.DesafioId, new EnviarCodigoEntradaRequest(entrada.Opcoes[0].Id));
        var r = await servico.ValidarOtpAsync(new ValidarOtpRequest(_cpf, otp.CodigoTeste!), null, null);

        r.Token.Should().Be("token-personificado");
        r.Paciente.Should().Be(new PacienteSessaoDto(alvo.PacienteId, "JOANA ALVO", "11144477735"));
        await _sessoes.DidNotReceiveWithAnyArgs().AbrirSessaoAsync(default, default!, default!, default!, default, default, default);
        await _telefones.Received(1).MarcarValidadoAsync(
            _cpf, "5521999991234", Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sem_personificacao_abre_como_o_proprio()
    {
        var id = ComFicha("5521999991234");
        var servico = CriarServico();

        var entrada = await Entrar(servico);
        var otp = await servico.EnviarCodigoEntradaAsync(entrada.DesafioId, new EnviarCodigoEntradaRequest(entrada.Opcoes[0].Id));
        var r = await servico.ValidarOtpAsync(new ValidarOtpRequest(_cpf, otp.CodigoTeste!), null, null);

        r.Paciente.Id.Should().Be(id);
        await _sessoes.Received(1).AbrirSessaoAsync(
            id, "MARIA DA SILVA", _cpf, "otp-whatsapp", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    internal static PacienteDto Paciente(
        Guid id,
        string? telefoneCelular = null,
        string? telefoneVerificado = null,
        DateOnly? nascimento = null,
        string cpf = Cpf,
        string nome = "MARIA DA SILVA") =>
        new(
            Id: id,
            NomeCompleto: nome,
            Cpf: cpf,
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
