namespace SMSMarica.Data.Entities;

/// <summary>
/// Registro (histórico) de aceite do termo de consentimento LGPD pelo cidadão.
/// O acesso ao app exige um consentimento <b>ativo</b> (não revogado) da
/// <b>versão vigente</b> do termo. Mantemos histórico para provar O QUÊ e QUANDO
/// foi aceito (<see cref="Versao"/> + <see cref="TextoHash"/>), exigência da LGPD.
/// </summary>
public class CidadaoConsentimento
{
    public Guid Id { get; set; }

    public Guid CidadaoAcessoId { get; set; }
    public CidadaoAcesso CidadaoAcesso { get; set; } = null!;

    /// <summary>Versão do termo aceito (ex.: <c>1.0</c>).</summary>
    public string Versao { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) do texto exato que foi aceito — prova de integridade.</summary>
    public string TextoHash { get; set; } = string.Empty;

    public DateTime AceitoEm { get; set; }
    public string? Ip { get; set; }
    public string? Dispositivo { get; set; }

    /// <summary>Null = consentimento ativo. Preenchido = revogado pelo titular.</summary>
    public DateTime? RevogadoEm { get; set; }
}
