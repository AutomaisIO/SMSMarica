using System.Text.RegularExpressions;

namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>
/// Catálogo VERSIONADO de builds do Klinikos, no CÓDIGO (não no banco). Por que no código: cada
/// município é uma instância isolada (ADR-0043), mas o conhecimento de "como este build se comporta"
/// é do PRODUTO — vive na base compartilhada e chega a todas as instâncias por deploy. Assim, se
/// outro projeto tem Klinikos no mesmo build, reaproveita o perfil que já existe, sem recon.
///
/// <para>Cada build é uma <b>cópia imutável</b> (como migration): não se edita o perfil de um build;
/// build novo = entrada nova. O git guarda o histórico.</para>
///
/// <para>Fluxo de segurança (ver <see cref="KlinikosBuildDivergenteException"/> e
/// <see cref="KlinikosBuildDesconhecidoException"/>): a fonte DECLARA a versão (dropdown, gravada no
/// <c>ParametrosJson</c>); o motor DETECTA a <c>lblVersao</c> da tela; só executa se
/// <b>detectada == declarada</b> e a declarada estiver no catálogo. Divergência bloqueia — nunca
/// roda o perfil de um build contra um sistema atualizado.</para>
/// </summary>
public static class KlinikosBuildCatalogo
{
    /// <summary>Como a espinha é extraída neste build. <c>Conde2024</c> usa 407+667+526 (traz
    /// chegada+cor+CID). <c>Upa2025</c> usa 751+752 (cadastro+CID; chegada/cor NÃO vêm por relatório
    /// nesta build — ficam na fila viva/deep). Build novo com mesma estrutura reaproveita a estratégia.</summary>
    public enum Estrategia { Conde2024, Upa2025 }

    public sealed record Build(string Versao, string Rotulo, string AppRootPadrao, Estrategia Estrategia);

    /// <summary>Builds conhecidos, com perfil. Ao aparecer um build novo (o motor gera erro),
    /// acrescenta-se aqui uma ENTRADA NOVA — cópia, nunca edição de uma existente.</summary>
    public static readonly IReadOnlyList<Build> Conhecidos =
    [
        new("K.2024.09.2.1", "Conde 2024 (K.2024.09.2.1)", "/KlinikosNet", Estrategia.Conde2024),
        new("U.2025.06.1.26", "UPA/Santa Rita 2025 (U.2025.06.1.26)", "/UPA24H", Estrategia.Upa2025),
    ];

    private static readonly Regex VersaoNaTela =
        new(@"[KU]\.\d{4}(?:\.\d+)+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Extrai a versão da tela (a <c>lblVersao</c> traz "Versão: U.2025.06.1.26").
    /// Devolve a string normalizada (ex.: <c>U.2025.06.1.26</c>) ou <c>null</c> se não achou.</summary>
    public static string? ExtrairVersao(string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;
        var m = VersaoNaTela.Match(html);
        return m.Success ? m.Value.Trim().ToUpperInvariant() : null;
    }

    public static string Normalizar(string? versao) => (versao ?? string.Empty).Trim().ToUpperInvariant();

    public static bool Reconhece(string? versao) =>
        Conhecidos.Any(b => b.Versao.Equals(Normalizar(versao), StringComparison.OrdinalIgnoreCase));

    public static Build? Perfil(string? versao) =>
        Conhecidos.FirstOrDefault(b => b.Versao.Equals(Normalizar(versao), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A versão DETECTADA na tela difere da DECLARADA na fonte (ou a fonte não declarou versão). O motor
/// CRITICA e bloqueia — não roda o perfil errado. É condição de configuração/operação (o vendor
/// provavelmente atualizou o build): 409, retryável depois de reconciliar a versão. Nunca um 500.
/// </summary>
public sealed class KlinikosBuildDivergenteException(string slug, string declarada, string detectada)
    : Exception(string.IsNullOrWhiteSpace(declarada)
        ? $"A fonte '{slug}' não declarou a versão do Klinikos. Selecione a versão detectada "
          + $"('{detectada}') no cadastro antes de sincronizar."
        : $"Versão do Klinikos divergente na fonte '{slug}': declarada '{declarada}', detectada "
          + $"'{detectada}'. O build da origem mudou — reconcilie a versão no cadastro antes de sincronizar.")
{
    public string Slug { get; } = slug;
    public string Declarada { get; } = declarada;
    public string Detectada { get; } = detectada;
}

/// <summary>
/// A versão detectada não tem perfil no catálogo — build novo. É o alerta "criar perfil": um humano
/// faz o recon do build novo e acrescenta uma cópia em <see cref="KlinikosBuildCatalogo.Conhecidos"/>.
/// </summary>
public sealed class KlinikosBuildDesconhecidoException(string slug, string detectada)
    : Exception($"Build do Klinikos SEM PERFIL na fonte '{slug}': '{detectada}'. É um build novo — "
        + "faça o recon e crie o perfil no catálogo (KlinikosBuildCatalogo) antes de sincronizar.")
{
    public string Slug { get; } = slug;
    public string Detectada { get; } = detectada;
}
