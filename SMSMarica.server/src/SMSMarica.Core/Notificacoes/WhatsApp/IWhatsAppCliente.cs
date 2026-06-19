namespace SMSMarica.Core.Notificacoes.WhatsApp;

public sealed record EnvioWhatsAppResultado(bool Ok, string? WaMessageId, string? Erro);

/// <summary>
/// Cliente do WhatsApp Cloud API (Meta). Token/PhoneNumberId vêm da configuração cifrada
/// (<see cref="Tfd.Configuracao.ITfdConfigService"/>). Cada envio é auditado em
/// <c>tfd_mensagem_whatsapp</c>. Mensagens iniciadas pelo sistema (fora da janela de 24h)
/// exigem template HSM aprovado; dentro da janela, texto livre.
/// </summary>
public interface IWhatsAppCliente
{
    Task<EnvioWhatsAppResultado> EnviarTextoAsync(
        string telefone, string texto, Guid? sessaoId = null, Guid? pacienteId = null, CancellationToken ct = default);

    Task<EnvioWhatsAppResultado> EnviarTemplateAsync(
        string telefone, string template, string idiomaBcp47, IReadOnlyList<string> parametros,
        Guid? sessaoId = null, Guid? pacienteId = null, CancellationToken ct = default);
}
