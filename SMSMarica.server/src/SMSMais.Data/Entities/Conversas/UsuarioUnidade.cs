namespace SMSMais.Data.Entities.Conversas;

/// <summary>
/// Vínculo N:N entre um <see cref="Usuario"/> (agente) e uma <see cref="Unidade"/>. Define a
/// visibilidade das filas do chat: o operador enxerga as conversas das suas unidades. Um agente
/// pode cobrir várias unidades (ex.: USFs + CDT); <see cref="Principal"/> marca a unidade padrão
/// (no máximo uma por usuário) usada ao iniciar novas conversas.
/// </summary>
public class UsuarioUnidade
{
    public Guid UsuarioId { get; set; }
    public Guid UnidadeId { get; set; }

    /// <summary>Unidade padrão do agente (no máximo uma marcada por usuário).</summary>
    public bool Principal { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }

    public Usuario? Usuario { get; set; }
    public Unidade? Unidade { get; set; }
}
