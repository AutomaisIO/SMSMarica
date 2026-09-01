using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Sincronismo;
using SMSMais.Core.Notificacoes.WhatsApp;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Aviso de falha de sincronismo por WhatsApp.
///
/// <para>O caso que importa é o da <b>janela de 24h fechada</b>: os motores rodam de madrugada e o
/// operador não conversa com o número há dias, então <i>toda</i> notificação real cai nesse
/// caminho. Se a reabertura por template não disparar, o aviso simplesmente não existe — que foi
/// exatamente o que aconteceu na primeira carga inicial em produção (01/09/2026): 84 mensagens
/// gravadas como falha, nenhuma entregue, nenhum template enviado.</para>
/// </summary>
public class NotificadorSincronismoTests
{
    /// <summary>Erro real devolvido pela Meta, copiado da auditoria de produção.</summary>
    private const string ErroForaDaJanela =
        "(131047) Re-engagement message – Message failed to send because more than 24 hours have "
        + "passed since the customer last replied to this number.";

    /// <summary>
    /// Telefone único por teste. O freio de reabertura é <b>estático</b> (vive no processo, não na
    /// instância) — sem isso, um teste marcaria a janela do outro como "já reaberta" e a suíte
    /// passaria ou falharia conforme a ordem de execução.
    /// </summary>
    private static string TelefoneUnico() => $"219{Random.Shared.Next(10_000_000, 99_999_999)}";

    private static NotificadorSincronismo Criar(WhatsAppFake zap, string? telefone = null) =>
        new(new CredencialFake(telefone ?? TelefoneUnico()), zap,
            Options.Create(new ComunicacaoPacienteOptions()),
            NullLogger<NotificadorSincronismo>.Instance);

    [Fact]
    public async Task Com_a_janela_aberta_manda_so_o_texto()
    {
        var zap = new WhatsAppFake();
        await Criar(zap).NotificarAsync("sisreg", "Título", "Detalhe");

        zap.Textos.Should().HaveCount(1);
        zap.Textos[0].Should().Contain("Título").And.Contain("Detalhe");
        // Template é mensagem PAGA: não pode sair quando o texto já passou.
        zap.Templates.Should().BeEmpty();
    }

    [Fact]
    public async Task Com_a_janela_fechada_reabre_com_template_e_repete_o_relatorio()
    {
        // Primeiro texto falha (janela fechada); depois da reabertura, passa.
        var zap = new WhatsAppFake { ErroDoPrimeiroTexto = ErroForaDaJanela };

        await Criar(zap).NotificarAsync("sisreg", "CAPTCHA no SISREG", "Precisa de você");

        zap.Templates.Should().ContainSingle("a janela precisa ser reaberta uma vez");
        zap.Templates[0].Should().Be("validacao_cadastro");
        zap.Textos.Should().HaveCount(2, "o relatório é reenviado depois da reabertura");
        zap.Textos[1].Should().Contain("Precisa de você");
    }

    [Fact]
    public async Task Falha_que_nao_e_janela_fechada_nao_gasta_template()
    {
        var zap = new WhatsAppFake { ErroDoPrimeiroTexto = "(131026) Message undeliverable" };

        await Criar(zap).NotificarAsync("sisreg", "Título", "Detalhe");

        zap.Templates.Should().BeEmpty("número inválido não se resolve reabrindo janela");
        zap.Textos.Should().HaveCount(1);
    }

    /// <summary>
    /// Uma carga inicial com 40 unidades gera dezenas de avisos em minutos. Se cada um tentasse
    /// reabrir a janela, seriam dezenas de mensagens PAGAS — e o operador receberia o mesmo
    /// "temos uma informação" quarenta vezes.
    /// </summary>
    [Fact]
    public async Task Rajada_de_avisos_reabre_a_janela_uma_vez_so()
    {
        var zap = new WhatsAppFake { ErroDoPrimeiroTexto = ErroForaDaJanela, FalharTodosOsTextos = true };
        var notificador = Criar(zap);

        for (var i = 0; i < 10; i++)
        {
            await notificador.NotificarAsync("sisreg", $"Unidade {i}", "detalhe");
        }

        zap.Templates.Should().HaveCount(1);
    }

    [Fact]
    public async Task Sem_telefone_cadastrado_nao_envia_nada()
    {
        var zap = new WhatsAppFake();
        var notificador = new NotificadorSincronismo(
            new CredencialFake(telefone: null), zap,
            Options.Create(new ComunicacaoPacienteOptions()),
            NullLogger<NotificadorSincronismo>.Instance);

        await notificador.NotificarAsync("sisreg", "Título", "Detalhe");

        zap.Textos.Should().BeEmpty();
        zap.Templates.Should().BeEmpty();
    }

    [Fact]
    public async Task A_mesma_chave_de_repeticao_nao_repete_o_alerta()
    {
        var zap = new WhatsAppFake();
        var notificador = Criar(zap);
        var chave = $"captcha-{Guid.NewGuid()}"; // o freio é estático: chave própria por teste

        await notificador.NotificarAsync("sisreg", "CAPTCHA", "primeira", chaveRepeticao: chave);
        await notificador.NotificarAsync("sisreg", "CAPTCHA", "segunda", chaveRepeticao: chave);

        zap.Textos.Should().ContainSingle();
    }

    // ---------------------------------------------------------------- dublês

    private sealed class WhatsAppFake : IWhatsAppCliente
    {
        public List<string> Textos { get; } = [];
        public List<string> Templates { get; } = [];
        public string? ErroDoPrimeiroTexto { get; init; }

        /// <summary>Todo texto falha — a janela nunca abre, como quando o operador não responde.</summary>
        public bool FalharTodosOsTextos { get; init; }

        private bool _primeiroTextoFeito;

        public Task<EnvioWhatsAppResultado> EnviarTextoAsync(
            string telefone, string texto, Guid? pacienteId = null, CancellationToken ct = default)
        {
            Textos.Add(texto);
            var falha = ErroDoPrimeiroTexto is not null && (FalharTodosOsTextos || !_primeiroTextoFeito);
            _primeiroTextoFeito = true;
            return Task.FromResult(falha
                ? new EnvioWhatsAppResultado(false, null, ErroDoPrimeiroTexto)
                : new EnvioWhatsAppResultado(true, "wamid", null));
        }

        public Task<EnvioWhatsAppResultado> EnviarTemplateAsync(
            string telefone, string template, string idiomaBcp47, IReadOnlyList<string> parametros,
            Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default)
        {
            Templates.Add(template);
            return Task.FromResult(new EnvioWhatsAppResultado(true, "wamid", null));
        }

        public Task<EnvioWhatsAppResultado> EnviarTemplateAutenticacaoAsync(
            string telefone, string template, string idiomaBcp47, string codigo,
            Guid? pacienteId = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<EnvioWhatsAppResultado> EnviarTemplateComBotoesAsync(
            string telefone, string template, string idiomaBcp47,
            IReadOnlyList<string> parametrosBody, IReadOnlyList<BotaoTemplateWhatsApp> botoes,
            Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<EnvioWhatsAppResultado> EnviarInterativoBotoesAsync(
            string telefone, string texto, IReadOnlyList<BotaoInterativoWhatsApp> botoes,
            Guid? pacienteId = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TemplateWhatsApp>> ListarTemplatesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateWhatsApp>>([]);
    }

    /// <param name="telefone">Null = integração sem telefone cadastrado.</param>
    private sealed class CredencialFake(string? telefone) : IIntegracaoCredencialService
    {
        private readonly string _json = telefone is null
            ? """{"autoLogin":true}"""
            : $$"""{"autoLogin":true,"telefonesNotificacao":["{{telefone}}"]}""";

        public Task<IntegracaoCredencialContexto> ObterContextoAsync(
            string provedor, CancellationToken ct = default) =>
            Task.FromResult(new IntegracaoCredencialContexto(
                provedor, null, null, null, _json, true));

        public Task<IntegracaoCredencialDto> ObterAsync(string provedor, CancellationToken ct = default) =>
            Task.FromResult(new IntegracaoCredencialDto(
                provedor, provedor, true, true, null, _json, true));

        public Task AtualizarAsync(
            string provedor, AtualizarIntegracaoCredencialRequest request, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task LimparAsync(string provedor, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<IntegracaoCredencialDto>> ListarAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IntegracaoCredencialDto>>([]);
    }
}
