using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.SerWeb;

namespace SMSMais.Core.Ser;

/// <summary>Ligar/desligar o disparo diário e a que horas ele roda.</summary>
public sealed record SerVarreduraConfigDto(bool Ativo, string HoraLocal);

/// <summary>
/// Configuração do disparo diário da varredura do SER, <b>persistida em banco</b>.
///
/// <para><b>Por que sair do appsettings:</b> `Ser:Varredura:Ativo` e `HoraLocal` só existiam como
/// configuração de arquivo, então mudar a hora de uma rodada exigia editar o servidor e
/// redeployar. Isso não é configuração de infraestrutura — é decisão de operação, e quem decide
/// não tem (nem deve ter) acesso ao deploy.</para>
///
/// <para>Mora no <c>ParametrosJson</c> da credencial do SER, onde o <c>baseUrl</c> já vivia. O
/// merge preserva as chaves que não são nossas: sobrescrever o JSON inteiro apagaria o baseUrl e
/// o motor passaria a apontar para o endereço default sem ninguém entender por quê.</para>
/// </summary>
public interface ISerVarreduraConfigService
{
    Task<SerVarreduraConfigDto> ObterAsync(CancellationToken cancellationToken);
    Task<SerVarreduraConfigDto> SalvarAsync(SerVarreduraConfigDto config, CancellationToken cancellationToken);
}

public sealed class SerVarreduraConfigService(IIntegracaoCredencialService credenciais)
    : ISerVarreduraConfigService
{
    public const string ChaveAtivo = "varreduraAtiva";
    public const string ChaveHora = "varreduraHoraLocal";

    /// <summary>Madrugada por default — mas agora é só um default, não um dogma: com sessões
    /// simultâneas provadas (08/08/2026), a rodada não tira mais o SER de quem regula.</summary>
    public const string HoraPadrao = "02:30";

    public async Task<SerVarreduraConfigDto> ObterAsync(CancellationToken cancellationToken)
    {
        var json = await LerParametrosAsync(cancellationToken);
        return new SerVarreduraConfigDto(
            json?[ChaveAtivo]?.GetValue<bool>() ?? false,
            json?[ChaveHora]?.GetValue<string>() ?? HoraPadrao);
    }

    public async Task<SerVarreduraConfigDto> SalvarAsync(
        SerVarreduraConfigDto config, CancellationToken cancellationToken)
    {
        var hora = Normalizar(config.HoraLocal);

        var atual = await credenciais.ObterAsync(SerWebSessao.Provedor, cancellationToken);
        var json = Parse(atual.ParametrosJson) ?? new JsonObject();
        json[ChaveAtivo] = config.Ativo;
        json[ChaveHora] = hora;

        await credenciais.AtualizarAsync(
            SerWebSessao.Provedor,
            new AtualizarIntegracaoCredencialRequest(
                // Vazios de propósito: o store trata vazio como "mantém o atual", e a senha do SER
                // é write-only — nunca passa por aqui.
                ClientId: null,
                ClientSecret: null,
                RedirectUri: atual.RedirectUri,
                ParametrosJson: json.ToJsonString(),
                Ativo: atual.Ativo),
            cancellationToken);

        return new SerVarreduraConfigDto(config.Ativo, hora);
    }

    // ------------------------------------------------------------------ interno

    private async Task<JsonObject?> LerParametrosAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(SerWebSessao.Provedor, cancellationToken);
            return Parse(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
            // Provedor ainda não cadastrado: para a tela isso é simplesmente "não configurado".
            return null;
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

    /// <summary>
    /// <c>HH:mm</c> validado. Hora inválida seria pior que hora errada: o scheduler compararia
    /// contra um valor que nunca chega e o disparo diário simplesmente nunca aconteceria —
    /// silenciosamente, que é o modo de falhar que este módulo mais combate.
    /// </summary>
    internal static string Normalizar(string? hora)
    {
        if (!TimeOnly.TryParseExact(hora?.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var t))
        {
            throw new ValidacaoException(
                "ser.hora_invalida",
                $"Hora inválida: \"{hora}\". Use o formato HH:mm (ex.: 02:30).");
        }

        return t.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
