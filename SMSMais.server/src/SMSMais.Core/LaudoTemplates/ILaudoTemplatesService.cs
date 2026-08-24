using SMSMais.Core.LaudoTemplates.Dtos;

namespace SMSMais.Core.LaudoTemplates;

public interface ILaudoTemplatesService
{
    Task<IReadOnlyList<LaudoTemplateListItemDto>> ListarAsync(
        string? categoria,
        bool incluirInativos,
        CancellationToken cancellationToken = default);

    Task<LaudoTemplateDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(
        Guid usuarioId,
        CadastrarLaudoTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task AtualizarAsync(
        Guid id,
        Guid usuarioId,
        AtualizarLaudoTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReativarAsync(Guid id, CancellationToken cancellationToken = default);
}
