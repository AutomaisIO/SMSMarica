using System.Globalization;
using Hl7.Fhir.Model;
using SMSMais.Core.Integracoes.KlinikosWeb.Cid;
using SMSMais.Core.Integracoes.Pep.Estrategias.Klinikos;

namespace SMSMais.Core.Integracoes.KlinikosWeb.Fhir;

/// <summary>
/// Web-linha → FHIR R4, com a MESMA FORMA que o conector SQL (<see cref="KlinikosFhirMapper"/>) —
/// é o que faz o teste de paridade poder comparar campo a campo. Em vez de reimplementar o
/// mapeamento, ADAPTA as linhas web para os records que o mapper SQL já consome
/// (<c>BoletimLinha</c>/<c>PacienteLinha</c>) e delega — o Encounter e o Patient saem pelo mesmo
/// código. A Condition é montada aqui porque o CID web vem como TEXTO e precisa passar pelo
/// de-para antes de virar código.
///
/// <para>Diferenças de fonte que o mapper não some (e o teste de paridade deve expor):</para>
/// <list type="bullet">
///   <item>a espinha web não tem CPF/CNS/mãe/sexo (o 407 dá nome+nascimento+prontuário) — o
///     Patient sai fino; identidade completa vem do cadastro (rel. 21) e do deep;</item>
///   <item>a chave local do paciente aqui é o PRONTUÁRIO (o 407 não expõe o <c>pac_codigo</c> que
///     o SQL usa) — por isso a escrita web só é primária onde não há dono SQL (Conde), controlado
///     pelo <c>webPrimaria</c> do <c>ParametrosJson</c> da <c>IaFonte</c>;</item>
///   <item>a hora do 667/526 tem precisão de minuto; o SQL tem o segundo.</item>
/// </list>
/// </summary>
public sealed class KlinikosWebFhirMapper
{
    private const string SysCid = "http://hl7.org/fhir/sid/icd-10";
    private const string SysBoletim = "urn:klinikos:boletim";

    private readonly KlinikosFhirMapper _sql;
    private readonly ICidDeParaService _cid;
    private readonly string _slug;
    private readonly string _source;

    public KlinikosWebFhirMapper(string slug, string source, ICidDeParaService cid)
    {
        _slug = slug;
        _source = source;
        _cid = cid;
        _sql = new KlinikosFhirMapper(slug, source);
    }

    public string Source => _source;

    /// <summary>Paciente FINO da espinha (nome, nascimento, chave local = prontuário). Sem CPF.</summary>
    public Patient MontarPaciente(EspinhaRegistro e)
    {
        var codLocal = string.IsNullOrWhiteSpace(e.Prontuario) ? e.SpaCodigo : e.Prontuario!;
        var linha = new PacienteLinha(
            Codigo: codLocal,
            Nome: e.Paciente,
            Cpf: null, Cns: null,
            Nascimento: DataNascimento(e.NascimentoIdade),
            Sexo: null, Mae: null, Pai: null,
            Telefone: null, Celular: null, Email: null,
            Obito: null, Responsavel: null, TelefoneResponsavel: null, Raca: null, Rv: 0);
        return _sql.BuildPatient(linha);
    }

    /// <summary>Encounter EMER da espinha: identifier do boletim, chegada, clínica. Mesma forma do SQL.</summary>
    public Encounter MontarEncounter(EspinhaRegistro e, string patientRef, string? organizationRef, string unidCodigo)
    {
        // A espinha (407+667) não diz se houve atendimento médico — isso vem do desfecho (526/deep).
        // Assume-se que houve (92,3% dos boletins têm), e o deep corrige o status/desfecho depois.
        // TODO(deep): setar teveAtendimento/desfecho a partir do 526 e da tela do atendimento.
        var linha = new BoletimLinha(
            Codigo: e.SpaCodigo,
            PacCodigo: e.Prontuario,
            UnidCodigo: unidCodigo,
            Chegada: ChegadaIso(e.Chegada),
            DataBoletim: null,
            NomeSocial: null,
            CartaoSus: null,
            FormaChegada: e.Origem,
            RiscoCodigo: e.Cor,
            Rv: 0);
        return _sql.BuildEncounter(linha, patientRef, organizationRef, teveAtendimento: true);
    }

    /// <summary>
    /// Condition do CID (via de-para texto→código). Mesmo identifier do SQL
    /// (<c>{slug}:{spa}:cond</c>) e mesmo <c>code</c> (coding ICD-10 + text) quando o de-para
    /// mapeou; quando não mapeou, entra text-only (e o chamador registra a exceção). Devolve
    /// <c>null</c> quando não há CID.
    /// </summary>
    public Condition? MontarCondition(string spa, string? cidTexto, string patientRef, string encRef, out bool mapeado)
    {
        mapeado = false;
        var resolvido = _cid.Resolver(cidTexto);
        if (string.IsNullOrWhiteSpace(resolvido.Texto)) return null;

        var code = new CodeableConcept { Text = resolvido.Texto };
        if (resolvido.Codigo is { } cod)
        {
            code.Coding = [new Coding(SysCid, ComPonto(cod), null)];
            mapeado = true;
        }

        return new Condition
        {
            Meta = new Meta { Source = _source },
            Subject = new ResourceReference(patientRef),
            Encounter = new ResourceReference(encRef),
            Code = code,
            Identifier = [new Identifier(SysBoletim, $"{_slug}:{spa}:cond")],
        };
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>"M545" → "M54.5" (categoria de 3 + subcategoria), como no conector SQL.</summary>
    private static string ComPonto(string codigo) =>
        codigo.Length > 3 && !codigo.Contains('.', StringComparison.Ordinal)
            ? codigo[..3] + "." + codigo[3..]
            : codigo;

    /// <summary>
    /// Chegada do 667 ("dd/MM/yyyy HH:mm" ou "dd/MM/yyyy HH:mm:ss") → ISO local sem offset
    /// ("yyyy-MM-ddTHH:mm:ss"); o mapper SQL acrescenta o <c>-03:00</c>. Precisão de MINUTO é o
    /// limite da fonte web (o segundo fica 00) — tolerância documentada no teste de paridade.
    /// </summary>
    internal static string? ChegadaIso(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;
        string[] formatos = ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy"];
        if (DateTime.TryParseExact(bruto.Trim(), formatos, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
        {
            return dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }
        return null;
    }

    /// <summary>
    /// Extrai a data de nascimento do campo combinado "Dt. Nascimento/ Idade" do 407, quando vem
    /// como data legível. Se vier como serial de Excel ou só idade, devolve null (o nascimento
    /// limpo entra pelo cadastro/deep) — melhor null que data inventada.
    /// </summary>
    internal static string? DataNascimento(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;
        var primeiro = bruto.Trim().Split([' ', '/'], 4);
        // Aceita só se começa com dd/MM/yyyy.
        if (DateTime.TryParseExact(
                string.Join('/', primeiro.Take(3)), "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        return null;
    }
}
