using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Cidadao.Dtos;
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
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Confirmação do RETORNO (08/10/2026): quem volta já fez o primeiro atendimento com a guia. A
/// confirmação do Complexo Regulador ("Boas notícias!… retire a guia no posto") mandava o paciente
/// de fisioterapia ao posto a cada sessão. O retorno sai no <c>confirmar_agendamento_urlapp</c>, com
/// o local e o endereço da unidade executante — e a conferência de identidade continua igual.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConfirmacaoRetornoTests(PostgresFixture fixture)
{
    private readonly string _telefone = SeedSolicitacao.TelefoneAleatorio();

    private async Task<(Solicitacao S, ComunicacaoPaciente C)> SeedAsync(
        SmsMaisDbContext db, Guid pacienteId, TipoVaga? vaga)
    {
        var exame = await SeedSolicitacao.CriarAsync(db, pacienteId, dataAgendada: DateTime.UtcNow.AddDays(5));
        var s = await db.Solicitacoes.Include(x => x.UnidadeExecutante).SingleAsync(x => x.Id == exame.SolicitacaoId);
        s.TipoVaga = vaga;
        s.UnidadeExecutante!.Endereco = new Endereco
        {
            Cep = "24900000",
            Logradouro = "Avenida Roberto Silveira",
            Numero = "2158",
            Bairro = "Flamengo",
            Cidade = "Maricá",
            Uf = "RJ",
        };
        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Consulta,
            Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
            SolicitacaoId = s.Id,
            PacienteId = pacienteId,
            Status = StatusComunicacao.Pendente,
            ProximaTentativaEm = DateTime.UtcNow,
            // A janela de envio (08h–18h) não é o que está sob teste.
            IgnorarJanelaHorario = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();
        return (s, c);
    }

    private async Task<(ComunicacaoPacienteService Servico, IWhatsAppCliente Whats)> ServicoAsync(
        SmsMaisDbContext db, Solicitacao s, bool verificado)
    {
        var pacienteId = s.PacienteId;
        var cpf = SeedSolicitacao.CpfAleatorio();
        // A comunicação guarda o link gerado (FK): ele tem de existir de verdade.
        var link = new CidadaoLoginLink
        {
            Id = Guid.CreateVersion7(),
            PatientId = pacienteId,
            Cpf = cpf,
            SolicitacaoId = s.Id,
            ExpiraEm = DateTime.UtcNow.AddDays(3),
            CriadoEm = DateTime.UtcNow,
        };
        db.CidadaoLoginLinks.Add(link);
        await db.SaveChangesAsync();
        var links = Substitute.For<ICidadaoLoginLinkService>();
        links.GerarParaSolicitacaoAsync(default, default, default, default)
            .ReturnsForAnyArgs(new MagicLinkDto(link.Id, "https://app.exemplo/entrar", link.ExpiraEm));
        var pacientes = Substitute.For<IPacientesService>();
        var dto = PacienteDtoFabrica.Criar(pacienteId, "JOSE ROSA", cpf: cpf,
                nascimento: new DateOnly(1960, 1, 1), sexo: Sexo.Masculino)
            with { TelefoneCelular = _telefone, TelefoneVerificado = verificado ? _telefone : null };
        pacientes.ObterPorIdAsync(pacienteId, Arg.Any<CancellationToken>()).Returns(dto);
        var whats = Substitute.For<IWhatsAppCliente>();
        whats.ListarTemplatesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TemplateWhatsApp>());
        whats.EnviarTemplateComBotoesAsync(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.CONF.{Guid.NewGuid():N}", null));
        whats.EnviarTemplateAsync(default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.DESAFIO.{Guid.NewGuid():N}", null));
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>())
            .Returns(new ConfirmacaoConfiguracaoDto("00:00", "00:00", 100, SomenteSisreg: false, true, null));
        var servico = new ComunicacaoPacienteService(
            db, pacientes, links, whats, Options.Create(new ComunicacaoPacienteOptions()),
            new UsuarioAtualAccessorFake(), Substitute.For<IDispensaContatoService>(),
            Substitute.For<IContatoComprometidoService>(), regras, NullLogger<ComunicacaoPacienteService>.Instance);
        return (servico, whats);
    }

    private static object?[] EnvioComBotoes(IWhatsAppCliente whats) =>
        Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateComBotoesAsync)).GetArguments();

    [Fact]
    public async Task Retorno_com_numero_verificado_sai_com_local_e_endereco_sem_mandar_ao_posto()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var (s, c) = await SeedAsync(db, pacienteId, TipoVaga.Retorno);
        var (servico, whats) = await ServicoAsync(db, s, verificado: true);

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        var args = EnvioComBotoes(whats);
        Assert.Equal("confirmar_agendamento_urlapp", args[1]);
        var p = (IReadOnlyList<string>)args[3]!;
        Assert.Equal("um retorno", p[1]);
        Assert.Equal($"local: {s.UnidadeExecutante!.Nome}", p[4]);
        Assert.Equal("Avenida Roberto Silveira, 2158 · Flamengo · Maricá/RJ", p[6]);

        await using var db2 = fixture.CriarDbContext();
        Assert.Equal(StatusComunicacao.Enviada,
            (await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id)).Status);
    }

    [Fact]
    public async Task Retorno_com_numero_nao_verificado_passa_pela_conferencia_de_identidade()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var (s, c) = await SeedAsync(db, pacienteId, TipoVaga.Retorno);
        var (servico, whats) = await ServicoAsync(db, s, verificado: false);

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        // Só a primeira mensagem curta (sem dado nenhum do agendamento); a confirmação fica retida.
        Assert.Single(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateAsync));
        Assert.DoesNotContain(whats.ReceivedCalls(),
            x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTemplateComBotoesAsync));
        await using var db2 = fixture.CriarDbContext();
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral,
            (await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id)).Status);
    }

    [Fact]
    public async Task Primeira_vez_continua_com_a_confirmacao_do_complexo_regulador()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var (s, c) = await SeedAsync(db, pacienteId, TipoVaga.PrimeiraVez);
        var (servico, whats) = await ServicoAsync(db, s, verificado: true);

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        Assert.Equal("confirmacao_regulacao", EnvioComBotoes(whats)[1]);
    }

    [Theory]
    [InlineData(TipoVaga.Retorno, false)]
    [InlineData(TipoVaga.PrimeiraVez, true)]
    public async Task Combinado_so_lembra_de_retirar_a_guia_na_primeira_vez(TipoVaga vaga, bool lembraGuia)
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var (s, _) = await SeedAsync(db, pacienteId, vaga);

        var whats = Substitute.For<IWhatsAppCliente>();
        whats.EnviarTextoAsync(null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.OUT.{Guid.NewGuid():N}", null));
        whats.EnviarInterativoBotoesAsync(null!, null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.OUT.{Guid.NewGuid():N}", null));
        var handler = new ConfirmacaoAgendamentoWhatsAppHandler(
            db, whats, NullLogger<ConfirmacaoAgendamentoWhatsAppHandler>.Instance);

        // "Não poderei ir!" → "Não quero cancelar": vira confirmação de presença ("Combinado!").
        await handler.TratarAsync(Contexto(pacienteId, botao: $"confirma:{s.Id}"), default);
        await db.SaveChangesAsync();
        await handler.TratarAsync(Contexto(pacienteId, interativo: $"cancela_nao:{s.Id}"), default);
        await db.SaveChangesAsync();

        var combinado = Assert.Single(whats.ReceivedCalls()
            .Where(x => x.GetMethodInfo().Name == nameof(IWhatsAppCliente.EnviarTextoAsync))
            .Select(x => (string)x.GetArguments()[1]!));
        Assert.Contains("CONFIRMADA", combinado);
        Assert.Equal(lembraGuia, combinado.Contains("guia", StringComparison.OrdinalIgnoreCase));
    }

    private ManipuladorContexto Contexto(Guid pacienteId, string? botao = null, string? interativo = null)
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
            Status = StatusMensagemWhatsApp.Recebida,
            OcorridoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        return new ManipuladorContexto(conversa, msg, null, pacienteId, botao, interativo);
    }
}
