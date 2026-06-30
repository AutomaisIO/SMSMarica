namespace SMSMarica.Core.SolicitacoesExame.Declaracao;

/// <summary>
/// Gera a "Declaração de Comparecimento" (PDF) de uma solicitação de exame já
/// realizada — documento que atesta que o(a) usuário(a) compareceu à unidade
/// executora para o exame, com a data/hora reais do estudo no PACS.
/// </summary>
public interface IDeclaracaoComparecimentoService
{
    /// <summary>Bytes do PDF da declaração. Lança se a solicitação não existir ou ainda não estiver realizada.</summary>
    Task<byte[]> GerarAsync(Guid solicitacaoId, CancellationToken cancellationToken = default);
}
