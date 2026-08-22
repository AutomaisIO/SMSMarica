namespace SMSMarica.Data.Entities.Notificacoes;

/// <summary>
/// Configuração (linha única) do canal WhatsApp do município.
/// </summary>
/// <remarks>
/// <para>Quem fala com a Meta é o <b>Automais.Zap</b>. Esta instância não tem mais credencial
/// da Meta: token do System User, App Secret, verify token, WABA e URL do Graph saíram daqui
/// quando o App próprio do município foi desativado (ADR-0044).</para>
/// <para>Antiga <c>TfdConfigWhatsApp</c> / <c>tfd_config_whatsapp</c>. O canal é do município e
/// atende todo o sistema — nunca foi do TFD (ADR-0038).</para>
/// </remarks>
public class WhatsAppConfiguracao
{
    public Guid Id { get; set; }

    /// <summary>
    /// Linha pela qual as mensagens saem — o <c>phone_number_id</c> da Meta. Continua aqui
    /// porque é a instância que sabe por qual número ela fala; o resto é do relay.
    /// </summary>
    public string? PhoneNumberId { get; set; }

    /// <summary>Base da API do Automais.Zap.</summary>
    public string? ZapBaseUrl { get; set; }

    /// <summary>Token de tenant emitido pelo Automais.Zap (cifrado). Write-only.</summary>
    public string? ZapTokenCifrado { get; set; }

    /// <summary>
    /// Segredo combinado com o Automais.Zap para conferir o que ELE entrega no nosso webhook
    /// (cifrado). É a única forma de autenticar um evento recebido.
    /// </summary>
    public string? ZapSegredoWebhookCifrado { get; set; }

    /// <summary>
    /// Reservado: desligado, o envio sairia direto para a Meta. Sem credencial da Meta neste
    /// banco, hoje não há para onde voltar — fica como ponto de extensão, não como saída.
    /// </summary>
    public bool ZapAtivo { get; set; }

    /// <summary>Chave geral do canal. Desligada, o sistema não recebe nem envia.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
