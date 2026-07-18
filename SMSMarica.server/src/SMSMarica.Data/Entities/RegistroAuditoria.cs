namespace SMSMarica.Data.Entities;

/// <summary>
/// Trilha de auditoria de ações de usuário (append-only). Registra "quem
/// alterou o quê, de qual valor para qual, e quando" — rastro do sistema para
/// investigação. Genérico e desacoplado: a entidade auditada é identificada por
/// <see cref="Entidade"/> + <see cref="EntidadeId"/> como strings, sem FK, pois
/// pode viver em outro schema/serviço (ex.: Paciente vive no hub FHIR).
///
/// É um log imutável: nunca sofre UPDATE/DELETE — por isso não carrega o bloco
/// de auditoria por-linha (Criado/Atualizado/Excluido) das demais entidades.
/// </summary>
public sealed class RegistroAuditoria
{
    public Guid Id { get; set; }

    /// <summary>Tipo da entidade auditada (ex.: "Paciente"). Use <c>nameof</c>.</summary>
    public string Entidade { get; set; } = string.Empty;

    /// <summary>Identificador da entidade auditada (string p/ suportar Guid do hub FHIR).</summary>
    public string EntidadeId { get; set; } = string.Empty;

    /// <summary>Ação realizada (ex.: "AlteracaoNome"). Vocabulário livre por ora.</summary>
    public string Acao { get; set; } = string.Empty;

    /// <summary>Valor antes da alteração (null quando não se aplica).</summary>
    public string? ValorAnterior { get; set; }

    /// <summary>Valor depois da alteração (null quando não se aplica).</summary>
    public string? ValorNovo { get; set; }

    /// <summary>Usuário que executou a ação (null em jobs/seed sem contexto autenticado).</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>Nome do usuário no momento do registro (desnormalizado p/ leitura rápida).</summary>
    public string? UsuarioNome { get; set; }

    /// <summary>IP de onde a ação partiu (X-Forwarded-For já honrado). Null em jobs/seed ou
    /// registros anteriores à captura.</summary>
    public string? Ip { get; set; }

    public DateTime CriadoEm { get; set; }
}
