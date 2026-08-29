namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Qual motor de IA responde pelo robô de atendimento. Trocável pela tela — é o botão de rollback
/// da migração para a API (ADR-0050): se a Messages API der problema, volta para a assinatura sem
/// deploy. Valor estável — não renumerar.
/// </summary>
public enum MotorRobo
{
    /// <summary>Claude Code por ASSINATURA, via serviço Python <c>SMSMais.aiengine</c> (kind
    /// <c>atendimento</c>). Caminho legado, mantido enquanto o piloto da API não fecha.</summary>
    Assinatura = 1,

    /// <summary>Anthropic <b>Messages API</b> chamada direto do .NET, com o loop de tool-use e os
    /// comandos executados in-process. Custo medido por chamada e resposta em segundos.</summary>
    Api = 2,
}
