using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Ia;

/// <summary>
/// Base de dados (alvo) gerenciável pela UI. Ex.: "Salux PRODUCAO", "Salux TREINAMENTO".
/// A connection string é montada em runtime a partir de host/porta/serviço/usuário/senha
/// (senha cifrada). A conta deve ser read-only (ex.: salux_obs).
/// </summary>
public class IaFonte
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TipoFonte Tipo { get; set; }
    public DialetoSql Dialeto { get; set; }
    public AmbienteFonte Ambiente { get; set; }

    public string? Host { get; set; }
    public int? Porta { get; set; }

    /// <summary>Nome do serviço/SID (Oracle) ou database (Postgres).</summary>
    public string? Servico { get; set; }

    public string? Usuario { get; set; }

    /// <summary>Senha da conta read-only, cifrada (IDataProtector). Write-only na API.</summary>
    public string? SenhaCifrada { get; set; }

    /// <summary>Base URL, para fontes acessadas via API (ex.: FHIR).</summary>
    public string? BaseUrl { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
