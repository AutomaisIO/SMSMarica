using Microsoft.AspNetCore.DataProtection;

namespace Automais.Zap.Core.Seguranca;

public interface IProtetorSegredos
{
    string Proteger(string textoPuro);

    /// <summary>Devolve null quando o texto não decifra — chave trocada ou valor corrompido.</summary>
    string? Revelar(string? textoCifrado);
}

/// <summary>
/// Cifra os segredos da Meta em repouso.
///
/// O <see cref="Proposito"/> entra na derivação da chave: mudar um caractere torna
/// indecifrável tudo que já foi gravado, e falha em runtime, não no build. Não renomear.
/// </summary>
public sealed class ProtetorSegredos : IProtetorSegredos
{
    private const string Proposito = "Automais.Zap.Segredos";

    private readonly IDataProtector _protetor;

    public ProtetorSegredos(IDataProtectionProvider provider) => _protetor = provider.CreateProtector(Proposito);

    public string Proteger(string textoPuro) => _protetor.Protect(textoPuro);

    public string? Revelar(string? textoCifrado)
    {
        if (string.IsNullOrWhiteSpace(textoCifrado)) return null;
        try
        {
            return _protetor.Unprotect(textoCifrado);
        }
        catch
        {
            // Perder a chave não pode derrubar o serviço: o chamador trata como "não configurado",
            // que no caminho do webhook significa falhar fechado (503).
            return null;
        }
    }
}
