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

/// <summary>
/// Cabeçalho do modelo, quando ele tem um.
///
/// <para><see cref="Formato"/> é o da Meta (TEXT, IMAGE, VIDEO, DOCUMENT, LOCATION) e decide o
/// que cada envio precisa mandar: modelo com MÍDIA no topo exige o componente de header em
/// <b>toda</b> mensagem — a arte que aparece no modelo aprovado é só exemplo e não vai sozinha.
/// Quem envia sem ela leva <c>(#132012) Parameter format does not match format in the created
/// template</c>.</para>
///
/// <para>O catálogo expõe isto porque a instância não tem credencial da Meta (ADR-0044): sem o
/// formato aqui, o sistema do cliente só descobre que o modelo pede imagem quando a mensagem
/// já falhou na fila.</para>
/// </summary>
/// <param name="Parametros">Variáveis no texto do cabeçalho (só faz sentido em TEXT; a Meta
/// aceita no máximo uma).</param>
/// <param name="Exemplo">O exemplo aprovado: a URL da arte (mídia) ou o texto de amostra.
/// Serve para a tela mostrar o que foi aprovado — <b>não</b> para enviar.</param>
public sealed record CabecalhoTemplateMeta(string Formato, string? Texto, int Parametros, string? Exemplo)
{
    /// <summary>Formatos que exigem um arquivo em cada envio.</summary>
    public bool ExigeMidia => Formato is "IMAGE" or "VIDEO" or "DOCUMENT";
}

public sealed record TemplateMeta(
    string Id,
    string Nome,
    string Idioma,
    string Categoria,
    string Status,
    string? Corpo,
    int Parametros,
    string? MotivoRejeicao,
    IReadOnlyList<string> Exemplos,
    CabecalhoTemplateMeta? Cabecalho = null);

/// <summary>Dados mínimos para submeter um template à aprovação da Meta.</summary>
public sealed record NovoTemplate(
    string Nome,
    string Idioma,
    string Categoria,
    string Corpo,
    string? Cabecalho,
    string? Rodape,
    IReadOnlyList<string> ExemplosCorpo);

/// <summary>
/// Perfil comercial do número, como aparece para o cidadão no WhatsApp. Tudo aqui é
/// editável pela Graph API — é a parte da gestão que antes exigia entrar na Meta.
/// </summary>
public sealed record PerfilNegocioMeta(
    string? Sobre,
    string? Endereco,
    string? Descricao,
    string? Email,
    string? FotoUrl,
    IReadOnlyList<string> Sites,
    string? Vertical);

/// <summary>Campos do perfil a gravar. Campo nulo ou vazio não é enviado (mantém o que está).</summary>
public sealed record AtualizarPerfilNegocio(
    string? Sobre,
    string? Endereco,
    string? Descricao,
    string? Email,
    IReadOnlyList<string> Sites,
    string? Vertical);

/// <summary>
/// Ramos de atividade que a Cloud API aceita em <c>vertical</c>, com o rótulo humano.
/// A lista é contratual da Meta — valor fora dela é recusado com (#100).
/// </summary>
public static class VerticalNegocio
{
    public static readonly IReadOnlyList<(string Valor, string Rotulo)> Opcoes =
    [
        ("UNDEFINED", "— não informado —"),
        ("GOVT", "Governo e serviço público"),
        ("HEALTH", "Saúde"),
        ("EDU", "Educação"),
        ("FINANCE", "Finanças"),
        ("PROF_SERVICES", "Serviços profissionais"),
        ("RETAIL", "Varejo"),
        ("GROCERY", "Mercado e alimentação"),
        ("RESTAURANT", "Restaurante"),
        ("HOTEL", "Hotelaria"),
        ("TRAVEL", "Viagens e turismo"),
        ("AUTO", "Automotivo"),
        ("BEAUTY", "Beleza e cuidado pessoal"),
        ("APPAREL", "Vestuário"),
        ("ENTERTAIN", "Entretenimento"),
        ("EVENT_PLAN", "Eventos"),
        ("NONPROFIT", "Sem fins lucrativos"),
        ("OTHER", "Outro"),
    ];

    public static string Rotulo(string? valor)
        => Opcoes.FirstOrDefault(o => string.Equals(o.Valor, valor, StringComparison.OrdinalIgnoreCase)).Rotulo
           ?? valor ?? "—";
}

/// <summary>Um dia de tráfego do WABA segundo a própria Meta (campo <c>analytics</c>).</summary>
public sealed record PontoAnalytics(DateTimeOffset Inicio, long Enviadas, long Entregues);

/// <summary>
/// Mensagens cobradas e custo por categoria no período (campo <c>pricing_analytics</c>).
/// Desde julho/2025 a Meta cobra por mensagem, não por conversa — <c>conversation_analytics</c>
/// ficou para trás junto com o modelo antigo.
/// </summary>
public sealed record CategoriaCobranca(string Categoria, long Mensagens, decimal Custo);

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

    // --- Perfil comercial do número (o que o cidadão vê no WhatsApp) ---
    Task<ResultadoMeta<PerfilNegocioMeta>> ObterPerfilNegocioAsync(string phoneNumberId, CancellationToken ct = default);
    Task<ResultadoMeta<bool>> AtualizarPerfilNegocioAsync(string phoneNumberId, AtualizarPerfilNegocio dados, CancellationToken ct = default);
    Task<ResultadoMeta<bool>> AtualizarFotoPerfilAsync(string phoneNumberId, byte[] conteudo, string contentType, CancellationToken ct = default);

    // --- Métricas da Meta (WABA) ---
    Task<ResultadoMeta<IReadOnlyList<PontoAnalytics>>> ObterAnalyticsAsync(
        string wabaId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default);
    Task<ResultadoMeta<IReadOnlyList<CategoriaCobranca>>> ObterCobrancaAsync(
        string wabaId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default);
}
