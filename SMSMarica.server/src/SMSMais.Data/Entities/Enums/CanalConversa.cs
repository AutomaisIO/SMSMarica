namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Canal de origem de uma conversa. Por ora só WhatsApp (via Automais.Zap); o enum existe para
/// que o modelo já suporte outros canais (ex.: web chat) sem migração estrutural.
/// </summary>
public enum CanalConversa
{
    WhatsApp = 1,
}
