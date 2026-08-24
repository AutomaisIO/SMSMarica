namespace SMSMais.Core.Integracoes.Sisreg;

/// <summary>
/// Os três índices Elasticsearch do SISREG (Manual v2.1 §3). O prefixo é combinado
/// com UF+município (escopo municipal) ou com <c>-nacional</c> (escopo federal).
/// </summary>
public enum TipoIndiceSisreg
{
    MarcacaoAmbulatorial = 1,
    SolicitacaoAmbulatorial = 2,
    SolicitacaoHospitalar = 3,
}

public static class TipoIndiceSisregExtensions
{
    /// <summary>Prefixo do índice conforme o manual (ex.: <c>marcacao-ambulatorial</c>).</summary>
    public static string Prefixo(this TipoIndiceSisreg tipo) => tipo switch
    {
        TipoIndiceSisreg.MarcacaoAmbulatorial => "marcacao-ambulatorial",
        TipoIndiceSisreg.SolicitacaoAmbulatorial => "solicitacao-ambulatorial",
        TipoIndiceSisreg.SolicitacaoHospitalar => "solicitacao-hospitalar",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };
}
