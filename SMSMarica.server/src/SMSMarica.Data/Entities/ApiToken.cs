namespace SMSMarica.Data.Entities;

/// <summary>
/// Token de API (chave de serviço) usado por integrações externas — ex.: o
/// CentralIA chamando os endpoints de <c>/integracoes</c> sem JWT de usuário.
/// O token em claro é mostrado UMA ÚNICA VEZ na criação; só o hash (SHA-256)
/// fica persistido. Revogação é manual (sem expiração automática).
/// </summary>
public class ApiToken
{
    public Guid Id { get; set; }

    /// <summary>Nome/origem do token (ex.: "CentralIA"). Identifica o consumidor.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Hash SHA-256 (hex) do token. UNIQUE. O token em claro nunca é guardado.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Prefixo legível do token (ex.: "smk_a1b2c3") só para exibir na lista.</summary>
    public string Prefixo { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }

    /// <summary>Última vez que o token foi usado com sucesso (best-effort).</summary>
    public DateTime? UltimoUsoEm { get; set; }

    public DateTime? RevogadoEm { get; set; }
    public Guid? RevogadoPor { get; set; }
}
