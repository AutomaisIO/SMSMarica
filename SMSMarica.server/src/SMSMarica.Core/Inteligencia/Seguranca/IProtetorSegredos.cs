namespace SMSMarica.Core.Inteligencia.Seguranca;

/// <summary>
/// Cifra/decifra segredos do módulo IA (token do provedor, senha das bases) em repouso.
/// Implementação na Api via ASP.NET Data Protection (IDataProtector). Os valores cifrados nunca
/// são devolvidos ao front (write-only nas APIs de configuração).
/// </summary>
public interface IProtetorSegredos
{
    string Proteger(string textoPuro);
    string Revelar(string textoCifrado);
}
