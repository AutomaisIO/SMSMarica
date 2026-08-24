using SMSMarica.Core.Associacoes.Dtos;
using SMSMarica.Core.Worklist;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Associacoes;

/// <summary>Desfecho da conciliação de um study do PACS com as solicitações abertas.</summary>
public enum ResultadoConciliacao
{
    /// <summary>Nenhuma solicitação aberta casa (por AccessionNumber nem por PatientID).</summary>
    SemSolicitacao,

    /// <summary>Study já tinha vínculo (associação explícita ativa) — nada a fazer.</summary>
    JaConciliada,

    /// <summary>Vínculo estabelecido nesta chamada: promoveu a Realizada (worklist) ou associou.</summary>
    Conciliada,
}

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

    /// <summary>
    /// Concilia um study do PACS com as solicitações abertas, casando pelas chaves duráveis
    /// em ordem de confiança: <b>AccessionNumber</b> do DICOM (worklist carrega o nº SMS),
    /// <b>PatientID</b> = nº SMS (técnico digitou o número no campo do paciente) e
    /// <b>StudyInstanceUID</b> pré-gerado (worklist que voltou sem accession). Nenhuma depende
    /// de QUANDO a solicitação foi criada. Worklist genuíno (StudyUID herdado) só promove a
    /// Realizada (vínculo implícito); demais casos criam associação explícita. Idempotente:
    /// study já vinculado retorna <see cref="ResultadoConciliacao.JaConciliada"/>.
    /// </summary>
    Task<ResultadoConciliacao> ConciliarStudyAsync(EstudoPacsRecente estudo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Concilia um LOTE de studies: pré-filtra em 2 queries os já vinculados (caso dominante
    /// em regime — o poller repassa a mesma janela a cada 30s) e delega o restante a
    /// <see cref="ConciliarStudyAsync"/>. Falha de um study não derruba o lote.
    /// </summary>
    Task<ConciliacaoLoteResultado> ConciliarLoteAsync(
        IReadOnlyList<EstudoPacsRecente> estudos, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resincronização sob demanda (rede de segurança): varre os studies recentes do PACS
    /// (janela ampla, pela DATA DO EXAME) e concilia cada um via
    /// <see cref="ConciliarStudyAsync"/>. Idempotente. Recupera exames órfãos.
    /// </summary>
    Task<ResincronizacaoResultadoDto> ResincronizarAsync(CancellationToken cancellationToken = default);
}
