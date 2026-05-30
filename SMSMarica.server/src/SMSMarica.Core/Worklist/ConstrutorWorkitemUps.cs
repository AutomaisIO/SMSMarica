using System.Text.Json.Nodes;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Worklist;

/// <summary>
/// Converte uma <see cref="SolicitacaoExame"/> em payload DICOM+JSON aceito pelo
/// UPS-RS do dcm4chee. As tags seguem o IOD da Unified Procedure Step (PS3.3 C.7);
/// formato JSON conforme PS3.18 Annex F.
/// </summary>
internal static class ConstrutorWorkitemUps
{
    /// <summary>
    /// Gera array com 1 elemento (UPS-RS aceita arrays de workitems no POST).
    /// Espera <c>solicitacao.Paciente.Usuario</c> e <c>solicitacao.TipoExame</c>
    /// carregados.
    /// </summary>
    public static JsonArray Construir(SolicitacaoExame s, string aeTitleEstacao)
    {
        ArgumentNullException.ThrowIfNull(s);
        var paciente = s.Paciente ?? throw new InvalidOperationException("Paciente não carregado.");
        var tipoExame = s.TipoExame ?? throw new InvalidOperationException("TipoExame não carregado.");
        var usuarioPaciente = paciente.Usuario ?? throw new InvalidOperationException("Paciente.Usuario não carregado.");

        var quandoAgendado = (s.DataAgendada ?? DateTime.UtcNow).ToString("yyyyMMddHHmmss");

        var workitem = new JsonObject
        {
            // PatientName — formato DICOM PN "ULTIMO^PRIMEIRO"
            ["00100010"] = ValorPn(usuarioPaciente.NomeCompleto),
            // PatientID — usamos o Guid do paciente como ID estável.
            ["00100020"] = ValorLo(paciente.Id.ToString()),
            // PatientBirthDate
            ["00100030"] = usuarioPaciente.DataNascimento.HasValue
                ? ValorDa(usuarioPaciente.DataNascimento.Value.ToString("yyyyMMdd"))
                : ValorVazio("DA"),
            // PatientSex
            ["00100040"] = ValorCs(MapearSexo(usuarioPaciente.Sexo)),

            // AccessionNumber (vai no Study)
            ["00080050"] = ValorSh(s.AccessionNumber),

            // StudyInstanceUID alvo do exame (importante pra amarrar o study que voltar)
            ["0020000D"] = ValorUi(s.StudyInstanceUID),

            // Procedure description
            ["00321060"] = ValorLo(tipoExame.RequestedProcedureDescription),

            // Referenced Request Sequence — descreve o pedido original
            ["0040A370"] = new JsonObject
            {
                ["vr"] = "SQ",
                ["Value"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["00080050"] = ValorSh(s.AccessionNumber),
                        ["00321060"] = ValorLo(tipoExame.RequestedProcedureDescription),
                        ["00321064"] = SequenciaCodigo(tipoExame),
                        ["0040A372"] = new JsonObject  // ReferencedRequestProcedureCodeSequence
                        {
                            ["vr"] = "SQ",
                            ["Value"] = new JsonArray { SequenciaCodigoInline(tipoExame) },
                        },
                        ["00401001"] = ValorSh($"RP-{s.AccessionNumber}"),  // RequestedProcedureID
                    },
                },
            },

            // ScheduledProcedureStepStartDateTime
            ["00404005"] = ValorDt(quandoAgendado),
            // ScheduledStationNameCodeSequence — qual equipamento deve executar.
            // No MVP usamos a string do AE como label (não temos cadastro de equipamentos).
            ["00404025"] = new JsonObject
            {
                ["vr"] = "SQ",
                ["Value"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["00080100"] = ValorSh(aeTitleEstacao),
                        ["00080102"] = ValorSh("99SMSMARICA"),
                        ["00080104"] = ValorLo($"Estação {aeTitleEstacao}"),
                    },
                },
            },
            // InputReadinessState
            ["00404041"] = ValorCs("READY"),
            // ProcedureStepState
            ["00741000"] = ValorCs("SCHEDULED"),
            // ProcedureStepPriority
            ["00741200"] = ValorCs(MapearPrioridade(s.Prioridade)),
            // ProcedureStepLabel — texto humano (aparece em alguns equipamentos)
            ["00741204"] = ValorLo(tipoExame.ScheduledProcedureStepDescription),
        };

        // ScheduledProcessingParametersSequence — modalidade requerida
        workitem["00404026"] = new JsonObject
        {
            ["vr"] = "SQ",
            ["Value"] = new JsonArray
            {
                new JsonObject
                {
                    ["00080100"] = ValorSh(tipoExame.ModalidadeDicom.ToString()),
                    ["00080102"] = ValorSh("DCM"),
                    ["00080104"] = ValorLo($"{tipoExame.ModalidadeDicom}"),
                },
            },
        };

        return new JsonArray(workitem);
    }

    // ---- helpers ----

    private static JsonObject ValorVazio(string vr) => new() { ["vr"] = vr };
    private static JsonObject ValorPn(string nome) => new() { ["vr"] = "PN", ["Value"] = new JsonArray(new JsonObject { ["Alphabetic"] = FormatarPn(nome) }) };
    private static JsonObject ValorLo(string v) => new() { ["vr"] = "LO", ["Value"] = new JsonArray(v) };
    private static JsonObject ValorSh(string v) => new() { ["vr"] = "SH", ["Value"] = new JsonArray(v) };
    private static JsonObject ValorUi(string v) => new() { ["vr"] = "UI", ["Value"] = new JsonArray(v) };
    private static JsonObject ValorCs(string v) => new() { ["vr"] = "CS", ["Value"] = new JsonArray(v) };
    private static JsonObject ValorDa(string v) => new() { ["vr"] = "DA", ["Value"] = new JsonArray(v) };
    private static JsonObject ValorDt(string v) => new() { ["vr"] = "DT", ["Value"] = new JsonArray(v) };

    private static JsonObject SequenciaCodigo(TipoExame t)
    {
        // ProcedureCodeSequence (SIGTAP como sistema de codificação)
        return new JsonObject
        {
            ["vr"] = "SQ",
            ["Value"] = new JsonArray { SequenciaCodigoInline(t) },
        };
    }

    private static JsonObject SequenciaCodigoInline(TipoExame t)
    {
        var codigoSigtap = t.ProcedimentoSigtap?.Codigo ?? "00.00.00.000-0";
        return new JsonObject
        {
            ["00080100"] = ValorSh(codigoSigtap),                                  // CodeValue
            ["00080102"] = ValorSh("SIGTAP-DATASUS"),                              // CodingSchemeDesignator
            ["00080104"] = ValorLo(t.ProcedimentoSigtap?.Nome ?? t.Nome),          // CodeMeaning
        };
    }

    /// <summary>"João da Silva" → "SILVA^JOÃO DA"</summary>
    private static string FormatarPn(string nome)
    {
        var n = (nome ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(n)) return "ANONIMO";
        var partes = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 1) return partes[0].ToUpperInvariant();
        var ultimo = partes[^1].ToUpperInvariant();
        var primeiros = string.Join(' ', partes[..^1]).ToUpperInvariant();
        return $"{ultimo}^{primeiros}";
    }

    private static string MapearSexo(Data.Entities.Enums.Sexo? sexo) => sexo switch
    {
        Data.Entities.Enums.Sexo.Masculino => "M",
        Data.Entities.Enums.Sexo.Feminino => "F",
        _ => "O",
    };

    private static string MapearPrioridade(Data.Entities.Enums.PrioridadeSolicitacao p) => p switch
    {
        Data.Entities.Enums.PrioridadeSolicitacao.Urgente => "HIGH",
        Data.Entities.Enums.PrioridadeSolicitacao.Prioritaria => "MEDIUM",
        _ => "LOW",
    };
}
