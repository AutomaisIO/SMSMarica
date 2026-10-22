namespace SMSMais.Core.Cidadao.Entrada;

/// <summary>
/// Por onde o cidadão está entrando: o PWA (navegador) ou o app nativo. O app manda no cabeçalho
/// <see cref="Cabecalho"/>; sem ele, vale o PWA. É preferência de produto (cada canal tem a sua
/// configuração), não fronteira de segurança — as travas da entrada valem nos dois (ADR-0078).
/// </summary>
public static class CanalAppCidadao
{
    public const string Cabecalho = "X-App-Canal";

    public const string Pwa = "pwa";
    public const string Nativo = "nativo";

    public static string Normalizar(string? valor) =>
        string.Equals(valor?.Trim(), Nativo, StringComparison.OrdinalIgnoreCase) ? Nativo : Pwa;
}
