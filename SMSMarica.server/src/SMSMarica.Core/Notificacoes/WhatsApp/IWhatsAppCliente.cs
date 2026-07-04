namespace SMSMarica.Core.Notificacoes.WhatsApp;

public sealed record EnvioWhatsAppResultado(bool Ok, string? WaMessageId, string? Erro);

/// <summary>Tipo do botão em um template com componentes de botão.</summary>
public enum TipoBotaoTemplate
{
    /// <summary>Botão de URL dinâmica — <c>Valor</c> é o SUFIXO da URL configurada no template
    /// (ex.: token do magic link em <c>https://app.smsmarica.online/entrar/{{1}}</c>).</summary>
    Url = 1,

    /// <summary>Quick reply — <c>Valor</c> é o payload devolvido no webhook
    /// (<c>messages[].button.payload</c>), máx. 128 chars.</summary>
    QuickReply = 2,
}

/// <summary>Botão do template, na MESMA ordem em que foi configurado na Meta (a posição vira o index).</summary>
public sealed record BotaoTemplateWhatsApp(TipoBotaoTemplate Tipo, string Valor);

/// <summary>Botão de resposta em mensagem interativa (janela de 24h). Título máx. 20 chars.</summary>
public sealed record BotaoInterativoWhatsApp(string Id, string Titulo);

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
    /// Envia um template com componentes de botão (URL dinâmica e/ou quick reply), além dos
    /// parâmetros do corpo. Os botões devem vir na ordem configurada no template (posição = index).
    /// </summary>
    Task<EnvioWhatsAppResultado> EnviarTemplateComBotoesAsync(
        string telefone, string template, string idiomaBcp47,
        IReadOnlyList<string> parametrosBody, IReadOnlyList<BotaoTemplateWhatsApp> botoes,
        Guid? sessaoId = null, Guid? pacienteId = null, CancellationToken ct = default);

    /// <summary>
    /// Envia mensagem interativa com botões de resposta (só dentro da janela de 24h — fora dela
    /// a Meta rejeita). O id escolhido volta no webhook em <c>interactive.button_reply.id</c>.
    /// Máximo 3 botões.
    /// </summary>
    Task<EnvioWhatsAppResultado> EnviarInterativoBotoesAsync(
        string telefone, string texto, IReadOnlyList<BotaoInterativoWhatsApp> botoes,
        Guid? sessaoId = null, Guid? pacienteId = null, CancellationToken ct = default);

    /// <summary>
    /// Lista os templates <b>APPROVED</b> da WABA (catálogo Meta), para o operador escolher ao
    /// iniciar uma conversa. Cacheado em memória (TTL curto). Retorna vazio em modo simulado ou
    /// quando a integração não está configurada.
    /// </summary>
    Task<IReadOnlyList<TemplateWhatsApp>> ListarTemplatesAsync(CancellationToken ct = default);
}
