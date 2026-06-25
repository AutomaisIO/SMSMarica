using SMSMarica.Core.Cidadao.Dtos;

namespace SMSMarica.Core.Cidadao;

/// <summary>
/// Consentimento LGPD do cidadão. O acesso ao app exige um aceite ativo da
/// <see cref="TermoConsentimento.VersaoVigente"/>. O gate por requisição vive em
/// <see cref="ICidadaoSessaoService"/> (junto da validação da sessão); aqui ficam a
/// consulta do status e o registro do aceite.
/// </summary>
public interface IConsentimentoCidadaoService
{
    /// <summary>Status do consentimento do paciente + texto/versão vigentes.</summary>
    Task<ConsentimentoStatusDto> ObterStatusAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Registra o aceite da versão vigente para o paciente (idempotente: se já houver
    /// aceite ativo da versão vigente, não duplica). Lança se o acesso não existir.
    /// </summary>
    Task RegistrarAsync(Guid patientId, string? ip, string? dispositivo, CancellationToken ct = default);
}
