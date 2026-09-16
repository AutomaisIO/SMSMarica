using System.Text.Json;

namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>
/// Interpreta o <c>ParametrosJson</c> de uma <c>IaFonte</c> do tipo <c>KlinikosWeb</c> nos valores
/// que o conector precisa: caminho da app (<c>appRoot</c>), código da unidade (<c>unidCodigo</c>),
/// o slug do <c>meta.source</c> FHIR (<c>metaSource</c>) e se o web é a fonte primária de escrita
/// (<c>webPrimaria</c> — só o Conde, onde não há SQL no hub).
///
/// <para>Puro e sem I/O — testável em unidade. Defaults conservadores: <c>webPrimaria</c> = false
/// (a escrita só é liberada quando o operador marca explicitamente), <c>appRoot</c> =
/// <c>/KlinikosNet</c> (o Conde), <c>metaSource</c> derivado do slug.</para>
/// </summary>
public static class KlinikosWebParametros
{
    /// <summary>Base do <c>meta.source</c>, igual à do conector SQL (mesmo hub).</summary>
    public const string SourceBase = "https://smsmarica.saude.marica/source";

    public sealed record Valores(string AppRoot, string UnidCodigo, string MetaSource, bool WebPrimaria);

    /// <summary>Sugestões de preenchimento por slug conhecido — usadas só como conveniência de
    /// cadastro/seed; o runtime lê o que estiver gravado no <c>ParametrosJson</c>.</summary>
    public static readonly IReadOnlyDictionary<string, Valores> Sugestoes =
        new Dictionary<string, Valores>(StringComparer.OrdinalIgnoreCase)
        {
            ["klinikos-conde"] = new("/KlinikosNet", "0005", $"{SourceBase}/klinikos/klinikos-conde", WebPrimaria: true),
            ["upa24h-marica-sqlserver"] = new("/UPA24H", "0006", $"{SourceBase}/klinikos/upa24h-marica-sqlserver", WebPrimaria: false),
            ["santarita-marica-sqlserver"] = new("/UPA24H", "0007", $"{SourceBase}/klinikos/santarita-marica-sqlserver", WebPrimaria: false),
        };

    public static Valores Resolver(string slug, string? parametrosJson)
    {
        string? appRoot = null, unid = null, metaSource = null;
        bool? webPrimaria = null;

        if (!string.IsNullOrWhiteSpace(parametrosJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(parametrosJson);
                var r = doc.RootElement;
                appRoot = Texto(r, "appRoot");
                unid = Texto(r, "unidCodigo");
                metaSource = Texto(r, "metaSource");
                if (r.TryGetProperty("webPrimaria", out var wp) &&
                    (wp.ValueKind == JsonValueKind.True || wp.ValueKind == JsonValueKind.False))
                {
                    webPrimaria = wp.GetBoolean();
                }
            }
            catch (JsonException)
            {
                // JSON inválido → cai nos defaults abaixo.
            }
        }

        return new Valores(
            AppRoot: "/" + (appRoot ?? "/KlinikosNet").Trim('/'),
            UnidCodigo: unid ?? string.Empty,
            MetaSource: metaSource ?? $"{SourceBase}/klinikos/{slug}",
            WebPrimaria: webPrimaria ?? false);
    }

    private static string? Texto(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;
}
