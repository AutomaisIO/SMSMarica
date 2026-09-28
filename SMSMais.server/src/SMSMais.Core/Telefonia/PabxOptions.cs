namespace SMSMais.Core.Telefonia;

/// <summary>
/// Conexão com o Automais.Pabx, a API da VM de telefonia. Uma VM por instância (ADR-0043):
/// URL e chave vêm de variável de ambiente (<c>Telefonia__Pabx__BaseUrl</c>,
/// <c>Telefonia__Pabx__ApiKey</c>), nunca de migration.
/// </summary>
public sealed class PabxOptions
{
    public const string Secao = "Telefonia:Pabx";

    /// <summary>Ex.: <c>https://telefonia.smsmarica.online/</c>. Vazio = telefonia desligada.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Chave de serviço do Pabx (header <c>X-Api-Key</c>).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Unidade do Pabx onde os softphones são inventariados (0 = Complexo Regulador).</summary>
    public int UnidadeSoftphone { get; set; }

    public int TimeoutSegundos { get; set; } = 15;

    public bool Configurado => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}
