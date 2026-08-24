using SMSMais.Core.Telefones.Dtos;

namespace SMSMais.Core.Telefones;

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

    /// <summary>
    /// Lança <c>ConflitoException</c> se o número já é o contato CONFIRMADO de OUTRO CPF. Existe
    /// para quem gera o próprio OTP (login do PWA) poder barrar ANTES de mandar o código — falhar
    /// só na confirmação queimaria o código do cidadão por um erro que já era conhecido.
    /// </summary>
    Task GarantirNumeroLivreAsync(string cpf, string numero, CancellationToken ct = default);

    /// <summary>
    /// Define o telefone PRINCIPAL do paciente (por CPF) SEM exigir verificação — edição manual
    /// rápida pelo painel (ticket #16). Trocar o número derruba o marcador de verificado
    /// (o novo número nasce não-verificado; verificar depois é opcional).
    /// </summary>
    Task<TelefoneValidadoDto> DefinirPrincipalAsync(string cpf, string numero, CancellationToken ct = default);
}
