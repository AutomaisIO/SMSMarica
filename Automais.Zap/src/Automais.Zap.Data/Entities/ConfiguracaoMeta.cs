namespace Automais.Zap.Data.Entities;

/// <summary>
/// Credenciais do App único da Automais na Meta. Linha única (PK fixa).
///
/// Ficam no banco, e não só em env var, porque a configuração tem de ser feita **pela própria
/// plataforma** — quem cadastra um cliente novo não deveria precisar de SSH. Os três segredos
/// são cifrados com o anel de Data Protection do relay.
///
/// O env continua valendo como bootstrap: campo vazio aqui cai no <c>Meta__*</c>. É o que
/// impede o serviço de ficar inacessível se alguém salvar a tela pela metade.
/// </summary>
public sealed class ConfiguracaoMeta
{
    public static readonly Guid IdSingleton = new("00000000-0000-0000-0000-00000000fffd");

    public Guid Id { get; set; } = IdSingleton;

    /// <summary>ID do App na Meta. Não é segredo.</summary>
    public string? AppId { get; set; }

    /// <summary>Confere o HMAC do webhook e assina o recorte quando o lote tem mais de um dono.</summary>
    public string? AppSecretCifrado { get; set; }

    /// <summary>Combinado com a Meta no handshake do webhook.</summary>
    public string? VerifyTokenCifrado { get; set; }

    /// <summary>
    /// Token do System User. Usado SÓ pelo console de gestão (ler WABAs, inscrever o App,
    /// templates). O relay não envia mensagem, então o caminho do webhook nunca toca nele.
    /// </summary>
    public string? TokenSistemaCifrado { get; set; }

    /// <summary>Base da Graph API, com a versão. Ex.: <c>https://graph.facebook.com/v21.0/</c>.</summary>
    public string BaseUrl { get; set; } = "https://graph.facebook.com/v21.0/";

    public DateTimeOffset? AtualizadoEm { get; set; }
}
