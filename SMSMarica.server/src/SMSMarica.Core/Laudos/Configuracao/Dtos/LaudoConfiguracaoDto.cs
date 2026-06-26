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

/// <summary>
/// Só as regras de INICIAR o laudo (sem o cabeçalho/rodapé). Leitura leve, liberada a
/// qualquer usuário autenticado — o front (botão "Laudar") precisa delas mesmo sem a
/// permissão de Configuração de Laudo.
/// </summary>
public sealed record RegrasIniciarLaudoDto(
    bool PermitirLaudarSemAssociacao,
    bool PermitirLaudarSemAnamnese);
