namespace SMSMais.Core.Medicos.Assinatura;

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

    /// <summary>
    /// Modo de assinatura do médico (ADR-0061). Sem linha gravada devolve o padrão
    /// <see cref="Data.Entities.Enums.ModoAssinaturaMedico.Desktop"/> com <c>Configurado = false</c>.
    /// </summary>
    Task<ModoAssinaturaMedicoDto> ObterModoAsync(Guid medicoId, CancellationToken cancellationToken = default);

    /// <summary>Grava (upsert) o modo de assinatura do médico.</summary>
    Task<ModoAssinaturaMedicoDto> DefinirModoAsync(
        Guid medicoId, Data.Entities.Enums.ModoAssinaturaMedico modo, CancellationToken cancellationToken = default);
}
