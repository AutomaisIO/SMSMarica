namespace SMSMarica.Core.Laudos.Configuracao.Dtos;

/// <summary>Cabeçalho/rodapé institucional global do laudo (HTML + JSON re-editável).</summary>
public sealed record LaudoConfiguracaoDto(
    string CabecalhoHtml,
    string CabecalhoJson,
    string RodapeHtml,
    string RodapeJson,
    DateTime? AtualizadoEm);

/// <summary>Payload para salvar o cabeçalho/rodapé global.</summary>
public sealed record SalvarLaudoConfiguracaoRequest(
    string? CabecalhoHtml,
    string? CabecalhoJson,
    string? RodapeHtml,
    string? RodapeJson);
