namespace SMSMarica.Data.Entities;

/// <summary>
/// Papel profissional de <see cref="Usuario"/> (1:1). Carrega apenas campos
/// específicos de médico (CRM, especialidade). Dados pessoais base vivem em
/// <see cref="Usuario"/>. Ver ADR-0005 + ADR-0006.
/// </summary>
public class Medico
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>Número do CRM (apenas dígitos).</summary>
    public string Crm { get; set; } = string.Empty;

    /// <summary>UF do CRM (2 letras).</summary>
    public string UfCrm { get; set; } = string.Empty;

    /// <summary>Especialidade principal (opcional).</summary>
    public string? Especialidade { get; set; }

    /// <summary>Registro de Qualificação de Especialista (opcional).</summary>
    public string? Rqe { get; set; }

    /// <summary>Data de validade do CRM (opcional).</summary>
    public DateOnly? ValidadeCrm { get; set; }

    // Auditoria (sem flag Ativo — Usuario.Ativo trata acesso; ExcluidoEm trata exclusão).
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
