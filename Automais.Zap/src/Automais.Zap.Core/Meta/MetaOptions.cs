namespace Automais.Zap.Core.Meta;

/// <summary>
/// Credenciais do App único da Automais na Meta. Vêm de variável de ambiente
/// (<c>Meta__AppSecret</c>, <c>Meta__VerifyToken</c>), não do banco: são lidas no caminho
/// quente de cada webhook, e não têm por que existir uma consulta para isso.
/// </summary>
public sealed class MetaOptions
{
    public const string Secao = "Meta";

    /// <summary>ID do App. Nao e segredo, mas anda junto.</summary>
    public string? AppId { get; set; }

    /// <summary>App Secret. Usado para conferir a assinatura que a Meta manda.</summary>
    public string? AppSecret { get; set; }

    /// <summary>Token combinado no painel da Meta para o handshake do webhook.</summary>
    public string? VerifyToken { get; set; }
}
