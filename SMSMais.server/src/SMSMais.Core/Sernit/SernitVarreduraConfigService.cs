using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.SernitWeb;

namespace SMSMais.Core.Sernit;

/// <summary>Ligar/desligar o disparo diário do SERNIT e a que horas ele roda.</summary>
public sealed record SernitVarreduraConfigDto(bool Ativo, string HoraLocal);

/// <summary>
/// Configuração do disparo diário da varredura do SERNIT, <b>persistida em banco</b> (no
/// <c>ParametrosJson</c> da credencial do provedor <c>sernit</c>, onde o <c>baseUrl</c> já vive).
/// Espelho do <c>SerVarreduraConfigService</c> do SER-RJ. O merge preserva as chaves que não são
/// nossas — sobrescrever o JSON inteiro apagaria o <c>baseUrl</c>.
/// </summary>
public interface ISernitVarreduraConfigService
{
    Task<SernitVarreduraConfigDto> ObterAsync(CancellationToken cancellationToken);
    Task<SernitVarreduraConfigDto> SalvarAsync(SernitVarreduraConfigDto config, CancellationToken cancellationToken);
}

public sealed class SernitVarreduraConfigService(IIntegracaoCredencialService credenciais)
    : ISernitVarreduraConfigService
{
    public const string ChaveAtivo = "varreduraAtiva";
    public const string ChaveHora = "varreduraHoraLocal";
    public const string HoraPadrao = "02:30";

    public async Task<SernitVarreduraConfigDto> ObterAsync(CancellationToken cancellationToken)
    {
        var json = await LerParametrosAsync(cancellationToken);
        return new SernitVarreduraConfigDto(
            json?[ChaveAtivo]?.GetValue<bool>() ?? false,
            json?[ChaveHora]?.GetValue<string>() ?? HoraPadrao);
    }

    public async Task<SernitVarreduraConfigDto> SalvarAsync(
        SernitVarreduraConfigDto config, CancellationToken cancellationToken)
    {
        var hora = Normalizar(config.HoraLocal);

        var atual = await credenciais.ObterAsync(SernitWebSessao.Provedor, cancellationToken);
        var json = Parse(atual.ParametrosJson) ?? new JsonObject();
        json[ChaveAtivo] = config.Ativo;
        json[ChaveHora] = hora;

        await credenciais.AtualizarAsync(
            SernitWebSessao.Provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: null,
                ClientSecret: null,
                RedirectUri: atual.RedirectUri,
                ParametrosJson: json.ToJsonString(),
                Ativo: atual.Ativo),
            cancellationToken);

        return new SernitVarreduraConfigDto(config.Ativo, hora);
    }

    private async Task<JsonObject?> LerParametrosAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(SernitWebSessao.Provedor, cancellationToken);
            return Parse(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
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

    internal static string Normalizar(string? hora)
    {
        if (!TimeOnly.TryParseExact(hora?.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var t))
        {
            throw new ValidacaoException(
                "sernit.hora_invalida",
                $"Hora inválida: \"{hora}\". Use o formato HH:mm (ex.: 02:30).");
        }

        return t.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
