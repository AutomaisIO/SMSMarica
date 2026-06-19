namespace SMSMarica.Data.Entities.Integracoes;

/// <summary>
/// Credencial de um provedor externo de login OAuth (Microsoft, Facebook, Google, ...).
/// <c>client_id</c>/<c>client_secret</c> ficam cifrados em repouso (IDataProtector) e são
/// write-only na API (a tela só sabe se já estão definidos). Linha única por
/// <see cref="Provedor"/> (chave estável, minúscula).
/// <para>
/// Guarda as credenciais do <b>app/provedor</b> — NÃO os dados do cidadão (esses ficam em
/// <c>CidadaoAcesso</c>). O fluxo de login social (sessão do cidadão, ADR-0018) consome
/// estas credenciais via service, sem conhecer esta tabela diretamente.
/// </para>
/// </summary>
public class IntegracaoCredencial
{
    public Guid Id { get; set; }

    /// <summary>Chave estável do provedor, minúscula: "microsoft", "facebook", "google".</summary>
    public string Provedor { get; set; } = string.Empty;

    /// <summary><c>client_id</c>/<c>app_id</c> cifrado. Write-only na API.</summary>
    public string? ClientIdCifrado { get; set; }

    /// <summary><c>client_secret</c>/<c>app_secret</c> cifrado. Write-only na API.</summary>
    public string? ClientSecretCifrado { get; set; }

    /// <summary>Parâmetros extras não-sensíveis em JSON (ex.: <c>tenant_id</c> do Microsoft). Públicos.</summary>
    public string? ParametrosJson { get; set; }

    /// <summary>URI de redirecionamento OAuth (pública).</summary>
    public string? RedirectUri { get; set; }

    /// <summary>Provedor habilitado. Quando false, o login social por ele recusa de forma tratada.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
