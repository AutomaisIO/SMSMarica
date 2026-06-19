using SMSMarica.Core.Integracoes.Credenciais.Dtos;

namespace SMSMarica.Core.Integracoes.Credenciais;

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
    };

    public static bool EhSuportado(string provedor) => Suportados.ContainsKey(provedor);

    public static string Rotulo(string provedor) =>
        Suportados.TryGetValue(provedor, out var r) ? r : provedor;
}
