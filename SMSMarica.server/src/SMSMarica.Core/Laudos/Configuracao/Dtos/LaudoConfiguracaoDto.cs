namespace SMSMarica.Core.Laudos.Configuracao.Dtos;

/// <summary>Cabeçalho/rodapé institucional global do laudo + regras de iniciar o laudo.</summary>
public sealed record LaudoConfiguracaoDto(
    string CabecalhoHtml,
    string CabecalhoJson,
    string RodapeHtml,
    string RodapeJson,
    bool PermitirLaudarSemAssociacao,
    bool PermitirLaudarSemAnamnese,
    DateTime? AtualizadoEm);

/// <summary>Payload para salvar o cabeçalho/rodapé global + regras de iniciar o laudo.</summary>
public sealed record SalvarLaudoConfiguracaoRequest(
    string? CabecalhoHtml,
    string? CabecalhoJson,
    string? RodapeHtml,
    string? RodapeJson,
    bool PermitirLaudarSemAssociacao = false,
    bool PermitirLaudarSemAnamnese = false);
