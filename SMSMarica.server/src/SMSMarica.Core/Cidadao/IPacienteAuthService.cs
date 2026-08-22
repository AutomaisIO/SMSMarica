using SMSMarica.Core.Cidadao.Dtos;

namespace SMSMarica.Core.Cidadao;

/// <summary>
/// Autenticação do paciente no PWA: CPF + código (OTP). Hoje o código é exibido na tela
/// (modo teste); com o canal WhatsApp configurado (Automais.Zap), o código vai por ele.
/// </summary>
public interface IPacienteAuthService
{
    /// <summary>
    /// Passo 1: só CPF. Envia o código apenas quando o contato do cadastro está VERIFICADO
    /// (<c>situacao=otp</c>). Sem verificação devolve <c>situacao=verificacao</c> e, sem cadastro,
    /// <c>situacao=cadastro</c> — em ambos nada é enviado e o app segue para
    /// <see cref="SolicitarOtpVerificacaoAsync"/>.
    /// </summary>
    Task<OtpEmitidoDto> SolicitarOtpAsync(SolicitarOtpRequest request, CancellationToken ct = default);

    /// <summary>
    /// Passo 2 (contato não verificado, ou número perdido): confere a identidade e envia o código
    /// para o telefone informado. Com cadastro: data de nascimento + nº da solicitação SISREG do
    /// próprio paciente. Sem cadastro: par CPF/nascimento conferido na Receita (proxy CPF) — o
    /// paciente é criado só quando o código é confirmado em <see cref="ValidarOtpAsync"/>.
    /// </summary>
    Task<OtpEmitidoDto> SolicitarOtpVerificacaoAsync(
        SolicitarOtpVerificacaoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Valida o OTP e abre a sessão (single-device). <paramref name="dispositivo"/> e
    /// <paramref name="ip"/> vêm do request HTTP (User-Agent / IP) só para rótulo/auditoria.
    /// </summary>
    Task<RespostaLoginPacienteDto> ValidarOtpAsync(
        ValidarOtpRequest request, string? dispositivo, string? ip, CancellationToken ct = default);
}
