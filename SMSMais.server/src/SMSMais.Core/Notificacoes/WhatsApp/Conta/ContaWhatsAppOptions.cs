namespace SMSMais.Core.Notificacoes.WhatsApp.Conta;

/// <summary>Seção <c>ContaWhatsApp</c>: o freio e o aviso de conta do WhatsApp parada.</summary>
public sealed class ContaWhatsAppOptions
{
    public const string SecaoConfig = "ContaWhatsApp";

    /// <summary>Com a conta parada, de quanto em quanto tempo UM envio vai à Meta para ver se voltou.</summary>
    public int IntervaloSondaMinutos { get; set; } = 10;

    /// <summary>De quanto em quanto tempo se pergunta à Meta (pelo Automais.Zap) a saúde do número.</summary>
    public int IntervaloConsultaSaudeMinutos { get; set; } = 5;

    /// <summary>
    /// Logins que veem a faixa vermelha de conta parada. Vazio = quem tem acesso global. Fica no
    /// ambiente do servidor (<c>ContaWhatsApp__FaixaParaLogins__0=...</c>), não aqui: é de cada instância.
    /// </summary>
    public string[] FaixaParaLogins { get; set; } = [];
}
