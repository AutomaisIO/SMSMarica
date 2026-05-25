namespace SMSMarica.Core.Worklist;

public sealed class Dcm4cheeUpsOptions
{
    public const string SecaoConfig = "Pacs:Dcm4chee";

    /// <summary>Base URL UPS-RS. Aponta para o AE DCM4CHEE no deploy unsecure
    /// da SMS Maricá. Ex.: http://pacs.../dcm4chee-arc/aets/DCM4CHEE/rs/</summary>
    public string UpsBaseUrl { get; set; } = string.Empty;

    /// <summary>AE Title da estação (equipamento) que deve executar — vai em
    /// ScheduledStationNameCodeSequence (DICOM tag 0040,4025).</summary>
    public string StationAeTitle { get; set; } = "DCM4CHEE";
}
