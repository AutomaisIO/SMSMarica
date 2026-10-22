namespace SMSMais.Data.Entities.Enums;

/// <summary>Quem avisou que o paciente terminou o atendimento e já está no local esperando o resgate.</summary>
public enum CanalAvisoResgate
{
    /// <summary>O próprio paciente, pelo app/PWA do cidadão.</summary>
    Paciente = 1,

    /// <summary>A central, no painel (depois de uma ligação da clínica ou do paciente).</summary>
    Central = 2,

    /// <summary>Um motorista que chegou ao local e viu o paciente esperando (tablet).</summary>
    Motorista = 3,
}
