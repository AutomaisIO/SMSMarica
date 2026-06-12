namespace SMSMarica.Core.Medicos.Assinatura;

/// <summary>
/// Rubrica visual (imagem) de assinatura do médico — usada no carimbo do PDF do
/// laudo. Armazenada no smsmarica, keyed pelo id do Practitioner (hub FHIR).
/// </summary>
public interface IAssinaturaMedicoService
{
    /// <summary>Rubrica ativa do médico, ou null se não houver.</summary>
    Task<AssinaturaMedicoDto?> ObterAsync(Guid medicoId, CancellationToken cancellationToken = default);

    /// <summary>Cria ou substitui (upsert) a rubrica do médico.</summary>
    Task<AssinaturaMedicoDto> SalvarAsync(
        Guid medicoId, SalvarAssinaturaMedicoRequest request, CancellationToken cancellationToken = default);

    /// <summary>Remove (soft-delete) a rubrica do médico.</summary>
    Task RemoverAsync(Guid medicoId, CancellationToken cancellationToken = default);
}
