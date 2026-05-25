namespace SMSMarica.Core.Worklist;

public sealed class Dcm4cheeUpsOptions
{
    public const string SecaoConfig = "Pacs:Dcm4chee";

    /// <summary>Base URL UPS-RS — aponta para o AE WORKLIST do dcm4chee
    /// (DCM4CHEE não responde UPS). Ex.: http://pacs.../dcm4chee-arc/aets/WORKLIST/rs/</summary>
    public string UpsBaseUrl { get; set; } = string.Empty;

    /// <summary>AE Title da estação (equipamento) que deve executar — vai em
    /// ScheduledStationNameCodeSequence (DICOM tag 0040,4025).</summary>
    public string StationAeTitle { get; set; } = "MAMO-SIM";
}
