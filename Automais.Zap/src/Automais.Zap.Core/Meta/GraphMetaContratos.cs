namespace Automais.Zap.Core.Meta;

/// <summary>
/// Resultado de uma chamada à Graph API. A Meta responde erro com corpo estruturado e
/// mensagem útil ("(#200) permissão faltando", "número já inscrito"); jogar isso fora e
/// mostrar "erro 400" na tela transformaria cada problema numa investigação.
/// </summary>
public sealed record ResultadoMeta<T>(bool Sucesso, T? Valor, string? Erro)
{
    public static ResultadoMeta<T> Ok(T valor) => new(true, valor, null);
    public static ResultadoMeta<T> Falha(string erro) => new(false, default, erro);
}

public sealed record WabaMeta(string Id, string? Nome, string? StatusRevisao, string? Moeda);

public sealed record NumeroMeta(
    string Id,
    string? DisplayPhoneNumber,
    string? VerifiedName,
    string? QualityRating,
    string? CodeVerificationStatus,
    string? PlatformType);

public sealed record AppInscrito(string Id, string? Nome);

public sealed record AssinaturaWebhook(string Objeto, string? CallbackUrl, bool Ativo, IReadOnlyList<string> Campos);

public sealed record TemplateMeta(
    string Id,
    string Nome,
    string Idioma,
    string Categoria,
    string Status,
    string? Corpo,
    int Parametros,
    string? MotivoRejeicao);

/// <summary>Dados mínimos para submeter um template à aprovação da Meta.</summary>
public sealed record NovoTemplate(
    string Nome,
    string Idioma,
    string Categoria,
    string Corpo,
    string? Cabecalho,
    string? Rodape,
    IReadOnlyList<string> ExemplosCorpo);

public interface IGraphMetaClient
{
    // --- App (usam token de app: {app-id}|{app-secret}) ---
    Task<ResultadoMeta<IReadOnlyList<AssinaturaWebhook>>> ObterWebhookDoAppAsync(CancellationToken ct = default);
    Task<ResultadoMeta<bool>> ConfigurarWebhookDoAppAsync(string callbackUrl, IReadOnlyList<string> campos, CancellationToken ct = default);

    // --- WABA (usam o token do System User) ---
    Task<ResultadoMeta<WabaMeta>> ObterWabaAsync(string wabaId, CancellationToken ct = default);
    Task<ResultadoMeta<IReadOnlyList<NumeroMeta>>> ListarNumerosAsync(string wabaId, CancellationToken ct = default);
    Task<ResultadoMeta<IReadOnlyList<AppInscrito>>> ListarAppsInscritosAsync(string wabaId, CancellationToken ct = default);
    Task<ResultadoMeta<bool>> InscreverAppNoWabaAsync(string wabaId, CancellationToken ct = default);
    Task<ResultadoMeta<bool>> DesinscreverAppDoWabaAsync(string wabaId, CancellationToken ct = default);

    // --- Templates ---
    Task<ResultadoMeta<IReadOnlyList<TemplateMeta>>> ListarTemplatesAsync(string wabaId, CancellationToken ct = default);
    Task<ResultadoMeta<string>> CriarTemplateAsync(string wabaId, NovoTemplate template, CancellationToken ct = default);
    Task<ResultadoMeta<bool>> ExcluirTemplateAsync(string wabaId, string nome, CancellationToken ct = default);
}
