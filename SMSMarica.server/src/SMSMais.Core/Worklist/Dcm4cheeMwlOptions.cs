namespace SMSMais.Core.Worklist;

public sealed class Dcm4cheeMwlOptions
{
    public const string SecaoConfig = "Pacs:Dcm4chee";

    /// <summary>Base URL do AE ADMINISTRATIVO de worklist (<c>WORKLIST</c>) — sem
    /// <c>dcmMWLWorklistLabel</c>, enxerga todos os itens; é por ele que criamos,
    /// confirmamos e removemos (<c>mwlitems</c> + <c>patients</c>). Os equipamentos usam
    /// os AEs com label (WORK-CDT, WORK-CMI), que só devolvem a lista da própria estação.
    /// Ex.: <c>http://pacs.../dcm4chee-arc/aets/WORKLIST/rs/</c></summary>
    public string WorklistBaseUrl { get; set; } = string.Empty;

    // O AE Title da estação NÃO é mais configuração: vem do Equipamento cadastrado
    // (unidade executante + modalidade) via IResolvedorEstacaoWorklist. Sem equipamento,
    // o envio falha com "Sem equipamento configurado" em vez de cair num AE genérico.
}
