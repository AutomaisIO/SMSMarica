namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Catálogo das unidades do painel e das cores de triagem do contrato.
///
/// São bases muito diferentes por trás — Salux/Oracle no Conde e o HIS em SQL Server nas
/// duas UPAs, cada uma na sua instância — mas o painel expõe todas com o MESMO shape, e
/// uma aba "geral" que é a soma da rede. O que não existe numa unidade vem nulo, nunca
/// zerado.
/// </summary>
public static class Unidades
{
    public const string IdGeral = "geral";
    public const string IdConde = "conde";
    public const string IdUpa = "upa";
    public const string IdSantaRita = "santarita";

    public const string NomeConde = "Hospital Municipal Conde Modesto Leal";

    /// <summary>
    /// Sigla para as etiquetas de escopo. Por extenso, o nome do hospital ocupa três
    /// linhas dentro de um cartão de indicador e afoga o número que importa.
    /// </summary>
    public const string SiglaConde = "HMCML";
    public const string NomeUpa = "UPA 24h Maricá";
    public const string NomeSantaRita = "UPA 24h Santa Rita";

    public const string FonteConde = "Salux HIS — Hospital Municipal Conde Modesto Leal";
    public const string FonteUpa = "HIS da UPA 24h Maricá (CNES 7164440)";
    public const string FonteSantaRita = "HIS da UPA 24h Santa Rita";
    public const string FonteGeral = "Salux HIS (Conde Modesto Leal) + HIS das UPAs Maricá e Santa Rita";

    /// <summary>Procedência do Conde quando o conector ativo é o Klinikos (ver <see cref="ConsultasCondeKlinikos"/>).</summary>
    public const string FonteCondeKlinikos = "Klinikos — Hospital Municipal Conde Modesto Leal";
    public const string FonteGeralKlinikos = "Klinikos (Conde Modesto Leal) + HIS das UPAs Maricá e Santa Rita";

    /// <summary>Slug da base do HMCML no cadastro do smsmarica (Oracle do Salux).</summary>
    public const string BaseConde = "salux-hcml";

    /// <summary>
    /// Slug do Klinikos do Conde (SQL Server, banco KLINIKOSNET, agente WSS ligado em
    /// 24/09/2026). Conector ADICIONAL ao Salux — ver <see cref="PainelOpcoes.FonteConde"/>.
    /// </summary>
    public const string BaseCondeKlinikos = "conde-marica-sqlserver";

    /// <summary>Slugs das UPAs (SQL Server, alcançadas pelo agente WSS reverso).</summary>
    public const string BaseUpa = "upa24h-marica-sqlserver";
    public const string BaseSantaRita = "santarita-marica-sqlserver";

    // ── Cores de triagem ───────────────────────────────────────────────────────

    public const string Vermelho = "VERMELHO";
    public const string Laranja = "LARANJA";
    public const string Amarelo = "AMARELO";
    public const string Verde = "VERDE";
    public const string Azul = "AZUL";
    public const string SemClassificacao = "SEM_CLASSIFICACAO";

    /// <summary>
    /// Ordem clínica (mais grave primeiro). O contrato sempre devolve as SEIS entradas,
    /// em qualquer unidade: assim o front nunca precisa adivinhar o que falta, e o que
    /// a unidade não usa ele apaga por <see cref="CoresDe"/>.
    /// </summary>
    public static readonly string[] Cores =
        [Vermelho, Laranja, Amarelo, Verde, Azul, SemClassificacao];

    /// <summary>
    /// O Manchester do Salux no Conde tem quatro cores — não existe laranja lá. As UPAs
    /// usam cinco (o cadastro <c>risco_acolhimento</c> delas tem laranja, ainda que
    /// raríssimo na prática: 8 casos em 13.242 na UPA Maricá e 2 em 7.876 em Santa Rita,
    /// nos 30 dias até 24/07/2026).
    /// </summary>
    public static IReadOnlyList<string> CoresDe(string unidadeId) => unidadeId switch
    {
        IdConde => [Vermelho, Amarelo, Verde, Azul, SemClassificacao],
        _ => Cores,
    };

    /// <summary>
    /// Cores do Conde conforme o conector. O Klinikos do Conde USA laranja (473 + 47
    /// classificações de 07/08 a 24/09/2026) — o "sem laranja" acima é do Manchester do Salux.
    /// </summary>
    public static IReadOnlyList<string> CoresDoConde(bool klinikos) =>
        klinikos ? Cores : CoresDe(IdConde);
}
