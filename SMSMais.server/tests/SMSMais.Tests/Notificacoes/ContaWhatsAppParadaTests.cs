using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Notificacoes.WhatsApp.Conta;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Conta do WhatsApp parada (19/10/2026: 131042, sem pagamento, das 10:29 às ~12:50 — 694 envios
/// recusados e ninguém avisado). O detector separa erro de CONTA de erro de NÚMERO; o freio segura
/// a fila sem gastar tentativa e deixa passar uma sonda por intervalo.
/// </summary>
public sealed class ContaWhatsAppParadaTests
{
    private sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = inicio;
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private static (SituacaoContaWhatsApp Situacao, RelogioManual Relogio) Nova(int sondaMinutos = 10)
    {
        var relogio = new RelogioManual(new DateTimeOffset(2026, 10, 19, 13, 29, 0, TimeSpan.Zero));
        var opcoes = Options.Create(new ContaWhatsAppOptions { IntervaloSondaMinutos = sondaMinutos });
        return (new SituacaoContaWhatsApp(opcoes, NullLogger<SituacaoContaWhatsApp>.Instance, relogio), relogio);
    }

    private static readonly ErroDeContaWhatsApp Pagamento =
        ErroContaWhatsApp.Classificar("(131042) Business eligibility payment issue")!;

    [Theory]
    [InlineData("(131042) Business eligibility payment issue — Message failed to send", "131042")]
    [InlineData("Business eligibility payment issue (code 131042)", "131042")]
    [InlineData("Business eligibility payment issue (code 131042/2494055)", "131042")]
    [InlineData("(#131031) Account has been locked", "131031")]
    [InlineData("(#368) Temporarily blocked for policies violations", "368")]
    [InlineData("Error validating access token (code 190/463)", "190")]
    [InlineData("Spam rate limit hit (code 131048)", "131048")]
    [InlineData("(133010) Account not registered", "133010")]
    public void Reconhece_erro_de_conta_nos_dois_envelopes(string erro, string codigo)
        => ErroContaWhatsApp.Classificar(erro)!.Codigo.Should().Be(codigo);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("(131026) Message undeliverable — Message Undeliverable.")]
    [InlineData("Message Undeliverable (code 131026)")]
    [InlineData("Rate limit hit (code 130429)")]
    [InlineData("Connection refused (api.smsmais.automais.com:443)")]
    [InlineData("HTTP 502: Bad Gateway")]
    [InlineData("Re-engagement message (code 131047)")]
    public void Erro_de_numero_ou_de_rede_nao_e_de_conta(string? erro)
        => ErroContaWhatsApp.Classificar(erro).Should().BeNull();

    [Fact]
    public void Conta_parada_segura_envios_e_libera_uma_sonda_por_intervalo()
    {
        var (s, relogio) = Nova(sondaMinutos: 10);
        s.PodeEnviar().Should().BeTrue("conta funcionando não tem freio");

        s.Parar(Pagamento, OrigemSituacaoConta.Envio, "(131042) ...").Should().BeTrue();
        s.Parar(Pagamento, OrigemSituacaoConta.Envio, "(131042) ...").Should().BeFalse("já estava parada");

        s.PodeEnviar().Should().BeFalse();
        s.PodeEnviar().Should().BeFalse();
        s.SondaDisponivel.Should().BeFalse();
        s.Parada!.EnviosSegurados.Should().Be(2);

        relogio.Agora = relogio.Agora.AddMinutes(10);
        s.SondaDisponivel.Should().BeTrue();
        s.PodeEnviar().Should().BeTrue("passou o intervalo: é a sonda");
        s.PodeEnviar().Should().BeFalse("a sonda é uma só por intervalo");
    }

    [Fact]
    public void Envio_aceito_libera_a_parada_que_veio_de_envio()
    {
        var (s, _) = Nova();
        s.Parar(Pagamento, OrigemSituacaoConta.Envio, null);
        s.Liberar(OrigemSituacaoConta.Envio).Should().BeTrue();
        s.Parada.Should().BeNull();
        s.PodeEnviar().Should().BeTrue();
    }

    [Fact]
    public void Parada_que_veio_do_recibo_nao_e_liberada_por_envio_aceito()
    {
        // Quando a Meta recusa no recibo, o envio SEMPRE é aceito antes — liberar ali soltaria a
        // fila inteira para voltar recusada a cada sonda.
        var (s, _) = Nova();
        s.Parar(Pagamento, OrigemSituacaoConta.Webhook, null);
        s.Liberar(OrigemSituacaoConta.Envio).Should().BeFalse();
        s.Parada.Should().NotBeNull();

        s.Liberar(OrigemSituacaoConta.Webhook).Should().BeTrue("recibo de entrega prova que a conta voltou");
    }

    [Fact]
    public void Consulta_de_saude_libera_qualquer_parada()
    {
        var (s, _) = Nova();
        s.Parar(Pagamento, OrigemSituacaoConta.Webhook, null);
        s.Liberar(OrigemSituacaoConta.SaudeMeta).Should().BeTrue();
    }

    [Theory]
    [InlineData("CONTA_WHATSAPP_PARADA: A Meta recusou os envios por problema de PAGAMENTO")]
    [InlineData("(131042) Business eligibility payment issue")]
    [InlineData("Account has been locked (code 131031)")]
    public void Comunicacao_nao_desiste_quando_a_conta_esta_parada(string erro)
        => ComunicacaoPacienteService.EhContaParada(erro).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("(131026) Message undeliverable")]
    [InlineData("Connection refused (api.smsmais.automais.com:443)")]
    public void Comunicacao_segue_a_regua_normal_nos_demais_erros(string? erro)
        => ComunicacaoPacienteService.EhContaParada(erro).Should().BeFalse();

    [Fact]
    public void O_prefixo_do_freio_e_o_que_a_comunicacao_reconhece()
        => WhatsAppCliente.CodigoContaParada.Should().Be("CONTA_WHATSAPP_PARADA");

    private const string Saude = """
        {"numeros":[
          {"phone_number_id":"111","pode_enviar":"BLOCKED","problemas":[
            {"entidade":"PHONE_NUMBER","pode_enviar":"AVAILABLE"},
            {"entidade":"WABA","pode_enviar":"BLOCKED","codigo":"141006",
             "descricao":"There is an error with the payment method.","solucao":"Update the payment method."}]},
          {"phone_number_id":"222","pode_enviar":"AVAILABLE","problemas":[]},
          {"phone_number_id":"333","pode_enviar":null,"erro":"timeout falando com a Meta"}
        ]}
        """;

    [Fact]
    public void Saude_bloqueada_vira_parada_com_o_motivo_da_Meta()
    {
        var saude = SaudeContaWhatsAppCliente.Parsear(Saude, "111")!;
        saude.Bloqueado.Should().BeTrue();

        var erro = SaudeContaWhatsAppCliente.ErroDe(saude);
        erro.Codigo.Should().Be("141006");
        erro.Explicacao.Should().Contain("WABA").And.Contain("payment method");
    }

    [Fact]
    public void Saude_do_outro_numero_nao_se_confunde_e_consulta_sem_resposta_fica_muda()
    {
        SaudeContaWhatsAppCliente.Parsear(Saude, "222")!.Bloqueado.Should().BeFalse();
        SaudeContaWhatsAppCliente.Parsear(Saude, "333").Should().BeNull("a Meta não respondeu sobre este número");
        SaudeContaWhatsAppCliente.Parsear(Saude, "999").Should().BeNull();
        SaudeContaWhatsAppCliente.Parsear("<html>404</html>", "111").Should().BeNull();
    }
}
