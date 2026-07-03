namespace SMSMarica.Core.Notificacoes.WhatsApp;

public sealed record EnvioWhatsAppResultado(bool Ok, string? WaMessageId, string? Erro);

/// <summary>
/// Template HSM aprovado (catálogo da WABA), usado para iniciar conversas fora da janela de 24h.
/// <see cref="Parametros"/> é o maior índice de <c>{{n}}</c> encontrado no corpo (quantos valores
/// o operador precisa preencher).
/// </summary>
public sealed record TemplateWhatsApp(string Nome, string Idioma, string Categoria, string? Corpo, int Parametros);

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

    /// <summary>
    /// Envia um template da categoria <b>AUTHENTICATION</b> (OTP). Diferente de um template
    /// comum, o código entra <b>duas vezes</b>: no corpo (<c>{{1}}</c>) e no botão de copiar
    /// (<c>sub_type: url, index: 0</c>) — exigência da Meta para templates de autenticação.
    /// O código nunca é gravado em claro na auditoria.
    /// </summary>
    Task<EnvioWhatsAppResultado> EnviarTemplateAutenticacaoAsync(
        string telefone, string template, string idiomaBcp47, string codigo,
        Guid? sessaoId = null, Guid? pacienteId = null, CancellationToken ct = default);

    /// <summary>
    /// Lista os templates <b>APPROVED</b> da WABA (catálogo Meta), para o operador escolher ao
    /// iniciar uma conversa. Cacheado em memória (TTL curto). Retorna vazio em modo simulado ou
    /// quando a integração não está configurada.
    /// </summary>
    Task<IReadOnlyList<TemplateWhatsApp>> ListarTemplatesAsync(CancellationToken ct = default);
}
