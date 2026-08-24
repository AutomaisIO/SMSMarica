using SMSMais.Core.Anamneses.Dtos;

namespace SMSMais.Core.Anamneses;

/// <summary>
/// Anamnese (questionário pré-exame) de uma solicitação. Aberta/reaberta para
/// edição pela enfermagem/atendimento; consultada pelo médico ao laudar.
/// </summary>
public interface IAnamnesesService
{
    /// <summary>
    /// Contexto da tela: pedido + paciente + anamnese existente (null se ainda
    /// não preenchida). Informar <paramref name="solicitacaoExameId"/> OU
    /// <paramref name="accessionNumber"/> (fluxo da linha do exame no PACS).
    /// </summary>
    Task<AnamneseContextoDto> ObterContextoAsync(
        Guid? solicitacaoExameId, string? accessionNumber, CancellationToken cancellationToken = default);

    /// <summary>Cria ou atualiza (upsert) a anamnese da solicitação.</summary>
    Task<AnamneseDto> SalvarAsync(
        Guid solicitacaoExameId, SalvarAnamneseDto dto, CancellationToken cancellationToken = default);
}
