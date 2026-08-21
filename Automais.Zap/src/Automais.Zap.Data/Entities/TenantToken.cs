namespace Automais.Zap.Data.Entities;

/// <summary>
/// Credencial de API de um tenant. Um tenant pode ter vários — um por sistema que integra,
/// para que revogar um não derrube os outros.
///
/// O token em claro só existe no instante da criação: o que fica guardado é o SHA-256 dele.
/// Vazamento do banco não vira acesso, e "reexibir o token" é impossível por construção, que
/// é o comportamento certo.
/// </summary>
public sealed class TenantToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Para que serve, na tela. Ex.: "SMSMarica produção".</summary>
    public required string Nome { get; set; }

    /// <summary>
    /// Parte pública do token, exibida na tela para o operador saber qual é qual sem
    /// precisar do segredo.
    /// </summary>
    public required string Prefixo { get; set; }

    /// <summary>SHA-256 hex do token completo.</summary>
    public required string Hash { get; set; }

    /// <summary>
    /// Alcance do envio. true = qualquer número do tenant, inclusive os que entrarem depois.
    /// false = só os listados em <see cref="Numeros"/>.
    ///
    /// O recorte é por NÚMERO e não por WABA porque é pelo <c>phone_number_id</c> que a Meta
    /// envia: um token que só pode falar pela linha da Regulação não deve conseguir mandar
    /// pela Central, mesmo estando as duas no mesmo WABA.
    /// </summary>
    public bool TodosNumeros { get; set; } = true;

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? UltimoUsoEm { get; set; }
    public DateTimeOffset? RevogadoEm { get; set; }

    public bool Ativo => RevogadoEm is null;

    public ICollection<TenantTokenNumero> Numeros { get; set; } = [];
}

/// <summary>Recorte do token por número, quando ele não vale para todos.</summary>
public sealed class TenantTokenNumero
{
    public Guid TokenId { get; set; }
    public TenantToken? Token { get; set; }

    public Guid NumeroId { get; set; }
    public Numero? Numero { get; set; }
}
