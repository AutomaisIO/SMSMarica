namespace SMSMarica.Core.Worklist;

public sealed class Dcm4cheeMwlOptions
{
    public const string SecaoConfig = "Pacs:Dcm4chee";

    /// <summary>Base URL do AE de worklist (WORK-CDT) do dcm4chee — os endpoints
    /// <c>mwlitems</c> e <c>patients</c> ficam abaixo dela. Ex.:
    /// <c>http://pacs.../dcm4chee-arc/aets/WORK-CDT/rs/</c></summary>
    public string WorklistBaseUrl { get; set; } = string.Empty;

    // O AE Title da estação NÃO é mais configuração: vem do Equipamento cadastrado
    // (unidade executante + modalidade) via IResolvedorEstacaoWorklist. Sem equipamento,
    // o envio falha com "Sem equipamento configurado" em vez de cair num AE genérico.
}
