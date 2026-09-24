using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Modo de assinatura de laudo escolhido para um médico (ADR-0061). Separado da
/// <see cref="AssinaturaMedico"/> (rubrica) de propósito: o administrador escolhe o modo
/// antes ou depois de enviar a imagem, e remover a rubrica não apaga a escolha.
/// Keyed pelo id do Practitioner no hub FHIR, sem FK (mesma régua da rubrica).
/// </summary>
public class ConfiguracaoAssinaturaMedico
{
    /// <summary>Id do Practitioner (hub FHIR) — chave primária, uma linha por médico.</summary>
    public Guid MedicoId { get; set; }

    public ModoAssinaturaMedico Modo { get; set; } = ModoAssinaturaMedico.SemCertificado;

    // ---- Auditoria (ADR-0006) ----
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
