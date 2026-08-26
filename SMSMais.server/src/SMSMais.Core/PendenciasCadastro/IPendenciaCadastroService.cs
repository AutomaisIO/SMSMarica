using SMSMais.Core.PendenciasCadastro.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.PendenciasCadastro;

public interface IPendenciaCadastroService
{
    Task<IReadOnlyList<PendenciaCadastroListItemDto>> ListarAsync(
        StatusPendenciaCadastro? status, CancellationToken ct = default);

    Task ResolverAsync(Guid id, string? nota, CancellationToken ct = default);

    Task IgnorarAsync(Guid id, string? nota, CancellationToken ct = default);

    /// <summary>
    /// Registra (ou atualiza a Aberta existente) uma pendência de número errado. Usado pelo
    /// comando do robô (<c>criadoPor = null</c>) e pelo registro manual do operador.
    /// Idempotente por telefone + paciente enquanto houver uma Aberta.
    /// </summary>
    Task<Guid> RegistrarNumeroErradoAsync(
        Guid? conversaId,
        string telefoneCanonical,
        Guid? pacienteId,
        VinculoContato vinculo,
        string? observacao,
        Guid? criadoPor,
        CancellationToken ct = default);
}
