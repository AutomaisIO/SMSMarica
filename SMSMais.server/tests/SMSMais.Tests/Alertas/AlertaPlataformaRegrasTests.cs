using System.Net;
using FluentAssertions;
using SMSMais.Core.Alertas;

namespace SMSMais.Tests.Alertas;

/// <summary>Regras puras do aviso de erro da plataforma (sem banco).</summary>
public class AlertaPlataformaRegrasTests
{
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(6, 960)]
    [InlineData(50, 960)]
    public void Freio_cresce_enquanto_a_fonte_segue_falhando(int avisosSeguidos, int minutos) =>
        AlertaPlataformaDespachante.IntervaloApos(avisosSeguidos).Should().Be(TimeSpan.FromMinutes(minutos));

    /// <summary>A Meta recusa variável com quebra de linha, tabulação ou vazia.</summary>
    [Theory]
    [InlineData("linha 1\nlinha 2\tfim", "linha 1 linha 2 fim")]
    [InlineData("   ", "-")]
    [InlineData(null, "-")]
    [InlineData("a      b", "a b")]
    [InlineData("*SISREG — CAPTCHA*\n\nPrecisa de você", "SISREG — CAPTCHA Precisa de você")]
    public void Variavel_do_template_sai_em_uma_linha(string? entrada, string esperado) =>
        AlertaPlataformaDespachante.ParaVariavel(entrada).Should().Be(esperado);

    [Fact]
    public void Variavel_do_template_tem_teto() =>
        AlertaPlataformaDespachante.ParaVariavel(new string('x', 2000)).Length.Should().Be(700);

    [Theory]
    [InlineData("SMSMais.Core.Integracoes.SerWeb.Varredura.Background.VarreduraSerRunner", true)]
    [InlineData("Microsoft.Extensions.Hosting.Internal.Host", true)]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command", false)]
    [InlineData("SMSMais.Core.Alertas.AlertaPlataformaWorker", false)]
    [InlineData("SMSMais.Core.Notificacoes.WhatsApp.WhatsAppCliente", false)]
    // O webhook e os manipuladores das conversas ENTRAM: só o cliente de envio fica fora.
    [InlineData("SMSMais.Core.Notificacoes.WhatsApp.WhatsAppWebhookService", true)]
    [InlineData("SMSMais.Core.Notificacoes.WhatsApp.Manipuladores.VerificacaoCadastralWhatsAppHandler", true)]
    [InlineData("SMSMais.Api.Middleware.ExceptionHandlingMiddleware", false)]
    public void Captura_do_log_pega_a_plataforma_e_nao_o_proprio_caminho_do_aviso(string categoria, bool captura) =>
        AlertaCatalogo.CapturarDoLog(categoria).Should().Be(captura);

    [Fact]
    public void Mesma_mensagem_de_log_e_uma_fonte_so_e_mensagens_diferentes_sao_fontes_diferentes()
    {
        const string cat = "SMSMais.Core.Integracoes.SisregWeb.Varredura.VarreduraAgendaService";
        var a = AlertaCatalogo.OrigemDoLog(cat, "Falha na varredura da unidade {Unidade}.");
        var b = AlertaCatalogo.OrigemDoLog(cat, "Falha na varredura da unidade {Unidade}.");
        var c = AlertaCatalogo.OrigemDoLog(cat, "SISREG_VARREDURA_CAPTCHA: unidade {Unidade} parou.");

        a.Chave.Should().Be(b.Chave);
        a.Chave.Should().NotBe(c.Chave);
        a.Rotulo.Should().Be("VarreduraAgendaService");
        a.Grupo.Should().Be("Sincronismo SISREG");
    }

    [Fact]
    public void Crash_de_BackgroundService_vira_servico_parou() =>
        AlertaCatalogo.OrigemDoLog("Microsoft.Extensions.Hosting.Internal.Host", "BackgroundService failed")
            .Chave.Should().Be(AlertaCatalogo.ServicoParou);

    /// <summary>O caso de 12/09/2026: crédito acabou e o robô ficou mudo.</summary>
    [Fact]
    public void Credito_esgotado_e_reconhecido_como_falha_de_conta()
    {
        const string corpo = """{"type":"error","error":{"type":"invalid_request_error","message":"Your credit balance is too low to access the Anthropic API."}}""";

        FalhaContaIa.Classificar(HttpStatusCode.BadRequest, corpo).Should().Contain("crédito");
        FalhaContaIa.EhFalhaDeConta($"Anthropic retornou 400: {corpo}").Should().BeTrue();
    }

    /// <summary>22 a 24/09/2026: teto de gasto mensal; o robô ficou três dias mudo sem o aviso da conta.</summary>
    [Fact]
    public void Limite_de_gasto_da_conta_e_reconhecido_como_falha_de_conta()
    {
        const string corpo = """{"type":"error","error":{"type":"invalid_request_error","message":"You have reached your specified API usage limits. You will regain access on 2026-10-01 at 00:00 UTC."}}""";

        FalhaContaIa.Classificar(HttpStatusCode.BadRequest, corpo).Should().Contain("limite de gasto");
        FalhaContaIa.EhFalhaDeConta($"Anthropic retornou 400: {corpo}").Should().BeTrue();
    }

    [Fact]
    public void Erro_comum_da_api_nao_e_falha_de_conta() =>
        FalhaContaIa.Classificar(HttpStatusCode.BadRequest,
            """{"error":{"type":"invalid_request_error","message":"max_tokens: too large"}}""")
            .Should().BeNull();
}
