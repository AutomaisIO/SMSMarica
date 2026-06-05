namespace SMSMarica.Data.Entities.Ia;

/// <summary>
/// Histórico de correções — à parte da auditoria comum. Registra cada vez que um erro de
/// query levou a IA a alterar o aprendizado: erro, SQL antes/depois e a instrução gerada.
/// Permite revisar e remover instruções aprendidas automaticamente (campos de revisão/remoção).
/// </summary>
public class IaCorrecao
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }
    public IaConsulta? Consulta { get; set; }

    /// <summary>Aprendizado gerado por esta correção (se virou instrução ativa).</summary>
    public Guid? AprendizadoId { get; set; }
    public IaAprendizado? Aprendizado { get; set; }

    public string ErroOriginal { get; set; } = string.Empty;
    public string? SqlAntes { get; set; }
    public string? SqlDepois { get; set; }
    public string? InstrucaoGerada { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? RevisadoEm { get; set; }
    public Guid? RevisadoPor { get; set; }
    public DateTime? RemovidoEm { get; set; }
    public Guid? RemovidoPor { get; set; }
}
