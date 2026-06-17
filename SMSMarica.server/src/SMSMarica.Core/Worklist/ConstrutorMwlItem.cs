using System.Text.Json.Nodes;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Worklist;

/// <summary>
/// Converte uma <see cref="SolicitacaoExame"/> + dados do paciente (hub FHIR) num
/// item de Modality Worklist (MWL) DICOM+JSON, aceito pelo endpoint <c>/mwlitems</c>
/// do dcm4chee. O equipamento (ex.: Fuji FDR-3000AWS) consome via C-FIND MWL
/// clássico, filtrando por ScheduledStationAETitle + Modality + Data — por isso a
/// SPS (0040,0100) carrega esses campos. Formato JSON conforme PS3.18 Annex F.
/// </summary>
internal static class ConstrutorMwlItem
{
    /// <summary>SPS ID estável derivado do accession — junto com o StudyInstanceUID
    /// forma a chave do MWL item (usada nos GET/DELETE do dcm4chee).</summary>
    public static string SpsId(SolicitacaoExame s) => $"SPS-{s.AccessionNumber}";

    /// <summary>RequestedProcedureID (0040,1001) limitado a 10 caracteres: apesar de o
    /// DICOM SH permitir 16, o Fuji rejeita IDs maiores. Deriva do AccessionNumber
    /// removendo o prefixo "SMS" — sobra ano+sequência (ex.: SMS2026000001 -> 2026000001,
    /// 10 chars, único por ano). Para accession em formato inesperado, trunca nos
    /// últimos 10 caracteres. Não é chave de lookup (o vínculo é por SpsId + StudyUID).</summary>
    public static string RequestedProcedureId(SolicitacaoExame s)
    {
        var acc = s.AccessionNumber ?? string.Empty;
        var id = acc.StartsWith("SMS", StringComparison.Ordinal) ? acc[3..] : acc;
        return id.Length <= 10 ? id : id[^10..];
    }

    /// <summary>PatientID exibido no equipamento: CPF (só dígitos) quando houver,
    /// senão o id do paciente no hub FHIR (Guid). O vínculo do estudo de volta é pelo
    /// StudyInstanceUID — não por este ID — então usar CPF é seguro e mais legível.</summary>
    public static string PatientId(SolicitacaoExame s, PacienteResumo p)
    {
        var cpf = new string((p.Cpf ?? string.Empty).Where(char.IsDigit).ToArray());
        return cpf.Length == 11 ? cpf : s.PacienteId.ToString();
    }

    /// <summary>Item MWL completo (top-level + Scheduled Procedure Step Sequence).</summary>
    public static JsonObject Item(SolicitacaoExame s, PacienteResumo paciente, string stationAeTitle)
    {
        var tipo = s.TipoExame ?? throw new InvalidOperationException("TipoExame não carregado.");
        var quando = s.DataAgendada ?? DateTime.UtcNow;

        return new JsonObject
        {
            ["00080050"] = Sh(s.AccessionNumber),                            // AccessionNumber
            ["00100010"] = Pn(paciente.Nome),                               // PatientName
            ["00100020"] = Lo(PatientId(s, paciente)),                      // PatientID (CPF ou Guid do hub FHIR)
            ["00100030"] = Da(paciente.DataNascimento),                     // PatientBirthDate
            ["00100040"] = Cs(MapearSexo(paciente.Sexo)),                   // PatientSex
            ["0020000D"] = Ui(s.StudyInstanceUID),                          // StudyInstanceUID
            ["00321060"] = Lo(tipo.RequestedProcedureDescription),          // RequestedProcedureDescription
            ["00321064"] = SeqCodigoProc(tipo),                            // RequestedProcedureCodeSequence (SIGTAP)
            ["00401001"] = Sh(RequestedProcedureId(s)),                     // RequestedProcedureID (<= 10 chars, exigência do Fuji)
            ["00401003"] = Sh(MapearPrioridade(s.Prioridade)),             // RequestedProcedurePriority
            ["00400100"] = new JsonObject                                   // ScheduledProcedureStepSequence
            {
                ["vr"] = "SQ",
                ["Value"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["00080060"] = Cs(tipo.ModalidadeDicom.ToString()),         // Modality
                        ["00400001"] = Ae(stationAeTitle),                          // ScheduledStationAETitle (filtro do equipamento)
                        ["00400002"] = ComVr("DA", quando.ToString("yyyyMMdd")),    // SPS StartDate
                        ["00400003"] = ComVr("TM", quando.ToString("HHmmss")),      // SPS StartTime
                        ["00400006"] = Pn(s.SolicitanteNome),                       // ScheduledPerformingPhysicianName (solicitante)
                        ["00400007"] = Lo(tipo.ScheduledProcedureStepDescription),  // SPS Description
                        ["00400009"] = Sh(SpsId(s)),                                // SPS ID
                        ["00400010"] = Sh(stationAeTitle),                          // ScheduledStationName
                        ["00400020"] = Cs("SCHEDULED"),                             // SPS Status
                    },
                },
            },
        };
    }

    /// <summary>Recurso Patient mínimo para registrar/atualizar no dcm4chee antes do
    /// MWL item (o <c>POST /mwlitems</c> exige que o paciente já exista no arquivo).</summary>
    public static JsonObject Paciente(SolicitacaoExame s, PacienteResumo p) => new()
    {
        ["00100010"] = Pn(p.Nome),
        ["00100020"] = Lo(PatientId(s, p)),
        ["00100030"] = Da(p.DataNascimento),
        ["00100040"] = Cs(MapearSexo(p.Sexo)),
    };

    // ---- helpers DICOM+JSON ----

    private static JsonObject ComVr(string vr, string v) => new() { ["vr"] = vr, ["Value"] = new JsonArray(v) };
    private static JsonObject Sh(string v) => ComVr("SH", v);
    private static JsonObject Lo(string v) => ComVr("LO", v);
    private static JsonObject Ui(string v) => ComVr("UI", v);
    private static JsonObject Cs(string v) => ComVr("CS", v);
    private static JsonObject Ae(string v) => ComVr("AE", v);
    private static JsonObject Pn(string nome) => new() { ["vr"] = "PN", ["Value"] = new JsonArray(new JsonObject { ["Alphabetic"] = FormatarPn(nome) }) };
    private static JsonObject Da(DateOnly? d) => d is null ? new JsonObject { ["vr"] = "DA" } : ComVr("DA", d.Value.ToString("yyyyMMdd"));

    private static JsonObject SeqCodigoProc(TipoExame t) => new()
    {
        ["vr"] = "SQ",
        ["Value"] = new JsonArray
        {
            new JsonObject
            {
                ["00080100"] = Sh(t.ProcedimentoSigtap?.Codigo ?? "00.00.00.000-0"),
                ["00080102"] = Sh("SIGTAP-DATASUS"),
                ["00080104"] = Lo(t.ProcedimentoSigtap?.Nome ?? t.Nome),
            },
        },
    };

    private static string MapearSexo(Sexo sexo) => sexo switch
    {
        Sexo.Masculino => "M",
        Sexo.Feminino => "F",
        _ => "O",
    };

    private static string MapearPrioridade(PrioridadeSolicitacao p) => p switch
    {
        PrioridadeSolicitacao.Urgente => "STAT",
        PrioridadeSolicitacao.Prioritaria => "HIGH",
        _ => "ROUTINE",
    };

    /// <summary>"João da Silva" → "SILVA^JOÃO DA" (DICOM PN: Family^Given).</summary>
    private static string FormatarPn(string nome)
    {
        var n = (nome ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(n)) return "PACIENTE";
        var partes = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 1) return partes[0].ToUpperInvariant();
        var ultimo = partes[^1].ToUpperInvariant();
        var primeiros = string.Join(' ', partes[..^1]).ToUpperInvariant();
        return $"{ultimo}^{primeiros}";
    }
}
