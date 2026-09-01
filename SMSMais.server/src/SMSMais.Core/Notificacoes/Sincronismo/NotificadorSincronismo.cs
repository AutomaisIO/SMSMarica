using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.WhatsApp;

namespace SMSMais.Core.Notificacoes.Sincronismo;

/// <summary>
/// Avisa por WhatsApp quem opera as integrações quando um sincronismo falha ou precisa de gente —
/// CAPTCHA, credencial derrubada, unidade com erro, rodada interrompida.
///
/// <para><b>Por que existe:</b> os motores do SISREG, SER e SERNIT rodam de madrugada e sozinhos.
/// Quando o SISREG pede CAPTCHA, o motor para e <b>ninguém fica sabendo</b> até alguém abrir a tela
/// e reparar que a importação do dia não aconteceu — o que na prática significa descobrir depois
/// que o paciente já perdeu a consulta. O aviso vai para os telefones cadastrados na própria
/// integração.</para>
///
/// <para><b>Onde ficam os telefones:</b> no <c>parametros_json</c> da credencial de cada provedor
/// (<c>telefonesNotificacao</c>), no mesmo lugar em que já moram baseUrl, autoLogin e a agenda do
/// lote. Não é tabela nova de propósito: é configuração da integração, tem o mesmo ciclo de vida da
/// credencial e é apagada junto com ela.</para>
/// </summary>
public interface INotificadorSincronismo
{
    /// <summary>
    /// Manda o aviso aos telefones do provedor. Nunca lança: falhar em avisar não pode derrubar o
    /// motor que estava tentando trabalhar — o erro real já está no log.
    /// </summary>
    /// <param name="provedor">Chave da integração: <c>sisreg</c>, <c>ser</c>, <c>sernit</c>.</param>
    /// <param name="chaveRepeticao">
    /// Identifica o TIPO do aviso para não repetir o mesmo alerta em looping (ex.: <c>captcha</c>).
    /// Nulo desliga o freio — use em resumo de fim de rodada, que é único por natureza.
    /// </param>
    Task NotificarAsync(
        string provedor, string titulo, string detalhe,
        string? chaveRepeticao = null, CancellationToken ct = default);

    /// <summary>Telefones cadastrados para receber avisos deste provedor.</summary>
    Task<IReadOnlyList<string>> ListarTelefonesAsync(string provedor, CancellationToken ct = default);

    /// <summary>Define os telefones do provedor. Lista vazia desliga o aviso.</summary>
    Task<IReadOnlyList<string>> SalvarTelefonesAsync(
        string provedor, IReadOnlyList<string> telefones, CancellationToken ct = default);
}

public sealed class NotificadorSincronismo(
    IIntegracaoCredencialService credenciais,
    IWhatsAppCliente whatsApp,
    IOptions<ComunicacaoPacienteOptions> comunicacao,
    ILogger<NotificadorSincronismo> logger) : INotificadorSincronismo
{
    public const string ChaveTelefones = "telefonesNotificacao";

    /// <summary>
    /// Código da Meta para "fora da janela de 24h" (re-engagement). Fora dela só entra template
    /// aprovado — é o que obriga o passo de reabertura abaixo.
    /// </summary>
    private const string CodigoForaDaJanela = "131047";

    /// <summary>
    /// Enquanto não existe template próprio para relatório técnico, reabrimos a janela com o
    /// template de verificação cadastral, que é genérico o bastante ("temos uma informação sobre
    /// {procedimento}"). O destinatário é o operador da integração, não o paciente.
    /// </summary>
    private const string ProcedimentoDoRelatorio = "relatório de sincronismo";

    /// <summary>
    /// Freio de repetição: o mesmo aviso não sai de novo antes disto. Uma rodada com 30 unidades
    /// quebrando pelo mesmo motivo mandaria 30 mensagens iguais — e um alerta que chega 30 vezes
    /// deixa de ser lido na primeira.
    /// </summary>
    private static readonly TimeSpan IntervaloMinimoRepeticao = TimeSpan.FromMinutes(30);

    private static readonly ConcurrentDictionary<string, DateTime> UltimoEnvio = new();

    /// <summary>
    /// Última reabertura de janela por telefone. <b>Reabrir é mensagem PAGA</b>, e a janela que ela
    /// abre só vale se o destinatário responder — enquanto ele não responde, todo aviso seguinte
    /// falha igual. Sem este freio, uma carga inicial de 40 unidades tentaria reabrir 80 vezes em
    /// poucos minutos: 80 cobranças e 80 vezes o mesmo "temos uma informação" na tela do operador,
    /// que é como se garante que ninguém leia nenhuma.
    /// </summary>
    private static readonly ConcurrentDictionary<string, DateTime> UltimaReabertura = new();

    /// <summary>Intervalo mínimo entre duas tentativas de reabrir a janela do mesmo telefone.</summary>
    private static readonly TimeSpan IntervaloMinimoReabertura = TimeSpan.FromHours(4);

    public async Task NotificarAsync(
        string provedor, string titulo, string detalhe,
        string? chaveRepeticao = null, CancellationToken ct = default)
    {
        try
        {
            if (chaveRepeticao is not null)
            {
                var chave = $"{provedor}|{chaveRepeticao}";
                var agora = DateTime.UtcNow;
                var ultimo = UltimoEnvio.GetValueOrDefault(chave);
                if (agora - ultimo < IntervaloMinimoRepeticao) return;
                UltimoEnvio[chave] = agora;
            }

            var telefones = await ListarTelefonesAsync(provedor, ct);
            if (telefones.Count == 0) return;

            var texto = $"*{provedor.ToUpperInvariant()} — {titulo}*\n\n{detalhe}";

            foreach (var telefone in telefones)
            {
                await EnviarComReaberturaAsync(telefone, texto, ct);
            }
        }
        catch (Exception ex)
        {
            // Avisar é secundário: se falhar, o motor segue e o erro original continua no log.
            logger.LogWarning(ex, "NOTIFICADOR_SINCRONISMO: falha ao avisar sobre {Provedor}.", provedor);
        }
    }

    /// <summary>
    /// Texto livre primeiro; se a janela de 24h estiver fechada, reabre com template e repete.
    ///
    /// <para>Nesta ordem de propósito: o texto livre carrega o relatório inteiro, e o template só
    /// cabe duas variáveis. Tentar o template antes gastaria uma mensagem paga em toda notificação,
    /// inclusive nas 23 horas em que a conversa está aberta.</para>
    /// </summary>
    private async Task EnviarComReaberturaAsync(string telefone, string texto, CancellationToken ct)
    {
        var envio = await whatsApp.EnviarTextoAsync(telefone, texto, ct: ct);
        if (envio.Ok) return;

        if (envio.Erro?.Contains(CodigoForaDaJanela, StringComparison.Ordinal) != true)
        {
            logger.LogWarning(
                "NOTIFICADOR_SINCRONISMO: não foi possível avisar {Telefone}: {Erro}", telefone, envio.Erro);
            return;
        }

        // Já tentamos reabrir há pouco: a janela continua fechada porque o destinatário ainda não
        // respondeu, não porque faltou insistir. Insistir aqui só gera cobrança.
        var agora = DateTime.UtcNow;
        var ultima = UltimaReabertura.GetValueOrDefault(telefone);
        if (agora - ultima < IntervaloMinimoReabertura)
        {
            logger.LogInformation(
                "NOTIFICADOR_SINCRONISMO: janela de {Telefone} fechada e reabertura já tentada — "
                + "aviso não entregue. Basta o destinatário responder a qualquer mensagem.", telefone);
            return;
        }
        UltimaReabertura[telefone] = agora;

        // Nome e idioma do template vêm da configuração de comunicação com o paciente — é lá que
        // eles são mantidos em dia com o que a Meta aprovou. Ler isso NÃO pode derrubar o aviso:
        // um try/catch próprio aqui, e não o genérico lá de cima, porque a diferença entre "a
        // configuração não abriu" e "a Meta recusou" muda o que a pessoa tem de ir consertar.
        string template, idioma;
        try
        {
            var opcoes = comunicacao.Value;
            template = opcoes.TemplateValidacaoCadastro;
            idioma = opcoes.Idioma;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "NOTIFICADOR_SINCRONISMO: não foi possível ler a configuração de comunicação "
                + "para reabrir a janela de {Telefone}.", telefone);
            return;
        }

        var legivel =
            "Olá! Este é o canal oficial da saúde. Temos uma informação sobre "
            + $"*{ProcedimentoDoRelatorio}*.";

        EnvioWhatsAppResultado reabertura;
        try
        {
            reabertura = await whatsApp.EnviarTemplateAsync(
                telefone, template, idioma,
                ["Operador", ProcedimentoDoRelatorio], conteudoLegivel: legivel, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "NOTIFICADOR_SINCRONISMO: erro ao enviar o template {Template} para {Telefone}.",
                template, telefone);
            return;
        }

        if (!reabertura.Ok)
        {
            logger.LogWarning(
                "NOTIFICADOR_SINCRONISMO: template {Template} recusado para {Telefone}: {Erro}",
                template, telefone, reabertura.Erro);
            return;
        }

        logger.LogInformation(
            "NOTIFICADOR_SINCRONISMO: janela de {Telefone} reaberta com o template {Template}.",
            telefone, template);

        // A janela abre com a mensagem entregue, não com a resposta do destinatário: o relatório
        // pode seguir na sequência.
        var segunda = await whatsApp.EnviarTextoAsync(telefone, texto, ct: ct);
        if (!segunda.Ok)
        {
            logger.LogWarning(
                "NOTIFICADOR_SINCRONISMO: janela reaberta mas o relatório não saiu para {Telefone}: {Erro}",
                telefone, segunda.Erro);
        }
    }

    public async Task<IReadOnlyList<string>> ListarTelefonesAsync(
        string provedor, CancellationToken ct = default)
    {
        var json = await LerParametrosAsync(provedor, ct);
        if (json?[ChaveTelefones] is not JsonArray lista) return [];

        return [.. lista
            .Select(n => SoDigitos(n?.GetValue<string>()))
            .Where(t => t.Length >= 10)
            .Distinct(StringComparer.Ordinal)];
    }

    public async Task<IReadOnlyList<string>> SalvarTelefonesAsync(
        string provedor, IReadOnlyList<string> telefones, CancellationToken ct = default)
    {
        var limpos = telefones
            .Select(SoDigitos)
            .Where(t => t.Length is >= 10 and <= 13)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (limpos.Count != telefones.Count(t => !string.IsNullOrWhiteSpace(t)))
        {
            throw new ValidacaoException(
                "integracao.telefone_invalido",
                "Informe telefones com DDD (10 a 13 dígitos, com ou sem o código do país).");
        }

        // Merge: baseUrl, autoLogin e a agenda do lote moram no MESMO JSON — sobrescrever o
        // documento inteiro apagaria a credencial de acesso da integração.
        var atual = await credenciais.ObterAsync(provedor, ct);
        var json = Parse(atual.ParametrosJson) ?? new JsonObject();
        json[ChaveTelefones] = new JsonArray([.. limpos.Select(t => (JsonNode)JsonValue.Create(t)!)]);

        await credenciais.AtualizarAsync(
            provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: null,
                ClientSecret: null,
                RedirectUri: atual.RedirectUri,
                ParametrosJson: json.ToJsonString(),
                Ativo: atual.Ativo),
            ct);

        return limpos;
    }

    private async Task<JsonObject?> LerParametrosAsync(string provedor, CancellationToken ct)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(provedor, ct);
            return Parse(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
            return null; // integração sem credencial: não há para quem avisar
        }
    }

    private static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SoDigitos(string? valor) =>
        new([.. (valor ?? string.Empty).Where(char.IsDigit)]);
}
