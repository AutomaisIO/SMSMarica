namespace SMSMarica.Core.Laudos.Assinatura;

/// <summary>
/// Configuração da assinatura digital de laudos (seção <c>Assinatura</c> do
/// appsettings). A assinatura PAdES é montada pelo serviço aberto
/// <c>Automais.Assinador</c> (iText); aqui só apontamos para ele.
/// </summary>
public sealed class AssinaturaOptions
{
    public const string SecaoConfig = "Assinatura";

    /// <summary>URL base do serviço Automais.Assinador (ex.: http://localhost:5082/).</summary>
    public string AssinadorBaseUrl { get; set; } = "http://localhost:5082/";

    /// <summary>Texto do carimbo visual fixo aplicado no rodapé do PDF assinado.</summary>
    public string TextoCarimbo { get; set; } = "Assinado digitalmente via certificado ICP-Brasil";

    /// <summary>Validade (minutos) da chave de uso único entregue ao agente.</summary>
    public int ChaveExpiraMinutos { get; set; } = 3;
}
