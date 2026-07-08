using SMSMarica.Core.Telefones.Dtos;

namespace SMSMarica.Core.Telefones;

/// <summary>
/// Validação do contato principal (WhatsApp) de uma PESSOA por OTP, ancorado por CPF.
/// O alvo é o par (CPF, número): uma vez validado, fica registrado em <c>contato_validado</c>
/// (1 por CPF, número único entre pessoas) e qualquer cadastro do mesmo CPF com esse número
/// exibe o selo. Usado pelo painel (operadora dispara) e pelo PWA do cidadão (o login por OTP
/// marca o contato automaticamente).
/// </summary>
public interface ITelefoneValidacaoService
{
    /// <summary>Gera e envia um código por WhatsApp para o número (contato principal do CPF).</summary>
    Task<TelefoneOtpEmitidoDto> EnviarCodigoAsync(string cpf, string numero, CancellationToken ct = default);

    /// <summary>Confirma o código; em sucesso, registra o número como contato validado do CPF.
    /// <paramref name="origem"/> identifica quem confirmou ("painel" | "pwa-cidadao").</summary>
    Task<TelefoneValidadoDto> ConfirmarCodigoAsync(
        string cpf, string numero, string codigo, CancellationToken ct = default, string origem = "painel");

    /// <summary>Marca o contato de um CPF como validado sem OTP (ex.: PWA cidadão). Idempotente por CPF.</summary>
    Task MarcarValidadoAsync(string cpf, string numero, string origem, Guid? validadoPor, CancellationToken ct = default);
}
