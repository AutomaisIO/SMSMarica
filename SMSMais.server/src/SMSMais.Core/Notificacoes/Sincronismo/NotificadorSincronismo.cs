using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Alertas;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Notificacoes.WhatsApp;

namespace SMSMais.Core.Notificacoes.Sincronismo;

/// <summary>
/// Avisa por WhatsApp quem opera as integrações quando um sincronismo falha ou precisa de gente —
/// CAPTCHA, credencial derrubada, unidade com erro, rodada interrompida.
///
/// <para><b>Por que existe:</b> os motores do SISREG, SER e SERNIT rodam de madrugada e sozinhos.
/// Quando o SISREG pede CAPTCHA, o motor para e <b>ninguém fica sabendo</b> até alguém abrir a tela
/// e reparar que a importação do dia não aconteceu — o que na prática significa descobrir depois
/// que o paciente já perdeu a consulta.</para>
///
/// <para><b>Falha vai pelo aviso da plataforma</b> (<see cref="IAlertaPlataforma"/>, template
/// <c>erro_plataforma</c>), para os telefones da plataforma E os desta integração. Antes ia por
/// texto livre com "reabertura" pelo template de verificação cadastral — e fora da janela de 24h a
/// Meta aceita o texto e só descarta depois, então o aviso "saía" e não chegava.</para>
///
/// <para><b>Onde ficam os telefones da integração:</b> no <c>parametros_json</c> da credencial de
/// cada provedor (<c>telefonesNotificacao</c>), no mesmo lugar em que já moram baseUrl, autoLogin e
/// a agenda do lote. É configuração da integração e é apagada junto com ela.</para>
/// </summary>
public interface INotificadorSincronismo
{
    /// <summary>
    /// Manda o aviso. Nunca lança: falhar em avisar não pode derrubar o motor que estava tentando
    /// trabalhar — o erro real já está no log.
    /// </summary>
    /// <param name="provedor">Chave da integração: <c>sisreg</c>, <c>ser</c>, <c>sernit</c>.</param>
    /// <param name="chaveRepeticao">
    /// Tipo do aviso. Sem <c>:</c> (ex.: <c>captcha</c>) vira fonte própria na tela, com freio
    /// próprio — um CAPTCHA não pode ficar preso atrás do freio de uma unidade que falhou dez
    /// minutos antes. Com <c>:</c> (ex.: <c>unidade:{id}</c>) entra na fonte do provedor.
    /// </param>
    /// <param name="informativo">
    /// Progresso, não falha ("Iniciando X", "OK X", rodada limpa): só texto, só para os telefones
    /// da integração. Não gasta template — se a janela de 24h estiver fechada, não chega, e tudo bem.
    /// </param>
    Task NotificarAsync(
        string provedor, string titulo, string detalhe,
        string? chaveRepeticao = null, CancellationToken ct = default, bool informativo = false);

    /// <summary>Telefones cadastrados para receber avisos deste provedor.</summary>
    Task<IReadOnlyList<string>> ListarTelefonesAsync(string provedor, CancellationToken ct = default);

    /// <summary>Define os telefones do provedor. Lista vazia desliga o aviso.</summary>
    Task<IReadOnlyList<string>> SalvarTelefonesAsync(
        string provedor, IReadOnlyList<string> telefones, CancellationToken ct = default);
}

public sealed class NotificadorSincronismo(
    IIntegracaoCredencialService credenciais,
    IWhatsAppCliente whatsApp,
    IAlertaPlataforma alerta,
    ILogger<NotificadorSincronismo> logger) : INotificadorSincronismo
{
    public const string ChaveTelefones = "telefonesNotificacao";

    public async Task NotificarAsync(
        string provedor, string titulo, string detalhe,
        string? chaveRepeticao = null, CancellationToken ct = default, bool informativo = false)
    {
        try
        {
            var telefones = await ListarTelefonesAsync(provedor, ct);
            var sistema = provedor.ToUpperInvariant();

            if (informativo)
            {
                var texto = $"*{sistema} — {titulo}*\n\n{detalhe}";
                foreach (var telefone in telefones)
                {
                    var envio = await whatsApp.EnviarTextoAsync(telefone, texto, ct: ct);
                    if (!envio.Ok)
                        logger.LogInformation(
                            "NOTIFICADOR_SINCRONISMO: informativo não entregue a {Telefone}: {Erro}", telefone, envio.Erro);
                }
                return;
            }

            var chave = AlertaCatalogo.Sincronismo(provedor);
            var rotulo = $"Sincronismo {sistema}";
            if (chaveRepeticao is { Length: > 0 } sub && !sub.Contains(':'))
            {
                chave = $"{chave}.{sub.ToLowerInvariant()}";
                rotulo = $"{rotulo} — {sub}";
            }

            alerta.Reportar(new EventoAlerta(chave, titulo, detalhe)
            {
                Rotulo = rotulo,
                Grupo = "Sincronismo",
                TelefonesExtras = telefones,
            });
        }
        catch (Exception ex)
        {
            // Avisar é secundário: se falhar, o motor segue e o erro original continua no log.
            logger.LogWarning(ex, "NOTIFICADOR_SINCRONISMO: falha ao avisar sobre {Provedor}.", provedor);
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
            return null; // integração sem credencial: não há telefone da integração
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
