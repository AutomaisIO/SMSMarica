using System.Globalization;
using FellowOakDicom;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Worklist;

/// <summary>
/// As tags DICOM que o cadastro do equipamento deixa escolher para os campos opcionais do item de
/// worklist — médico solicitante, unidade ("clínica") e o código do procedimento fora da sequência
/// de código (ADR-0074, adendo de 16/10/2026).
///
/// <para>A tag é livre porque cada aparelho lê um campo, e quem descobre qual é o técnico, testando.
/// Mas passa por três travas: (1) existe no dicionário DICOM e não é privada; (2) é de texto
/// (PN, LO, SH, ST, LT, UT), porque o valor é um nome ou um código; (3) não é uma das tags que o
/// item já preenche. Sobrescrever o accession, o paciente ou as chaves da etapa agendada não daria
/// erro em lugar nenhum — o aparelho só passaria a mostrar outra coisa, e a nossa conciliação
/// deixaria de casar o estudo com o pedido.</para>
/// </summary>
public static class TagDicomWorklist
{
    /// <summary>(0008,0090) Referring Physician's Name — o campo de médico que o CardioNet pede.</summary>
    public const string MedicoSolicitantePadrao = "00080090";

    /// <summary>(0008,0080) Institution Name — onde o exame é feito.</summary>
    public const string UnidadeExecutantePadrao = "00080080";

    /// <summary>(0032,1033) Requesting Service — no DICOM, o serviço que pediu o exame.</summary>
    public const string UnidadeSolicitantePadrao = "00321033";

    /// <summary>(0008,0100) Code Value — o código vai dentro das sequências de código, como no
    /// ADR-0074 original. É o "nada escolhido" do campo de código.</summary>
    public const string CodigoProcedimentoPadrao = "00080100";

    public static string UnidadePadrao(OrigemUnidadeWorklist origem) =>
        origem == OrigemUnidadeWorklist.Solicitante ? UnidadeSolicitantePadrao : UnidadeExecutantePadrao;

    /// <summary>O que <see cref="ConstrutorMwlItem"/> já preenche, mais os atributos do item de
    /// código. Nenhum campo opcional cai aqui.</summary>
    private static readonly HashSet<string> Reservadas = new(StringComparer.Ordinal)
    {
        "00080005", "00080050", "00080052", "00080060",
        "00080100", "00080102", "00080103", "00080104",
        "00100010", "00100020", "00100021", "00100030", "00100040",
        "00101020", "00101030", "001021A0", // altura, peso e fumante da anamnese do Holter
        "0020000D", "00321060", "00321064",
        "00400001", "00400002", "00400003", "00400007", "00400008", "00400009", "00400010", "00400020",
        "00400100", "00401001", "00401003", "00741202",
    };

    /// <summary>Atributos de texto do módulo Scheduled Procedure Step (PS3.4 K.6.1.2.2): vão DENTRO
    /// da (0040,0100). Todo o resto vai no nível do item.</summary>
    private static readonly HashSet<string> DaEtapaAgendada = new(StringComparer.Ordinal)
    {
        "00400006", // Scheduled Performing Physician's Name
        "00400011", // Scheduled Procedure Step Location
        "00400012", // Pre-Medication
        "00400400", // Comments on the Scheduled Procedure Step
        "00321070", // Requested Contrast Agent
    };

    /// <summary>Teto de caracteres por VR (PS3.5 6.2). UT é praticamente ilimitado no padrão; aqui
    /// fica no do LT, que já sobra para um nome ou código.</summary>
    private static readonly Dictionary<string, int> TetoPorVr = new(StringComparer.Ordinal)
    {
        ["PN"] = 64, ["LO"] = 64, ["SH"] = 16, ["ST"] = 1024, ["LT"] = 10240, ["UT"] = 10240,
    };

    /// <summary>"0008,0090", "(0008,0090)", "0008 0090" ou "00080090" → "00080090". Null quando
    /// vazio ou fora do formato.</summary>
    public static string? Normalizar(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return null;
        var hex = new string([.. entrada.Where(c => c is not ('(' or ')' or ',' or ' '))]).ToUpperInvariant();
        return hex.Length == 8 && hex.All(char.IsAsciiHexDigit) ? hex : null;
    }

    /// <summary>"00080090" → "0008,0090", como o técnico escreve.</summary>
    public static string Formatar(string tag) => $"{tag[..4]},{tag[4..]}";

    /// <summary>Por que a tag não serve para um campo opcional — ou null quando serve.</summary>
    public static string? Problema(string? entrada)
    {
        var tag = Normalizar(entrada);
        if (tag is null) return $"Tag DICOM inválida ('{entrada}'): use o formato 0008,0090.";
        if (Reservadas.Contains(tag))
            return $"A tag ({Formatar(tag)}) já é preenchida pelo sistema no item da worklist.";
        var vr = Vr(tag);
        if (vr is null) return $"A tag ({Formatar(tag)}) não existe no dicionário DICOM.";
        if (!TetoPorVr.ContainsKey(vr))
            return $"A tag ({Formatar(tag)}) é do tipo {vr}; aqui só cabem tags de texto (PN, LO, SH, ST, LT, UT).";
        return null;
    }

    /// <summary>VR da tag no dicionário, ou null quando ela não existe ou é privada.</summary>
    internal static string? Vr(string tag)
    {
        var t = new DicomTag(
            ushort.Parse(tag[..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            ushort.Parse(tag[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        if (t.IsPrivate) return null;
        var entrada = DicomDictionary.Default[t];
        if (entrada == DicomDictionary.UnknownTag || entrada.ValueRepresentations.Length == 0) return null;
        return entrada.ValueRepresentations[0].Code;
    }

    /// <summary>Serve para gravar no item? Mesma régua de <see cref="Problema"/>, para o construtor
    /// não confiar num cadastro feito antes de uma regra existir.</summary>
    internal static bool Utilizavel(string tag) => Problema(tag) is null;

    internal static bool VaiNaEtapaAgendada(string tag) => DaEtapaAgendada.Contains(tag);

    internal static int Teto(string vr) => TetoPorVr.TryGetValue(vr, out var t) ? t : 64;
}
