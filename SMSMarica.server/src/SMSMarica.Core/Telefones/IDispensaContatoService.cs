using SMSMarica.Core.Telefones.Dtos;

namespace SMSMarica.Core.Telefones;

/// <summary>
/// Dispensa de verificação do contato: o registro de que o paciente NÃO vai validar o WhatsApp,
/// com o motivo e a ciência dele. Existe para destravar a recepção — sem isso, quem não tem
/// celular (ou não consegue confirmar o código) ficava parado no balcão, porque a autorização
/// presencial exige contato verificado.
///
/// Escopo: por PACIENTE, até revogar. Cai sozinha quando o contato é verificado por OTP ou
/// quando o telefone principal muda (número novo merece uma tentativa nova de verificar).
/// </summary>
public interface IDispensaContatoService
{
    /// <summary>As opções de motivo que a recepção vê, com o efeito de cada uma nos envios.</summary>
    IReadOnlyList<MotivoDispensaContatoDto> ListarMotivos();

    /// <summary>
    /// Registra a dispensa. Exige <c>PacienteCiente</c> e, quando o motivo é "Outro", descrição.
    /// Se já havia uma ativa, ela é revogada e uma nova entra (a trilha guarda as duas).
    /// Solta ou reetiqueta as comunicações que estavam retidas por falta de contato verificado,
    /// conforme o motivo permita ou não o envio.
    /// </summary>
    Task<DispensaContatoDto> RegistrarAsync(
        Guid pacienteId, Data.Entities.Enums.MotivoDispensaContato motivo, string? motivoDescricao,
        bool pacienteCiente, CancellationToken ct = default);

    /// <summary>Derruba a dispensa ativa do paciente. Sem dispensa ativa é no-op (idempotente).</summary>
    Task RevogarAsync(Guid pacienteId, string? motivo, CancellationToken ct = default);

    /// <summary>Dispensa ATIVA do paciente, ou null.</summary>
    Task<DispensaContatoDto?> ObterAtivaAsync(Guid pacienteId, CancellationToken ct = default);

    /// <summary>Dispensas ativas de vários pacientes (listagens; evita N+1).</summary>
    Task<IReadOnlyDictionary<Guid, DispensaContatoDto>> ObterAtivasAsync(
        IEnumerable<Guid> pacienteIds, CancellationToken ct = default);
}
