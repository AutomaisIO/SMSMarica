namespace SMSMarica.Core.Worklist;

public sealed class Dcm4cheeUpsOptions
{
    public const string SecaoConfig = "Pacs:Dcm4chee";

    /// <summary>Base URL do AE WORKLIST. Ex.: http://pacs.../dcm4chee-arc/aets/WORKLIST/rs/</summary>
    public string UpsBaseUrl { get; set; } = string.Empty;

    /// <summary>AE Title da estação (equipamento) que deve executar. Usado em ScheduledStationNameCodeSequence.</summary>
    public string StationAeTitle { get; set; } = "MAMO-SIM";
}
