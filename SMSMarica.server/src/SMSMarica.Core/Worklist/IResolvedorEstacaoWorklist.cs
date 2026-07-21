using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Worklist;

/// <summary>
/// Descobre qual estação (AE Title) deve executar um exame, para carimbar o
/// ScheduledStationAETitle (0040,0001) do item de worklist — o filtro que o
/// equipamento usa no C-FIND MWL.
/// </summary>
public interface IResolvedorEstacaoWorklist
{
    /// <summary>
    /// AE Title da estação para <paramref name="exame"/>: o
    /// <see cref="Equipamento.IdentificadorDicom"/> do equipamento ativo da unidade
    /// executante na modalidade do exame. Sem equipamento cadastrado (ou só com
    /// identificador inválido), lança
    /// <see cref="Common.Excecoes.ConflitoException"/> <c>worklist.sem_equipamento</c>
    /// — o exame fica com o erro visível e o worker retenta depois do cadastro.
    /// </summary>
    Task<string> ResolverAsync(ExameImagem exame, CancellationToken cancellationToken = default);
}
