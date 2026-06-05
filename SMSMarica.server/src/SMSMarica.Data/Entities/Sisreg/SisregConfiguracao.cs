using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Sisreg;

/// <summary>
/// Configuração global (linha única) da integração com a API-SISREG (DATASUS).
/// Credenciais (senha/token) ficam cifradas em repouso (IDataProtector) e são
/// write-only na API. Parametriza UF/município/centrais para que a integração
/// seja reutilizável por outra prefeitura trocando só a configuração — ver ADR-0012.
/// </summary>
public class SisregConfiguracao
{
    public Guid Id { get; set; }

    /// <summary>Host base do Elasticsearch SISREG. Ex.: <c>https://sisreg-es.saude.gov.br/</c>.</summary>
    public string BaseUrl { get; set; } = "https://sisreg-es.saude.gov.br/";

    /// <summary>Municipal (índice por UF+município) ou Nacional (sufixo <c>-nacional</c>).</summary>
    public EscopoSisreg Escopo { get; set; } = EscopoSisreg.Municipal;

    /// <summary>UF da credencial (ex.: "RJ"). Compõe o nome do índice no escopo municipal.</summary>
    public string Uf { get; set; } = string.Empty;

    /// <summary>Código do município (IBGE, ex.: "3302700" Maricá). Compõe o índice no escopo municipal.</summary>
    public string Municipio { get; set; } = string.Empty;

    /// <summary>
    /// Códigos das centrais reguladoras (campo <c>codigo_central_reguladora</c>), separados
    /// por vírgula. Aplicados como filtro <c>terms</c> em todas as consultas.
    /// </summary>
    public string CentraisReguladoras { get; set; } = string.Empty;

    public TipoAutenticacaoSisreg TipoAutenticacao { get; set; } = TipoAutenticacaoSisreg.Basic;

    /// <summary>Login (usado no esquema Basic).</summary>
    public string? Login { get; set; }

    /// <summary>Senha cifrada (esquema Basic). Write-only na API.</summary>
    public string? SenhaCifrada { get; set; }

    /// <summary>Token cifrado (esquema Bearer/ApiKey). Write-only na API.</summary>
    public string? TokenCifrado { get; set; }

    /// <summary>Integração habilitada. Quando false, as consultas recusam de forma tratada.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
