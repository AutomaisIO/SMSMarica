using SMSMarica.Core.Integracoes.SisregWeb.Mapeamento.Dtos;

namespace SMSMarica.Core.Integracoes.SisregWeb.Mapeamento;

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

    Task AlternarProfissionalAsync(Guid profissionalId, bool habilitado, CancellationToken cancellationToken = default);

    Task AlternarProcedimentoAsync(Guid procedimentoId, bool habilitado, CancellationToken cancellationToken = default);

    /// <summary>Liga/desliga vários profissionais de uma vez.</summary>
    Task AlternarProfissionaisEmLoteAsync(IReadOnlyList<Guid> ids, bool habilitado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sincroniza os profissionais <b>habilitados</b> com o hub FHIR como <c>Practitioner</c>,
    /// deduplicando por CPF.
    /// </summary>
    Task<SisregSincronizacaoFhirDto> SincronizarFhirAsync(CancellationToken cancellationToken = default);
}
