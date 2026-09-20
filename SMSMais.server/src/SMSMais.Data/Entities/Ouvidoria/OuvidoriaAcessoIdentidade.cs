namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>
/// Registro de cada revelação da identidade de um manifestante sigiloso (Decreto 10.153/2019,
/// art. 6º §3º: o acesso é restrito e cada consulta fica documentada). Append-only; a
/// justificativa é obrigatória e o IP vem do <c>IUsuarioAtualAccessor</c>.
/// </summary>
public sealed class OuvidoriaAcessoIdentidade
{
    public Guid Id { get; set; }
    public Guid ManifestacaoId { get; set; }

    /// <summary>Quem revelou. FK para <c>smsmarica.usuario</c>.</summary>
    public Guid UsuarioId { get; set; }

    public string Justificativa { get; set; } = string.Empty;

    /// <summary>IPv4 ou IPv6 (45 caracteres cobrem IPv6 mapeado).</summary>
    public string? Ip { get; set; }

    public DateTime CriadoEm { get; set; }

    public OuvidoriaManifestacao? Manifestacao { get; set; }
}
