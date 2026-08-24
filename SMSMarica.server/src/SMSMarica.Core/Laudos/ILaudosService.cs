using SMSMarica.Core.Laudos.Dtos;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Laudos;

public interface ILaudosService
{
    Task<PaginaLaudosDto> ListarAsync(
        FiltroLaudosDto filtro,
        CancellationToken cancellationToken = default);

    Task<LaudoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Última versão (rascunho ou finalizada) do estudo.</summary>
    Task<LaudoDto?> ObterPorStudyAsync(
        string studyInstanceUID,
        CancellationToken cancellationToken = default);

    /// <summary>Histórico (todas as versões, sem conteúdo).</summary>
    Task<IReadOnlyList<LaudoHistoricoItemDto>> ListarHistoricoAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a última versão (não-excluída) de cada Study informado.
    /// Usado pela listagem PACS para popular o botão "PDF" / "Editar".
    /// </summary>
    Task<IReadOnlyList<LaudoPorStudyDto>> ListarPorStudyUidsAsync(
        IReadOnlyList<string> studyInstanceUIDs,
        CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(
        Guid usuarioId,
        CadastrarLaudoRequest request,
        CancellationToken cancellationToken = default);

    Task AtualizarAsync(
        Guid id,
        Guid usuarioId,
        AtualizarLaudoRequest request,
        CancellationToken cancellationToken = default);

    Task FinalizarAsync(
        Guid id,
        Guid usuarioId,
        FinalizarLaudoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Cria um novo rascunho derivado de um laudo finalizado (versão + 1).</summary>
    Task<Guid> CriarNovaVersaoAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-delete (só rascunho).</summary>
    Task ExcluirAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Carrega o laudo cru (com Medico/Paciente) para a renderização do PDF.</summary>
    Task<Laudo?> CarregarParaPdfAsync(Guid id, CancellationToken cancellationToken = default);
}
