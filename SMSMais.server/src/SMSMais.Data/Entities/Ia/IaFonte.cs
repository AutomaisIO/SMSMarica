using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Ia;

/// <summary>
/// Base de dados (alvo) gerenciável pela UI. Ex.: "Salux PRODUCAO", "Salux TREINAMENTO".
/// A connection string é montada em runtime a partir de host/porta/serviço/usuário/senha
/// (senha cifrada). A conta deve ser read-only (ex.: salux_obs).
/// </summary>
public class IaFonte
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Slug curto e estável (multi-base, ADR-0009). Na importação para o hub FHIR ele prefixa os
    /// identifiers internos (cd_paciente, cd_medico, baa, edoc) e compõe o <c>meta.source</c>,
    /// evitando colisão entre instâncias e carregando a proveniência. Único entre bases ativas.
    /// </summary>
    public string? Slug { get; set; }

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

    /// <summary>
    /// Quando true, a base NÃO é alcançada diretamente: um agente proxy roda no servidor de
    /// destino, disca para o smsmarica via WSS (reverso) e executa as consultas localmente.
    /// Nesse modelo <see cref="Host"/>/<see cref="Usuario"/>/<see cref="SenhaCifrada"/> ficam
    /// vazios — as credenciais do banco vivem no <c>.env</c> do destino, nunca aqui. O agente
    /// se identifica pelo <see cref="Slug"/> e autentica com o <see cref="AgenteTokenHash"/>.
    /// Ver ADR-0023.
    /// </summary>
    public bool ViaAgente { get; set; }

    /// <summary>
    /// Hash (SHA-256) do token de conexão do agente. Gerado no cadastro/rotação e mostrado ao
    /// operador uma única vez; guardamos só o hash. Nulo quando <see cref="ViaAgente"/> é false.
    /// </summary>
    public string? AgenteTokenHash { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Família de bases de MESMA estrutura (ex.: <c>klinikos</c> para UPA e Santa Rita). Bases da
    /// mesma família compartilham conhecimento (docs .md) e agrupam as avaliações — o aprendizado
    /// de uma vale para todas. Nula = base sem família (conhecimento só dela). Ver ADR-0023.
    /// </summary>
    public string? Familia { get; set; }

    /// <summary>
    /// Parâmetros específicos do conector, em JSON (nullable). Livre e extensível a novos
    /// conectores de prontuário sem migração por conector. O conector web do Klinikos guarda
    /// aqui <c>{"appRoot","unidCodigo","metaSource","webPrimaria"}</c> — a URL/usuário/senha vão
    /// nos campos próprios (<see cref="BaseUrl"/>/<see cref="Usuario"/>/<see cref="SenhaCifrada"/>).
    /// </summary>
    public string? ParametrosJson { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
