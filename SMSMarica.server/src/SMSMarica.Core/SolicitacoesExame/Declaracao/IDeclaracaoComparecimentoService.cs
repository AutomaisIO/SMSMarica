namespace SMSMarica.Core.SolicitacoesExame.Declaracao;

/// <summary>
/// Gera a "Declaração de Comparecimento" (PDF) de uma solicitação de exame já
/// realizada — documento que atesta que o(a) usuário(a) compareceu à unidade
/// executora para o exame, com a data/hora reais do estudo no PACS.
/// </summary>
public interface IDeclaracaoComparecimentoService
{
    /// <summary>Bytes do PDF da declaração. Lança se a solicitação não existir ou ainda não estiver realizada.</summary>
    Task<byte[]> GerarAsync(
        Guid solicitacaoId,
        DeclaracaoComparecimentoParametros? parametros = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica um selo de autenticidade (código do QR). Retorna os dados públicos a
    /// exibir (nome, data/hora, descrição, unidade) ou <c>null</c> se o código não existir.
    /// </summary>
    Task<DeclaracaoVerificacaoDto?> VerificarAsync(Guid codigo, CancellationToken cancellationToken = default);
}

/// <summary>Dados públicos exibidos na verificação de autenticidade da declaração.</summary>
public sealed record DeclaracaoVerificacaoDto(string Nome, DateTime DataHora, string Descricao, string? Unidade);

/// <summary>
/// Campos editáveis (informados pela atendente) impressos na declaração de
/// comparecimento: hora de entrada, hora de saída e o motivo. Quando ausentes,
/// o serviço cai para a data/hora real do estudo no PACS.
/// </summary>
public sealed record DeclaracaoComparecimentoParametros(
    DateTime? HoraEntrada = null,
    DateTime? HoraSaida = null,
    string? Motivo = null);
