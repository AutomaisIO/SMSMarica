using System.Reflection;

namespace SMSMarica.Core.Exames;

/// <summary>Recursos embutidos compartilhados pelos PDFs de exame (logo institucional).</summary>
public static class ExameRecursos
{
    private static readonly Lazy<byte[]?> LogoLazy = new(CarregarLogo);

    /// <summary>Logo "Saúde Maricá" embutido (PNG) usado nas capas dos PDFs. Null se ausente.</summary>
    public static byte[]? Logo => LogoLazy.Value;

    private static byte[]? CarregarLogo()
    {
        var asm = Assembly.GetExecutingAssembly();
        var nome = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("marica_logo.png", StringComparison.OrdinalIgnoreCase));
        if (nome is null) return null;
        using var stream = asm.GetManifestResourceStream(nome);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
