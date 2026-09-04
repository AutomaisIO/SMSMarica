namespace SMSMais.Core.RoboAtendimento.Dtos;

/// <summary>
/// Abre um item de treinamento. Vem do modal do ícone na bolha do robô (com
/// <see cref="MensagemWhatsAppId"/>) ou direto da tela de treinamento (sem mensagem).
/// </summary>
public sealed record AbrirTreinamentoRequest(
    string Critica,
    Guid? ConversaId = null,
    Guid? MensagemWhatsAppId = null,
    Guid? RoboAssuntoId = null);

/// <summary>Manda o agente treinar este item. A observação é o "mais alguma coisa?" do modal.</summary>
public sealed record TreinarRequest(string? Observacao);

/// <summary>Resposta do humano a uma pendência que travou a análise.</summary>
/// <param name="Autorizado">Só para pendência de alteração de CÓDIGO: autoriza virar
/// desenvolvimento. Ignorado nas de regra de negócio.</param>
public sealed record ResponderPendenciaRequest(string Resposta, bool? Autorizado = null);

/// <summary>Simulação manual: em branco, repete o caso que originou a crítica.</summary>
public sealed record SimularTreinamentoRequest(
    string? Mensagem = null,
    IReadOnlyList<SimularRoboTurnoDto>? Historico = null);

/// <summary>Turno do recorte de conversa congelado junto com a crítica.</summary>
public sealed record TreinamentoContextoTurnoDto(string Papel, string Texto, DateTime? Em);

public sealed record TreinamentoPendenciaDto(
    Guid Id,
    string Tipo,
    string Pergunta,
    string? Contexto,
    IReadOnlyList<string> Opcoes,
    string Status,
    string? Resposta,
    bool? Autorizado,
    DateTime CriadoEm,
    DateTime? RespondidoEm,
    string? RespondidoPorNome);

public sealed record TreinamentoAlteracaoDto(
    Guid Id,
    string Alvo,
    string Operacao,
    Guid RoboAssuntoId,
    string? AssuntoNome,
    Guid AlvoId,
    string? Antes,
    string? Depois,
    string? Justificativa,
    DateTime AplicadoEm,
    DateTime? DesfeitoEm,
    string? DesfeitoPorNome);

public sealed record TreinamentoSimulacaoDto(
    Guid Id,
    string Mensagem,
    string? AssuntoNome,
    string? Resposta,
    IReadOnlyList<RoboSimulacaoChamadaDto> Chamadas,
    string? Veredito,
    string? Analise,
    decimal? CustoUsd,
    long DuracaoMs,
    bool Automatica,
    string? ErroMensagem,
    DateTime CriadoEm,
    string? CriadoPorNome);

/// <summary>Linha da lista de itens de treinamento.</summary>
public sealed record TreinamentoItemResumoDto(
    Guid Id,
    Guid? ConversaId,
    Guid? MensagemWhatsAppId,
    string? AssuntoNome,
    string Critica,
    string? Trecho,
    string Status,
    int PendenciasAbertas,
    int AlteracoesAplicadas,
    string? UltimoVeredito,
    DateTime CriadoEm,
    string? CriadoPorNome);

/// <summary>Item aberto, com tudo que o humano precisa ver e decidir.</summary>
public sealed record TreinamentoItemDto(
    Guid Id,
    Guid? ConversaId,
    Guid? MensagemWhatsAppId,
    Guid? RoboAssuntoId,
    string? AssuntoNome,
    string Critica,
    string? Observacao,
    string? Trecho,
    IReadOnlyList<TreinamentoContextoTurnoDto> Contexto,
    string Status,
    string? Analise,
    string? Modelo,
    decimal? CustoUsd,
    DateTime? AnalisadoEm,
    string? ErroMensagem,
    IReadOnlyList<TreinamentoPendenciaDto> Pendencias,
    IReadOnlyList<TreinamentoAlteracaoDto> Alteracoes,
    IReadOnlyList<TreinamentoSimulacaoDto> Simulacoes,
    DateTime CriadoEm,
    string? CriadoPorNome);
