using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Alertas;
using SMSMais.Core.Notificacoes.Sincronismo;
using SMSMais.Core.Notificacoes.WhatsApp;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Aviso de falha de sincronismo por WhatsApp.
///
/// <para>Falha vai pelo aviso da plataforma (template <c>erro_plataforma</c>), para a lista única
/// de Sistema → Avisos no celular — não existe mais lista por integração. O caminho antigo — texto livre e "reabertura" com o template de
/// verificação cadastral — não entregava fora da janela de 24h: na primeira carga inicial em
/// produção (01/09/2026) foram 84 mensagens gravadas como falha, nenhuma entregue.</para>
/// </summary>
public class NotificadorSincronismoTests
{
    private const string Telefone = "21999990000";

    private static NotificadorSincronismo Criar(WhatsAppFake zap, AlertaFake alerta, string? telefone = Telefone) =>
        new(new DestinatariosFake(telefone), zap, alerta, NullLogger<NotificadorSincronismo>.Instance);

    [Fact]
    public async Task Falha_vai_pelo_aviso_da_plataforma()
    {
        var zap = new WhatsAppFake();
        var alerta = new AlertaFake();

        await Criar(zap, alerta).NotificarAsync("sisreg", "Falhou: CDT", "timeout");

        alerta.Eventos.Should().ContainSingle();
        var e = alerta.Eventos[0];
        e.Chave.Should().Be("sincronismo.sisreg");
        e.Detalhe.Should().Be("timeout");
        zap.Textos.Should().BeEmpty("a falha não sai mais por texto livre");
        zap.Templates.Should().BeEmpty("quem manda o template é o despachante da plataforma");
    }

    /// <summary>
    /// CAPTCHA é o aviso mais acionável: não pode ficar preso atrás do freio de uma unidade que
    /// falhou minutos antes — por isso vira fonte própria.
    /// </summary>
    [Fact]
    public async Task Captcha_vira_fonte_propria_e_unidade_entra_na_fonte_do_provedor()
    {
        var alerta = new AlertaFake();
        var notificador = Criar(new WhatsAppFake(), alerta);

        await notificador.NotificarAsync("sisreg", "CAPTCHA", "x", chaveRepeticao: "captcha");
        await notificador.NotificarAsync("sisreg", "Falhou: USF", "y", chaveRepeticao: "unidade:123");

        alerta.Eventos.Select(e => e.Chave).Should().Equal("sincronismo.sisreg.captcha", "sincronismo.sisreg");
    }

    [Fact]
    public async Task Informativo_sai_so_por_texto_para_os_destinatarios_e_nao_gasta_template()
    {
        var zap = new WhatsAppFake();
        var alerta = new AlertaFake();

        await Criar(zap, alerta).NotificarAsync("sisreg", "OK CDT", "12 médicos", informativo: true);

        zap.Textos.Should().ContainSingle().Which.Should().Contain("OK CDT");
        zap.Templates.Should().BeEmpty();
        alerta.Eventos.Should().BeEmpty("progresso não é erro");
    }

    // ---------------------------------------------------------------- dublês

    private sealed class AlertaFake : IAlertaPlataforma
    {
        public List<EventoAlerta> Eventos { get; } = [];
        public void Reportar(EventoAlerta evento) => Eventos.Add(evento);
    }

    private sealed class WhatsAppFake : IWhatsAppCliente
    {
        public List<string> Textos { get; } = [];
        public List<string> Templates { get; } = [];

        public Task<EnvioWhatsAppResultado> EnviarTextoAsync(
            string telefone, string texto, Guid? pacienteId = null, CancellationToken ct = default,
            OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico)
        {
            Textos.Add(texto);
            return Task.FromResult(new EnvioWhatsAppResultado(true, "wamid", null));
        }

        public Task<EnvioWhatsAppResultado> EnviarTemplateAsync(
            string telefone, string template, string idiomaBcp47, IReadOnlyList<string> parametros,
            Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default,
            OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico)
        {
            Templates.Add(template);
            return Task.FromResult(new EnvioWhatsAppResultado(true, "wamid", null));
        }

        public Task<EnvioWhatsAppResultado> EnviarTemplateAutenticacaoAsync(
            string telefone, string template, string idiomaBcp47, string codigo,
            Guid? pacienteId = null, CancellationToken ct = default,
            OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico) =>
            throw new NotSupportedException();

        public Task<EnvioWhatsAppResultado> EnviarTemplateComBotoesAsync(
            string telefone, string template, string idiomaBcp47,
            IReadOnlyList<string> parametrosBody, IReadOnlyList<BotaoTemplateWhatsApp> botoes,
            Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default,
            OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico) =>
            throw new NotSupportedException();

        public Task<EnvioWhatsAppResultado> EnviarInterativoBotoesAsync(
            string telefone, string texto, IReadOnlyList<BotaoInterativoWhatsApp> botoes,
            Guid? pacienteId = null, CancellationToken ct = default,
            OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TemplateWhatsApp>> ListarTemplatesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateWhatsApp>>([]);
    }

    /// <param name="telefone">Null = ninguém cadastrado em Avisos no celular.</param>
    private sealed class DestinatariosFake(string? telefone) : IAlertaDestinatarios
    {
        public Task<IReadOnlyList<string>> ListarAtivosAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(telefone is null ? [] : [telefone]);
    }
}
