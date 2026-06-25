using SMSMarica.Core.Associacoes.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Associacoes;

/// <summary>
/// Associação explícita estudo PACS ↔ solicitação de exame, para exames que não
/// chegaram via worklist. Soft-delete = desassociar. A resolução de vínculo
/// combina a associação explícita (precedência) com o casamento implícito por
/// StudyInstanceUID dos exames de worklist.
/// </summary>
public interface IExameAssociacaoService
{
    /// <summary>
    /// Associa um estudo a uma solicitação (pelo accession SMS). Idempotente quando
    /// já associado à MESMA solicitação; conflita se já associado a OUTRA. Promove a
    /// solicitação para Realizada. <paramref name="validarNoPacs"/> checa a existência
    /// do estudo no dcm4chee (true no fluxo manual; false no automático, que já achou).
    /// </summary>
    Task<ExameAssociacaoDto> AssociarAsync(
        AssociarExameRequest request,
        OrigemAssociacaoExame origem = OrigemAssociacaoExame.Manual,
        bool validarNoPacs = true,
        CancellationToken cancellationToken = default);

    /// <summary>Desassocia (soft-delete). Bloqueia se houver laudo finalizado para o estudo.</summary>
    Task DesassociarAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve o vínculo de um estudo: 1º associação explícita ativa, 2º solicitação
    /// casada implicitamente por StudyInstanceUID (worklist). Null se não houver.
    /// </summary>
    Task<VinculoExame?> ResolverVinculoAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lote para a listagem PACS: para cada studyUID, devolve o vínculo (explícito ou
    /// implícito) com o nome do paciente resolvido. UIDs sem vínculo não retornam linha.
    /// </summary>
    Task<IReadOnlyList<ExameAssociacaoDto>> ObterPorStudyUidsAsync(
        IReadOnlyList<string> studyInstanceUIDs, CancellationToken cancellationToken = default);
}
