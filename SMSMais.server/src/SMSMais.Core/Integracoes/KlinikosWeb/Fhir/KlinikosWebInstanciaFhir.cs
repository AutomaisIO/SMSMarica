namespace SMSMais.Core.Integracoes.KlinikosWeb.Fhir;

/// <summary>
/// Resolve, por provedor de instância, o <c>slug</c> e o <c>meta.source</c> com que o conector
/// web monta os recursos FHIR.
///
/// <para><b>Conde</b> ganha um slug NOVO (<c>klinikos-conde</c>): é a instância que o hub não
/// tinha por SQL — o web é a única fonte dela, então não colide com nada.</para>
///
/// <para><b>UPA</b> e <b>Santa Rita</b> reaproveitam o slug do conector SQL
/// (<c>upa24h-marica-sqlserver</c> / <c>santarita-marica-sqlserver</c>) <b>só para efeito de
/// comparação de forma</b> no teste de paridade — o web monta o recurso com o MESMO identifier
/// que o SQL gravou, para o diff bater campo a campo. A escrita nessas instâncias fica bloqueada
/// (o SQL é o dono); ver <see cref="EhFonteWebPrimaria"/>.</para>
/// </summary>
public static class KlinikosWebInstanciaFhir
{
    /// <summary>Base do <c>meta.source</c>, igual à do conector SQL (mesmo hub).</summary>
    public const string SourceBase = "https://smsmarica.saude.marica/source";

    public sealed record IdentidadeFhir(string Slug, string Source, string UnidCodigo, bool WebEhPrimaria);

    private static readonly IReadOnlyDictionary<string, IdentidadeFhir> PorProvedor =
        new Dictionary<string, IdentidadeFhir>(StringComparer.Ordinal)
        {
            // Conde: fonte web primária (não há SQL no hub) — slug próprio.
            [KlinikosInstancia.ProvedorConde] =
                new("klinikos-conde", $"{SourceBase}/klinikos/klinikos-conde", "0005", WebEhPrimaria: true),

            // UPAs: slug do SQL, só para paridade de forma; web NÃO é primária (o SQL é o dono).
            [KlinikosInstancia.ProvedorUpa] =
                new("upa24h-marica-sqlserver", $"{SourceBase}/klinikos/upa24h-marica-sqlserver", "0006", WebEhPrimaria: false),

            [KlinikosInstancia.ProvedorSantaRita] =
                new("santarita-marica-sqlserver", $"{SourceBase}/klinikos/santarita-marica-sqlserver", "0007", WebEhPrimaria: false),
        };

    public static IdentidadeFhir De(string provedor) =>
        PorProvedor.TryGetValue(provedor, out var id)
            ? id
            : throw new ArgumentException($"Provedor '{provedor}' não é uma instância Klinikos.", nameof(provedor));

    /// <summary>
    /// A escrita web é permitida nesta instância? Só onde o web é a fonte primária (Conde). Nas
    /// UPAs/Santa Rita o dono é o conector SQL — gravar por aqui duplicaria a mesma pessoa/boletim.
    /// </summary>
    public static bool EhFonteWebPrimaria(string provedor) =>
        PorProvedor.TryGetValue(provedor, out var id) && id.WebEhPrimaria;
}
