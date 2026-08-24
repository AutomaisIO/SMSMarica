using FluentAssertions;
using SMSMarica.Core.Notificacoes.Comunicacao;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// O detector de erro permanente precisa casar o envelope do Automais.Zap ("{message} (code N)")
/// E o antigo ("(N) message"). Quebrou uma vez na troca de transporte sem ninguém perceber:
/// número inexistente voltava para a fila em vez de falhar de vez.
/// </summary>
public sealed class ComunicacaoErroPermanenteTests
{
    [Theory]
    [InlineData("Message Undeliverable (code 131026)")]
    [InlineData("Phone number not allowed (code 131030)")]
    [InlineData("(131026) Message Undeliverable")]
    [InlineData("(131030) Phone number not allowed")]
    public void Reconhece_codigos_permanentes_nos_dois_envelopes(string erro)
        => ComunicacaoPacienteService.ErroPermanente(erro).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("timeout falando com a Meta")]
    [InlineData("Rate limit hit (code 130429)")]
    [InlineData("HTTP 502: Bad Gateway")]
    public void Outros_erros_continuam_retentaveis(string? erro)
        => ComunicacaoPacienteService.ErroPermanente(erro).Should().BeFalse();
}
