using SMSMais.Core.Integracoes.SisregWeb.Mapeamento.Dtos;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Integracoes.SisregWeb.Mapeamento;

/// <summary>
/// Mapeamento da unidade selecionada: profissionais do SISREG e seus procedimentos, com
/// habilita/desabilita para controlar o que entra na varredura de agenda.
///
/// <para>Todos os métodos exigem UMA unidade selecionada (header X-Unidade-Id).</para>
/// </summary>
public interface ISisregMapeamentoService
{
    /// <summary>Mapeamento persistido da unidade (não vai ao SISREG).</summary>
    Task<SisregMapeamentoDto> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Vai ao SISREG e reconcilia o mapeamento: descobre profissionais e procedimentos,
    /// preserva as habilitações já escolhidas e marca como ausente o que sumiu.
    /// </summary>
    Task<SisregMapeamentoAtualizacaoDto> AtualizarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconcilia o mapeamento de uma unidade <b>explícita</b> (sem depender do header
    /// <c>X-Unidade-Id</c>) e com autor explícito — a porta usada pelo motor em lote fora de uma
    /// request. <paramref name="antesDeCadaRequisicao"/> é chamado antes de cada ida ao SISREG
    /// (o lote usa para respeitar o teto de requisições/hora); passe <c>null</c> no uso interativo.
    /// </summary>
    Task<SisregMapeamentoAtualizacaoDto> AtualizarNoContextoAsync(
        Unidade unidade, Guid? usuarioId, Func<CancellationToken, Task>? antesDeCadaRequisicao,
        CancellationToken cancellationToken = default);

    Task AlternarProfissionalAsync(Guid profissionalId, bool habilitado, CancellationToken cancellationToken = default);

    Task AlternarProcedimentoAsync(Guid procedimentoId, bool habilitado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Liga/desliga o aviso por WhatsApp ao paciente quando este procedimento é importado NESTA
    /// unidade. Aplica a todas as linhas do mesmo procedimento na unidade (ele costuma aparecer
    /// sob vários profissionais) e devolve quantas foram afetadas.
    /// </summary>
    Task<int> AlternarEnvioConfirmacaoAsync(Guid procedimentoId, bool enviar, CancellationToken cancellationToken = default);

    /// <summary>Liga/desliga vários profissionais de uma vez.</summary>
    Task AlternarProfissionaisEmLoteAsync(IReadOnlyList<Guid> ids, bool habilitado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sincroniza os profissionais <b>habilitados</b> com o hub FHIR como <c>Practitioner</c>,
    /// deduplicando por CPF.
    /// </summary>
    Task<SisregSincronizacaoFhirDto> SincronizarFhirAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Núcleo do <see cref="SincronizarFhirAsync"/> com unidade e autor explícitos — para o motor em
    /// lote rodar fora de uma request.
    /// </summary>
    Task<SisregSincronizacaoFhirDto> SincronizarFhirNoContextoAsync(
        Unidade unidade, Guid? usuarioId, CancellationToken cancellationToken = default);
}
