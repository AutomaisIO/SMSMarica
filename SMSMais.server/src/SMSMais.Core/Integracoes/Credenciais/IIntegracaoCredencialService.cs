using SMSMais.Core.Integracoes.Credenciais.Dtos;

namespace SMSMais.Core.Integracoes.Credenciais;

/// <summary>
/// Credenciais (client_id/client_secret) dos provedores de login OAuth, cifradas em
/// repouso e write-only. A tela (staff, RBAC IntegracoesConfig) só vê flags de
/// "definido". O fluxo de login social do cidadão consome <see cref="ObterContextoAsync"/>
/// (segredos revelados) — separação limpa do território do cidadão (ADR-0018).
/// </summary>
public interface IIntegracaoCredencialService
{
    /// <summary>Lista os provedores suportados, criando placeholders "não configurado" para os que faltam.</summary>
    Task<IReadOnlyList<IntegracaoCredencialDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<IntegracaoCredencialDto> ObterAsync(string provedor, CancellationToken cancellationToken = default);

    /// <summary>Upsert: cifra apenas os campos preenchidos; vazios mantêm o valor atual.</summary>
    Task AtualizarAsync(string provedor, AtualizarIntegracaoCredencialRequest request, CancellationToken cancellationToken = default);

    /// <summary>Limpa as credenciais e desativa o provedor (mantém a linha + auditoria).</summary>
    Task LimparAsync(string provedor, CancellationToken cancellationToken = default);

    /// <summary>Contexto com os segredos revelados — só para uso interno (login social no backend).</summary>
    Task<IntegracaoCredencialContexto> ObterContextoAsync(string provedor, CancellationToken cancellationToken = default);
}

/// <summary>Credenciais reveladas de um provedor, para uso interno do backend.</summary>
public sealed record IntegracaoCredencialContexto(
    string Provedor,
    string? ClientId,
    string? ClientSecret,
    string? RedirectUri,
    string? ParametrosJson,
    bool Ativo);

/// <summary>Provedores de login OAuth suportados pela tela de credenciais (chave estável + rótulo).</summary>
public static class ProvedoresIntegracao
{
    public static readonly IReadOnlyDictionary<string, string> Suportados = new Dictionary<string, string>
    {
        ["microsoft"] = "Microsoft (Azure AD)",
        ["facebook"] = "Facebook",
        ["google"] = "Google (login)",
        // Armazenamento de objeto (S3) para os PDFs de exame.
        // clientId=accessKey, clientSecret=secretKey, parametrosJson={endpoint,region,bucket}.
        ["digitalocean_spaces"] = "DigitalOcean Spaces (S3)",
        // SISREG III (web scraping): consulta de paciente por CNS (CADSUS).
        // clientId=usuário (operador), clientSecret=senha, parametrosJson={baseUrl}.
        ["sisreg"] = "SISREG (consulta de paciente por CNS)",
        // SER — Sistema Estadual de Regulação (SES-RJ), web scraping SOMENTE LEITURA (ADR-0042).
        // clientId=usuário (operador), clientSecret=senha, parametrosJson={baseUrl}.
        // Cadastrada pela aba SER de Regulação → Configuração, não por esta tela.
        ["ser"] = "SER — Sistema Estadual de Regulação (SES-RJ)",
        // SERNIT — SER de Niterói (regulacao.niteroi.rj.gov.br), mesma stack, instância própria.
        // clientId=usuário (operador), clientSecret=senha, parametrosJson={baseUrl}.
        ["sernit"] = "SERNIT — SER de Niterói",
        // NB: o conector web do Klinikos NÃO é um provedor de credencial de serviço (não é
        // integração tipo Google/Spaces). É uma FONTE DE PRONTUÁRIO — configurada em
        // "Importar Prontuários → Fontes/Conectores" como uma IaFonte (Tipo=KlinikosWeb), com
        // URL/usuário/senha próprios. Por isso não aparece aqui.
    };

    public static bool EhSuportado(string provedor) => Suportados.ContainsKey(provedor);

    public static string Rotulo(string provedor) =>
        Suportados.TryGetValue(provedor, out var r) ? r : provedor;
}
