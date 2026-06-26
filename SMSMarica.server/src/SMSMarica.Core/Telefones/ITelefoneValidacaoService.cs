using SMSMarica.Core.Telefones.Dtos;

namespace SMSMarica.Core.Telefones;

/// <summary>
/// Validação de número de telefone por OTP (WhatsApp). O alvo é o NÚMERO em si — uma vez
/// validado, fica registrado globalmente (<c>numero_validado</c>) e qualquer cadastro com
/// esse número exibe o selo. Usado pelo painel (operadora dispara) e pelo PWA do cidadão
/// (o login por OTP marca o número automaticamente).
/// </summary>
public interface ITelefoneValidacaoService
{
    /// <summary>Gera e envia um código por WhatsApp para o número informado.</summary>
    Task<TelefoneOtpEmitidoDto> EnviarCodigoAsync(string numero, CancellationToken ct = default);

    /// <summary>Confirma o código; em caso de sucesso, marca o número como validado (origem painel).</summary>
    Task<TelefoneValidadoDto> ConfirmarCodigoAsync(string numero, string codigo, CancellationToken ct = default);

    /// <summary>Marca um número como validado sem OTP (ex.: caminho do PWA cidadão). Idempotente.</summary>
    Task MarcarValidadoAsync(string numero, string origem, Guid? validadoPor, CancellationToken ct = default);

    /// <summary>Situação de validação dos números informados (em lote).</summary>
    Task<IReadOnlyList<TelefoneValidadoDto>> ConsultarAsync(IReadOnlyList<string> numeros, CancellationToken ct = default);
}
