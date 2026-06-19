using SMSMarica.Core.Cidadao.Dtos;

namespace SMSMarica.Core.Cidadao;

/// <summary>
/// Autenticação do paciente no PWA: CPF + código (OTP). Hoje o código é exibido na tela
/// (modo teste); quando o WhatsApp/Meta estiver ativo, o envio do código migra para lá.
/// </summary>
public interface IPacienteAuthService
{
    Task<OtpEmitidoDto> SolicitarOtpAsync(SolicitarOtpRequest request, CancellationToken ct = default);
    Task<RespostaLoginPacienteDto> ValidarOtpAsync(ValidarOtpRequest request, CancellationToken ct = default);
}
