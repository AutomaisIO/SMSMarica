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

    /// <param name="PeriodicoLigado">Liga a puxada periódica da espinha desta base pelo scheduler.
    ///   MANUAL e por base — o motor nunca liga sozinho nem ajusta a cadência.</param>
    /// <param name="PeriodicoIntervaloMin">Intervalo em MINUTOS entre puxadas periódicas (ex.: 60,
    ///   30, 15). <c>0</c> ou negativo = desligado. É o número que o operador especifica por base,
    ///   depois de medir o ponto de saturação do Crystal daquela unidade.</param>
    /// <param name="DeepThrottleSeg">Espaçamento em SEGUNDOS entre boletins na fila do deep (drenada
    ///   pela tela viva, fora do Crystal). <c>0</c> = sem drenador ainda; campo pronto para quando o
    ///   drenador existir.</param>
    /// <param name="Build">Versão do Klinikos DECLARADA para esta fonte (o dropdown do cadastro, ex.:
    ///   <c>U.2025.06.1.26</c>). O motor detecta a versão da tela e só sincroniza se bater com esta —
    ///   ver <see cref="KlinikosBuildCatalogo"/>. Vazio = não declarada (o motor bloqueia até selecionar).</param>
    public sealed record Valores(
        string AppRoot, string UnidCodigo, string MetaSource, bool WebPrimaria,
        bool PeriodicoLigado = false, int PeriodicoIntervaloMin = 0, int DeepThrottleSeg = 0,
        string Build = "");

    /// <summary>Sugestões de preenchimento por slug conhecido — usadas só como conveniência de
    /// cadastro/seed; o runtime lê o que estiver gravado no <c>ParametrosJson</c>.</summary>
    public static readonly IReadOnlyDictionary<string, Valores> Sugestoes =
        new Dictionary<string, Valores>(StringComparer.OrdinalIgnoreCase)
        {
            ["klinikos-conde"] = new("/KlinikosNet", "0005", $"{SourceBase}/klinikos/klinikos-conde", WebPrimaria: true, Build: "K.2024.09.2.1"),
            ["upa24h-marica-sqlserver"] = new("/UPA24H", "0006", $"{SourceBase}/klinikos/upa24h-marica-sqlserver", WebPrimaria: false, Build: "U.2025.06.1.26"),
            ["santarita-marica-sqlserver"] = new("/UPA24H", "0007", $"{SourceBase}/klinikos/santarita-marica-sqlserver", WebPrimaria: false, Build: "U.2025.06.1.26"),
        };

    public static Valores Resolver(string slug, string? parametrosJson)
    {
        string? appRoot = null, unid = null, metaSource = null, build = null;
        bool? webPrimaria = null;
        bool periodicoLigado = false;
        int periodicoIntervaloMin = 0, deepThrottleSeg = 0;

        if (!string.IsNullOrWhiteSpace(parametrosJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(parametrosJson);
                var r = doc.RootElement;
                appRoot = Texto(r, "appRoot");
                unid = Texto(r, "unidCodigo");
                metaSource = Texto(r, "metaSource");
                build = Texto(r, "build");
                if (r.TryGetProperty("webPrimaria", out var wp) &&
                    (wp.ValueKind == JsonValueKind.True || wp.ValueKind == JsonValueKind.False))
                {
                    webPrimaria = wp.GetBoolean();
                }
                if (r.TryGetProperty("periodicoLigado", out var pl) &&
                    (pl.ValueKind == JsonValueKind.True || pl.ValueKind == JsonValueKind.False))
                {
                    periodicoLigado = pl.GetBoolean();
                }
                periodicoIntervaloMin = Inteiro(r, "periodicoIntervaloMin");
                deepThrottleSeg = Inteiro(r, "deepThrottleSeg");
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
            WebPrimaria: webPrimaria ?? false,
            PeriodicoLigado: periodicoLigado,
            PeriodicoIntervaloMin: periodicoIntervaloMin,
            DeepThrottleSeg: deepThrottleSeg,
            Build: KlinikosBuildCatalogo.Normalizar(build));
    }

    private static string? Texto(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;

    /// <summary>Lê um inteiro do JSON, aceitando número (<c>15</c>) ou texto numérico (<c>"15"</c>).
    /// Ausente ou inválido → <c>0</c> (que o chamador trata como desligado).</summary>
    private static int Inteiro(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(v.GetString(), out var n) => n,
            _ => 0,
        };
    }
}
