namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>Um turno da conversa alimentado ao motor (papel: "cidadao" | "atendente" | "robo").</summary>
public sealed record MensagemHistoricoRobo(string Papel, string Texto);

/// <summary>Entrada do motor para gerar UMA resposta do robô.</summary>
public sealed record EntradaMotorRobo(
    string ChaveSessao,
    Guid ConversaId,
    Guid? PacienteId,
    Guid? AssuntoId,
    string Modelo,
    string InstrucaoSistema,
    IReadOnlyList<string> ComandosHabilitados,
    IReadOnlyList<MensagemHistoricoRobo> Historico,
    string MensagemAtual,
    bool DentroDoHorario);

/// <summary>Resposta estruturada do motor (a ferramenta terminal <c>responder_cidadao</c>).
/// Tokens/custo do turno vêm do <c>ResultMessage</c> do aiengine (nulos se indisponíveis).</summary>
public sealed record RespostaMotorRobo(
    string Texto,
    bool HandOff,
    string? MotivoHandOff,
    double? Confianca,
    long? TokensEntrada = null,
    long? TokensSaida = null,
    decimal? CustoUsd = null);

/// <summary>
/// Fala com o motor de IA (aiengine, kind <c>atendimento</c>) e devolve a resposta do robô.
/// Costura fina para o .NET compilar/testar sem o motor de pé.
/// </summary>
public interface IRoboAtendimentoMotor
{
    Task<RespostaMotorRobo> ResponderAsync(EntradaMotorRobo entrada, CancellationToken ct);
}
