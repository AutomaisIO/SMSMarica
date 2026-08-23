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

/// <summary>
/// Retrato completo de um número na Meta, para a tela de diagnóstico.
///
/// Separado de <see cref="NumeroMeta"/> de propósito: a listagem do WABA é caminho de
/// sincronização e roda para todos os números de uma vez; carregar nela campo que só uma
/// tela olha faria toda sincronização pagar por isso.
///
/// <see cref="Json"/> guarda a resposta crua formatada. Campo que a Meta adicionar amanhã
/// aparece na tela sem precisar de deploy — e quando o valor tipado não bater com o que a
/// Meta devolveu, dá para ver quem está errado.
/// </summary>
public sealed record NumeroDetalheMeta(
    string Id,
    string? DisplayPhoneNumber,
    string? VerifiedName,
    string? NameStatus,
    string? Status,
    string? QualityRating,
    string? PlatformType,
    string? Throughput,
    string? CodeVerificationStatus,
    string? SearchVisibility,
    string? MessagingLimitTier,
    bool? ContaOficial,
    string? ObaStatus,
    bool? NoAppBusiness,
    string Json);

/// <summary>
/// Leitura humana do <c>oba_status</c>.
///
/// A documentação da Meta só publica <c>NOT_STARTED</c> como exemplo — os demais valores são
/// observados, não contratuais. Por isso o <c>default</c> devolve o valor cru em vez de
/// chutar: inventar significado para um status novo é pior do que admitir que não se conhece.
/// </summary>
public static class ObaStatus
{
    public const string NaoIniciado = "NOT_STARTED";

    public static string Explicar(string? status) => (status ?? "").Trim().ToUpperInvariant() switch
    {
        "" => "A Meta não devolveu o campo para este número.",
        NaoIniciado => "Nunca solicitado. Enquanto ficar assim o número não tem selo azul e "
                       + "não aparece na busca do WhatsApp para quem não o tem nos contatos.",
        "PENDING" => "Solicitado e em análise pela Meta. A resposta costuma sair em alguns dias.",
        "APPROVED" => "Aprovado — o número exibe o selo azul.",
        "REJECTED" => "Negado. A Meta só aceita nova solicitação 30 dias depois da negativa.",
        var outro => $"Status \"{outro}\" — não documentado pela Meta; confira no Gerenciador do WhatsApp.",
    };

    /// <summary>Verde só quando aprovado; amarelo enquanto está em análise.</summary>
    public static string Selo(string? status) => (status ?? "").Trim().ToUpperInvariant() switch
    {
        "APPROVED" => "on",
        "PENDING" => "atencao",
        _ => "off",
    };
}

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
    string? MotivoRejeicao,
    IReadOnlyList<string> Exemplos);

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
    Task<ResultadoMeta<NumeroDetalheMeta>> ObterNumeroAsync(string phoneNumberId, CancellationToken ct = default);
    Task<ResultadoMeta<IReadOnlyList<AppInscrito>>> ListarAppsInscritosAsync(string wabaId, CancellationToken ct = default);
    Task<ResultadoMeta<bool>> InscreverAppNoWabaAsync(string wabaId, CancellationToken ct = default);
    Task<ResultadoMeta<bool>> DesinscreverAppDoWabaAsync(string wabaId, CancellationToken ct = default);

    // --- Templates ---
    Task<ResultadoMeta<IReadOnlyList<TemplateMeta>>> ListarTemplatesAsync(string wabaId, CancellationToken ct = default);
    Task<ResultadoMeta<string>> CriarTemplateAsync(string wabaId, NovoTemplate template, CancellationToken ct = default);
    Task<ResultadoMeta<bool>> ExcluirTemplateAsync(string wabaId, string nome, CancellationToken ct = default);
}
