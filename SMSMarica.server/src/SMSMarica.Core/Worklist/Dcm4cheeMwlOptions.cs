namespace SMSMarica.Core.Worklist;

public sealed class Dcm4cheeMwlOptions
{
    public const string SecaoConfig = "Pacs:Dcm4chee";

    /// <summary>Base URL do AE de worklist (WORK-CDT) do dcm4chee — os endpoints
    /// <c>mwlitems</c> e <c>patients</c> ficam abaixo dela. Ex.:
    /// <c>http://pacs.../dcm4chee-arc/aets/WORK-CDT/rs/</c></summary>
    public string WorklistBaseUrl { get; set; } = string.Empty;

    /// <summary>AE Title da estação (equipamento) que deve executar — vai em
    /// ScheduledStationAETitle (0040,0001) e é o filtro que o equipamento (Fuji
    /// FDR) usa na consulta MWL. Default = <c>FDR-MAMO</c> (mamógrafo do CDT).</summary>
    public string StationAeTitle { get; set; } = "FDR-MAMO";
}
