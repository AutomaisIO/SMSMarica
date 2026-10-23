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
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Telefones;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// O envio do aviso de remarcação (ADR-0083), com dois modelos próprios de "Mudança de data": número
/// provado recebe as duas datas, com "Confirmar nova data" e "Não poderei ir"; os demais recebem o
/// anônimo, com "Quero mais informações", e as datas chegam na conversa depois de se identificarem.
/// Sem os modelos aprovados na Meta a mensagem espera — nunca sai a confirmação comum no lugar.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AvisoRemarcacaoEnvioTests(PostgresFixture fixture)
{
    private readonly string _telefone = SeedSolicitacao.TelefoneAleatorio();

    private static readonly DateTime DataNova = DateTime.UtcNow.Date.AddDays(12).AddHours(12).AddMinutes(30);
    private static readonly DateTime DataAntiga = DateTime.UtcNow.Date.AddDays(5).AddHours(11);

    private async Task<(Solicitacao S, ComunicacaoPaciente C)> SeedAsync(
        SmsMaisDbContext db, DateTime? remarcadaDe, bool liberadaNaIdentificacao = false)
    {
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DataNova);
        var s = await db.Solicitacoes.SingleAsync(x => x.Id == exame.SolicitacaoId);
        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
            SolicitacaoId = s.Id,
            PacienteId = s.PacienteId,
            Status = StatusComunicacao.Pendente,
            ProximaTentativaEm = DateTime.UtcNow,
            IgnorarJanelaHorario = true, // a janela de envio não é o que está sob teste
            IgnorarVerificacaoTelefone = liberadaNaIdentificacao,
            RemarcadaDe = remarcadaDe,
            CriadoEm = DateTime.UtcNow,
        };
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();
        return (s, c);
    }

    private async Task<(ComunicacaoPacienteService Servico, IWhatsAppCliente Whats)> ServicoAsync(
        SmsMaisDbContext db, Solicitacao s, bool verificado, bool modelosAprovados = true,
        bool anonimoComoMarketing = false)
    {
        var cpf = SeedSolicitacao.CpfAleatorio();
        var link = new CidadaoLoginLink
        {
            Id = Guid.CreateVersion7(), PatientId = s.PacienteId, Cpf = cpf, SolicitacaoId = s.Id,
            ExpiraEm = DateTime.UtcNow.AddDays(3), CriadoEm = DateTime.UtcNow,
        };
        db.CidadaoLoginLinks.Add(link);
        await db.SaveChangesAsync();
        var links = Substitute.For<ICidadaoLoginLinkService>();
        links.GerarParaSolicitacaoAsync(default, default, default, default)
            .ReturnsForAnyArgs(new MagicLinkDto(link.Id, "https://app.exemplo/entrar", link.ExpiraEm));

        var pacientes = Substitute.For<IPacientesService>();
        var dto = PacienteDtoFabrica.Criar(s.PacienteId, "MARIA DE TESTE", cpf: cpf,
                nascimento: new DateOnly(1970, 1, 1), sexo: Sexo.Feminino)
            with { TelefoneCelular = _telefone, TelefoneVerificado = verificado ? _telefone : null };
        pacientes.ObterPorIdAsync(s.PacienteId, Arg.Any<CancellationToken>()).Returns(dto);

        var whats = Substitute.For<IWhatsAppCliente>();
        var catalogo = new List<TemplateWhatsApp> { new("confirmacao_regulacao", "pt_BR", "UTILITY", null, 7, []) };
        if (modelosAprovados)
        {
            catalogo.Add(new("agendamento_data_alterada", "pt_BR", "UTILITY", null, 4, []));
            catalogo.Add(new("agendamento_data_alterada_anonimo", "pt_BR",
                anonimoComoMarketing ? "MARKETING" : "UTILITY", null, 2, []));
        }
        whats.ListarTemplatesAsync(Arg.Any<CancellationToken>()).Returns(catalogo);
        whats.EnviarTemplateAsync(default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.AVISO.{Guid.NewGuid():N}", null));
        whats.EnviarTemplateComBotoesAsync(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.CONF.{Guid.NewGuid():N}", null));
        whats.EnviarInterativoBotoesAsync(default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.DADOS.{Guid.NewGuid():N}", null));

        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>())
            .Returns(new ConfirmacaoConfiguracaoDto("00:00", "00:00", 100, SomenteSisreg: false, true, null));
        var servico = new ComunicacaoPacienteService(
            db, pacientes, links, whats, Options.Create(new ComunicacaoPacienteOptions()),
            new UsuarioAtualAccessorFake(), Substitute.For<IDispensaContatoService>(),
            Substitute.For<IContatoComprometidoService>(), regras, NullLogger<ComunicacaoPacienteService>.Instance);
        return (servico, whats);
    }

    private static object?[] Chamada(IWhatsAppCliente whats, string metodo) =>
        Assert.Single(whats.ReceivedCalls(), x => x.GetMethodInfo().Name == metodo).GetArguments();

    private static bool Chamou(IWhatsAppCliente whats, string metodo) =>
        whats.ReceivedCalls().Any(x => x.GetMethodInfo().Name == metodo);

    private static string Quando(DateTime utc) =>
        FusoBrasilia.ParaExibicao(utc).ToString("dd/MM/yyyy 'às' HH:mm'h'");

    [Fact]
    public async Task Numero_verificado_recebe_a_mudanca_com_as_duas_datas_confirmar_e_nao_poderei_ir()
    {
        await using var db = fixture.CriarDbContext();
        var (s, c) = await SeedAsync(db, remarcadaDe: DataAntiga);
        var (servico, whats) = await ServicoAsync(db, s, verificado: true);

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        var args = Chamada(whats, nameof(IWhatsAppCliente.EnviarTemplateComBotoesAsync));
        Assert.Equal("agendamento_data_alterada", args[1]);
        var p = (IReadOnlyList<string>)args[3]!;
        Assert.StartsWith("Sra. ", p[0]);
        Assert.StartsWith("do seu exame de ", p[1]);
        Assert.Equal(Quando(DataAntiga), p[2]);
        Assert.Equal(Quando(DataNova), p[3]);
        // Só o link (confirmar) e o "Não poderei ir" — sem atendente.
        var botoes = (IReadOnlyList<BotaoTemplateWhatsApp>)args[4]!;
        Assert.Equal(2, botoes.Count);
        Assert.Equal(TipoBotaoTemplate.Url, botoes[0].Tipo);
        Assert.Equal($"confirma:{s.Id}", botoes[1].Valor);
    }

    [Fact]
    public async Task Numero_nao_verificado_recebe_o_anonimo_e_espera_o_mais_informacoes()
    {
        await using var db = fixture.CriarDbContext();
        var (s, c) = await SeedAsync(db, remarcadaDe: DataAntiga);
        var (servico, whats) = await ServicoAsync(db, s, verificado: false);

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        var args = Chamada(whats, nameof(IWhatsAppCliente.EnviarTemplateAsync));
        Assert.Equal("agendamento_data_alterada_anonimo", args[1]);
        Assert.Equal(new[] { "Sra. Maria", "do seu exame" }, (IReadOnlyList<string>)args[3]!); // nada do agendamento
        Assert.False(Chamou(whats, nameof(IWhatsAppCliente.EnviarTemplateComBotoesAsync)));

        await using var db2 = fixture.CriarDbContext();
        var atual = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.AguardandoVerificacaoCadastral, atual.Status);
        Assert.Contains("mudança de data", atual.MotivoFalha);
    }

    [Fact]
    public async Task Sem_os_modelos_aprovados_a_mensagem_espera_em_vez_de_sair_como_confirmacao_comum()
    {
        await using var db = fixture.CriarDbContext();
        var (s, c) = await SeedAsync(db, remarcadaDe: DataAntiga);
        var (servico, whats) = await ServicoAsync(db, s, verificado: true, modelosAprovados: false);

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        Assert.False(Chamou(whats, nameof(IWhatsAppCliente.EnviarTemplateComBotoesAsync)));
        Assert.False(Chamou(whats, nameof(IWhatsAppCliente.EnviarTemplateAsync)));
        await using var db2 = fixture.CriarDbContext();
        var atual = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Pendente, atual.Status);
        Assert.Equal(0, atual.Tentativas); // esperar não é tentativa
        Assert.True(atual.ProximaTentativaEm > DateTime.UtcNow.AddMinutes(50));
        Assert.Contains("Esperando o modelo", atual.MotivoFalha);
    }

    [Fact]
    public async Task Depois_de_se_identificar_vai_o_modelo_completo_e_nao_o_anonimo_de_novo()
    {
        await using var db = fixture.CriarDbContext();
        // A liberação liga IgnorarVerificacaoTelefone: o número acabou de ser provado.
        var (s, c) = await SeedAsync(db, remarcadaDe: DataAntiga, liberadaNaIdentificacao: true);
        var (servico, whats) = await ServicoAsync(db, s, verificado: false);

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        var args = Chamada(whats, nameof(IWhatsAppCliente.EnviarTemplateComBotoesAsync));
        Assert.Equal("agendamento_data_alterada", args[1]);
        Assert.False(Chamou(whats, nameof(IWhatsAppCliente.EnviarTemplateAsync)));
    }

    [Fact]
    public async Task Ao_pedir_mais_informacoes_os_dados_dizem_a_data_anterior_e_tem_so_confirmar_e_nao_poderei_ir()
    {
        await using var db = fixture.CriarDbContext();
        var (s, c) = await SeedAsync(db, remarcadaDe: DataAntiga);
        var (servico, whats) = await ServicoAsync(db, s, verificado: true);

        Assert.True(await servico.EnviarDetalhesNaConversaAsync(c.Id, _telefone));

        var args = Chamada(whats, nameof(IWhatsAppCliente.EnviarInterativoBotoesAsync));
        var texto = (string)args[1]!;
        Assert.StartsWith("*Mudança de data:*", texto);
        Assert.Contains(FusoBrasilia.ParaExibicao(DataAntiga).ToString("dd/MM/yyyy"), texto);
        Assert.Contains(FusoBrasilia.ParaExibicao(DataNova).ToString("dd/MM/yyyy"), texto);
        var botoes = (IReadOnlyList<BotaoInterativoWhatsApp>)args[2]!;
        Assert.Equal(new[] { "Sim, confirmo", "Não poderei ir" }, botoes.Select(b => b.Titulo));
    }

    [Fact]
    public async Task Anonimo_aprovado_como_marketing_conta_como_nao_aprovado_e_a_mensagem_espera()
    {
        await using var db = fixture.CriarDbContext();
        var (s, c) = await SeedAsync(db, remarcadaDe: DataAntiga);
        var (servico, whats) = await ServicoAsync(db, s, verificado: false, anonimoComoMarketing: true);

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        Assert.False(Chamou(whats, nameof(IWhatsAppCliente.EnviarTemplateAsync)));
        await using var db2 = fixture.CriarDbContext();
        var atual = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Pendente, atual.Status);
        Assert.Equal(0, atual.Tentativas);
        Assert.Contains("utilidade", atual.MotivoFalha);
    }

    [Fact]
    public async Task Envio_barrado_por_marketing_espera_a_revisao_sem_gastar_tentativa()
    {
        await using var db = fixture.CriarDbContext();
        var (s, c) = await SeedAsync(db, remarcadaDe: null);
        var (servico, whats) = await ServicoAsync(db, s, verificado: true);
        whats.EnviarTemplateComBotoesAsync(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(false, null,
                $"{WhatsAppCliente.CodigoModeloMarketing}: o modelo está classificado como MARKETING."));

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        await using var db2 = fixture.CriarDbContext();
        var atual = await db2.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Pendente, atual.Status); // não vira Falha
        Assert.Equal(0, atual.Tentativas);
        Assert.True(atual.ProximaTentativaEm > DateTime.UtcNow.AddMinutes(50));
        Assert.StartsWith(WhatsAppCliente.CodigoModeloMarketing, atual.MotivoFalha);
    }

    [Fact]
    public async Task Sem_remarcacao_sai_a_confirmacao_comum()
    {
        await using var db = fixture.CriarDbContext();
        var (s, c) = await SeedAsync(db, remarcadaDe: null);
        var (servico, whats) = await ServicoAsync(db, s, verificado: true);

        await servico.ProcessarTentativaEnvioAsync(c.Id);

        var args = Chamada(whats, nameof(IWhatsAppCliente.EnviarTemplateComBotoesAsync));
        Assert.Equal("confirmacao_regulacao", args[1]);
    }
}
