namespace SMSMais.Core.Notificacoes.WhatsApp;

public sealed record EnvioWhatsAppResultado(bool Ok, string? WaMessageId, string? Erro);

/// <summary>
/// Quem provocou o envio. É o que permite a guarda central de LGPD distinguir "o sistema resolveu
/// falar com o cidadão" de "alguém pediu esta mensagem".
/// </summary>
public enum OrigemEnvioWhatsApp
{
    /// <summary>O sistema iniciou a conversa (worker, gatilho, agendador). <b>Padrão de propósito</b>:
    /// quem esquecer de declarar cai na regra mais restritiva. É o único bloqueado por contato negado.</summary>
    Automatico = 1,

    /// <summary>Resposta a uma mensagem que o cidadão acabou de mandar (máquinas de estado, robô).
    /// Não é bloqueada — quem escreveu espera resposta —, mas nunca deve revelar dado de paciente
    /// cujo contato foi negado (isso é barrado antes, na resolução do paciente).</summary>
    Resposta = 2,

    /// <summary>Pedida por uma pessoa: operador no painel, ou o próprio cidadão (código de acesso).</summary>
    Humano = 3,
}

/// <summary>Código de erro devolvido quando a guarda de contato negado barra o envio.</summary>
public static class BloqueioEnvioWhatsApp
{
    public const string CodigoNumeroNegado = "bloqueio.numero_negado";
}

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
/// <param name="Exemplos">
/// Exemplos de cada variável, na ordem ({{1}}, {{2}}…), vindos do <c>example.body_text</c>
/// aprovado na Meta. Alimentam os placeholders da tela de nova conversa.
/// </param>
public sealed record TemplateWhatsApp(
    string Nome,
    string Idioma,
    string Categoria,
    string? Corpo,
    int Parametros,
    IReadOnlyList<string> Exemplos);

/// <summary>
/// Cliente do canal WhatsApp via Automais.Zap. Token de tenant e PhoneNumberId vêm da configuração cifrada
/// (<see cref="Tfd.Configuracao.ITfdConfigService"/>). Cada envio é auditado em
/// <c>whatsapp_mensagem</c>. Mensagens iniciadas pelo sistema (fora da janela de 24h)
/// exigem template HSM aprovado; dentro da janela, texto livre.
/// </summary>
public interface IWhatsAppCliente
{
    Task<EnvioWhatsAppResultado> EnviarTextoAsync(
        string telefone, string texto, Guid? pacienteId = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico);

    /// <param name="conteudoLegivel">Texto humano do template (com as variáveis já preenchidas) para
    /// gravar como conteúdo da mensagem — aparece na thread e no histórico do robô. Nulo mantém o
    /// marcador <c>[template:nome] param | param</c>.</param>
    Task<EnvioWhatsAppResultado> EnviarTemplateAsync(
        string telefone, string template, string idiomaBcp47, IReadOnlyList<string> parametros,
        Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico);

    /// <summary>
    /// Envia um template da categoria <b>AUTHENTICATION</b> (OTP). Diferente de um template
    /// comum, o código entra <b>duas vezes</b>: no corpo (<c>{{1}}</c>) e no botão de copiar
    /// (<c>sub_type: url, index: 0</c>) — exigência da Meta para templates de autenticação.
    /// O código nunca é gravado em claro na auditoria.
    /// </summary>
    Task<EnvioWhatsAppResultado> EnviarTemplateAutenticacaoAsync(
        string telefone, string template, string idiomaBcp47, string codigo,
        Guid? pacienteId = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico);

    /// <summary>
    /// Envia um template com componentes de botão (URL dinâmica e/ou quick reply), além dos
    /// parâmetros do corpo. Os botões devem vir na ordem configurada no template (posição = index).
    /// </summary>
    /// <param name="conteudoLegivel">Texto humano do template (variáveis preenchidas) gravado como
    /// conteúdo da mensagem — thread/histórico legíveis. Nulo mantém o marcador técnico.</param>
    Task<EnvioWhatsAppResultado> EnviarTemplateComBotoesAsync(
        string telefone, string template, string idiomaBcp47,
        IReadOnlyList<string> parametrosBody, IReadOnlyList<BotaoTemplateWhatsApp> botoes,
        Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico);

    /// <summary>
    /// Envia mensagem interativa com botões de resposta (só dentro da janela de 24h — fora dela
    /// a Meta rejeita). O id escolhido volta no webhook em <c>interactive.button_reply.id</c>.
    /// Máximo 3 botões.
    /// </summary>
    Task<EnvioWhatsAppResultado> EnviarInterativoBotoesAsync(
        string telefone, string texto, IReadOnlyList<BotaoInterativoWhatsApp> botoes,
        Guid? pacienteId = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico);

    /// <summary>
    /// Lista os templates <b>APPROVED</b> da WABA (catálogo Meta), para o operador escolher ao
    /// iniciar uma conversa. Cacheado em memória (TTL curto). Retorna vazio em modo simulado ou
    /// quando a integração não está configurada.
    /// </summary>
    Task<IReadOnlyList<TemplateWhatsApp>> ListarTemplatesAsync(CancellationToken ct = default);
}
