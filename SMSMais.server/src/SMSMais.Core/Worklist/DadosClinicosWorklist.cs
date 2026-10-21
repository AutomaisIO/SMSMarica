namespace SMSMais.Core.Worklist;

/// <summary>
/// Dados clínicos do paciente que vão no item de worklist, vindos da anamnese (hoje só a do
/// Holter): (0010,1020) Patient's Size em metros, (0010,1030) Patient's Weight em quilos e
/// (0010,21A0) Smoking Status (YES / NO / UNKNOWN).
/// </summary>
public sealed record DadosClinicosWorklist(decimal? AlturaMetros, decimal? PesoKg, string? SmokingStatus);
