using SMSMarica.Core.EstudoAnotacoes.Dtos;

namespace SMSMarica.Core.EstudoAnotacoes;

public interface IEstudoAnotacoesService
{
    /// <summary>Retorna a versão mais recente do estudo (ou null se ainda não houver anotações salvas).</summary>
    Task<EstudoAnotacaoVersaoDto?> ObterVersaoAtualAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>Lista todas as versões (do mais recente para o mais antigo) sem o payload.</summary>
    Task<IReadOnlyList<EstudoAnotacaoVersaoResumoDto>> ListarHistoricoAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>Retorna uma versão específica com o payload.</summary>
    Task<EstudoAnotacaoVersaoDto> ObterVersaoAsync(string studyInstanceUID, int versao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria uma nova versão (append-only). A versão é calculada como
    /// <c>max(versao) + 1</c> dentro do mesmo estudo. Retorna a versão criada.
    /// </summary>
    Task<EstudoAnotacaoVersaoDto> SalvarAsync(string studyInstanceUID, Guid usuarioId, SalvarEstudoAnotacaoRequest request, CancellationToken cancellationToken = default);
}
