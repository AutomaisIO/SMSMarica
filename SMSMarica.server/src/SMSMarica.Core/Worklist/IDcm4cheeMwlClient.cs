using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Worklist;

/// <summary>
/// Cliente do endpoint de Modality Worklist (MWL) do dcm4chee — REST <c>/mwlitems</c>
/// no AE WORK-CDT. Cria/atualiza, confirma e exclui itens que o equipamento (Fuji
/// FDR) puxa via C-FIND MWL clássico. Substitui o antigo fluxo UPS-RS.
/// </summary>
public interface IDcm4cheeMwlClient
{
    /// <summary>
    /// Resolve o paciente no hub FHIR, registra/atualiza no dcm4chee (o <c>POST
    /// /mwlitems</c> exige paciente existente) e cria/atualiza o MWL item a partir da
    /// <see cref="SolicitacaoExame"/> (com TipoExame carregado). Retorna a chave do
    /// item (SPS ID). Lança <see cref="Common.Excecoes.ConflitoException"/> se o
    /// PACS/FHIR estiver indisponível (o chamador retenta com backoff).
    /// </summary>
    Task<string> CriarOuAtualizarMwlItemAsync(SolicitacaoExame solicitacao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma se o MWL item ainda existe no dcm4chee (QIDO por StudyInstanceUID).
    /// <c>true</c> = existe; <c>false</c> = sumiu/expirou; lança
    /// <see cref="Common.Excecoes.ConflitoException"/> se indisponível.
    /// </summary>
    Task<bool> MwlItemExisteAsync(SolicitacaoExame solicitacao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exclui o MWL item do dcm4chee. Retorna <c>true</c> se removido (404 conta como
    /// removido — já não existia). Lança <see cref="Common.Excecoes.ConflitoException"/>
    /// se o PACS estiver indisponível ou recusar (o chamador decide se força).
    /// </summary>
    Task<bool> ExcluirMwlItemAsync(SolicitacaoExame solicitacao, CancellationToken cancellationToken = default);
}
