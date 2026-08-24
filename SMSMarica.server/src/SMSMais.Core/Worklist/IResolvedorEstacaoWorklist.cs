using SMSMais.Data.Entities;

namespace SMSMais.Core.Worklist;

/// <summary>Equipamento elegível para executar um exame (unidade executante + modalidade).</summary>
public sealed record EquipamentoCandidato(Guid Id, string Nome, string AeTitle);

/// <summary>
/// Descobre qual estação (AE Title) deve executar um exame, para carimbar o
/// ScheduledStationAETitle (0040,0001) e o WorklistLabel (0074,1202) do item de
/// worklist — o primeiro é o filtro que o equipamento usa no C-FIND MWL, o segundo
/// é a trava equivalente do lado do servidor.
/// </summary>
public interface IResolvedorEstacaoWorklist
{
    /// <summary>
    /// AE Title da estação para <paramref name="exame"/>. Usa o equipamento ESCOLHIDO
    /// (<see cref="ExameImagem.EquipamentoId"/>) quando houver; senão deduz pela unidade
    /// executante + modalidade. Lança <see cref="Common.Excecoes.ConflitoException"/>:
    /// <c>worklist.sem_equipamento</c> quando não há nenhum, e
    /// <c>worklist.equipamento_ambiguo</c> quando há mais de um e ninguém escolheu —
    /// o sistema não sorteia estação.
    /// </summary>
    Task<string> ResolverAsync(ExameImagem exame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Equipamentos elegíveis para o exame (unidade executante + modalidade, ativos e com
    /// AE Title válido). Vazio = nenhum cadastrado; mais de um = a recepção precisa escolher
    /// ao autorizar. Ordenado por nome.
    /// </summary>
    Task<IReadOnlyList<EquipamentoCandidato>> ListarCandidatosAsync(
        ExameImagem exame, CancellationToken cancellationToken = default);
}
