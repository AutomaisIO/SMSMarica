using System.Text.Json;

namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>
/// Uma instância do Klinikos (Eco Sistemas) — Conde, UPA Maricá ou Santa Rita. São três
/// <b>servidores e bases distintos</b> (só sincronizam login e cadastro entre si), então cada um
/// é um provedor de credencial próprio. O <c>AppRoot</c> difere: Conde serve em
/// <c>/KlinikosNet</c>; UPA e Santa Rita em <c>/UPA24H</c> (medido 16/09/2026).
/// </summary>
public sealed record KlinikosInstancia(string Provedor, Uri BaseUri, string AppRoot, string UnidCodigo)
{
    /// <summary>Provedores por instância (chave da credencial em <c>integracao_credencial</c>).</summary>
    public const string ProvedorConde = "klinikos_conde";
    public const string ProvedorUpa = "klinikos_upa";
    public const string ProvedorSantaRita = "klinikos_santarita";

    /// <summary>
    /// Defaults medidos por provedor (<c>AppRoot</c> + <c>UnidCodigo</c>). O operador só precisa
    /// informar URL/usuário/senha; o resto é constante da instância, sobrescritível pelo
    /// <c>parametrosJson</c> da credencial se algum dia mudar.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string AppRoot, string Unid)> Defaults =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            [ProvedorConde] = ("/KlinikosNet", "0005"),
            [ProvedorUpa] = ("/UPA24H", "0006"),
            [ProvedorSantaRita] = ("/UPA24H", "0007"),
        };

    public static bool EhProvedorKlinikos(string provedor) => Defaults.ContainsKey(provedor);

    /// <summary>
    /// Monta a instância a partir do provedor + <c>parametrosJson</c> da credencial. <c>baseUrl</c>
    /// é obrigatório (é o que o operador informa); <c>appRoot</c>/<c>unidCodigo</c> caem no default
    /// da instância quando ausentes.
    /// </summary>
    public static KlinikosInstancia De(string provedor, string? parametrosJson)
    {
        if (!Defaults.TryGetValue(provedor, out var padrao))
        {
            throw new ArgumentException($"Provedor '{provedor}' não é uma instância Klinikos.", nameof(provedor));
        }

        string? baseUrl = null, appRoot = null, unid = null;
        if (!string.IsNullOrWhiteSpace(parametrosJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(parametrosJson);
                baseUrl = Texto(doc.RootElement, "baseUrl");
                appRoot = Texto(doc.RootElement, "appRoot");
                unid = Texto(doc.RootElement, "unidCodigo");
            }
            catch (JsonException)
            {
                // parametrosJson inválido → usa defaults; baseUrl ausente cai na validação abaixo.
            }
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                $"A credencial '{provedor}' não tem baseUrl no parametrosJson — informe a URL da instância.");
        }

        return new KlinikosInstancia(
            provedor,
            new Uri(baseUrl.TrimEnd('/')),
            "/" + (appRoot ?? padrao.AppRoot).Trim('/'),
            unid ?? padrao.Unid);
    }

    private static string? Texto(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;
}

/// <summary>
/// Uma linha do relatório 407 (Pacientes Registrados no Dia): a base do Encounter + identidade.
/// <c>SpaCodigo</c> já vem normalizado (12 dígitos, zero-pad).
/// </summary>
public sealed record BoletimRegistro(
    string SpaCodigo, string? Prontuario, string? Paciente, string? NascimentoIdade, string? Clinica);

/// <summary>
/// Uma linha do relatório 667 (Nominal por Classificação de Risco): chegada + COR por boletim.
/// A cor vem de cabeçalho de grupo (não é coluna), resolvida no parser.
/// </summary>
public sealed record ClassificacaoRegistro(
    string SpaCodigo, string? Chegada, string? Cor, string? Origem);

/// <summary>
/// Registro da ESPINHA por boletim, resultado do JOIN 407×667 por <c>SpaCodigo</c> — o que a via
/// rápida gravaria como Encounter (chegada, cor, clínica) + identidade (paciente/prontuário).
/// A etapa de escrita FHIR (próxima) converte isto em recurso via o mapper canônico.
/// </summary>
public sealed record EspinhaRegistro(
    string SpaCodigo, string? Chegada, string? Cor, string? Clinica,
    string? Paciente, string? Prontuario, string? NascimentoIdade, string? Origem,
    bool Em407, bool Em667);

/// <summary>
/// Uma linha do relatório 526 (Atendimentos por Profissional): traz, por boletim, a hora do
/// atendimento e — em sub-linha — o TEXTO do CID (às vezes sem código). É a fonte do CID em
/// lote (o relatório nominal dedicado, o 815, está quebrado no Crystal deles).
/// </summary>
public sealed record AtendimentoRegistro(string SpaCodigo, string? HoraAtendimento, string? CidTexto);

/// <summary>Resultado do de-para de CID: código quando mapeável, sempre o texto original.</summary>
public sealed record CidResolvido(string? Codigo, string Texto, bool Mapeado);

/// <summary>Contagens do dry-run da espinha (sem PII) — o que seria montado, sem gravar nada.</summary>
public sealed record ResumoDryRun(
    string Provedor,
    DateOnly Dia,
    int Boletins,
    int ComCor,
    int ComChegada,
    int SoEm407,
    int SoEm667,
    int EmAmbos,
    IReadOnlyDictionary<string, int> PorCor,
    double Segundos);
