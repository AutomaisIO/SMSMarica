using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Configuração singleton do módulo de tickets. Hoje guarda a regra de visibilidade,
/// alterável pelo admin global a qualquer momento. Linha única com Id fixo.
/// </summary>
public sealed class TicketConfiguracao
{
    /// <summary>Id fixo da linha singleton.</summary>
    public static readonly Guid IdSingleton = new("77777777-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = IdSingleton;

    public TicketVisibilidade Visibilidade { get; set; } = TicketVisibilidade.Privado;

    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }

    public uint RowVersion { get; set; }
}
