using SMSMais.Core.Institucional;
using SMSMais.Core.Midias;

namespace SMSMais.Core.Exames;

/// <summary>
/// Identidade visual das capas dos PDFs de exame, resolvida da instituição desta
/// instância (ADR-0043/0046): nome curto, cor da marca, variante escura e logo (mídia).
/// Sem instituição configurada, o resultado é NEUTRO — nunca a marca de outro município.
/// </summary>
public sealed record IdentidadeVisualPdf(string Nome, string CorPrimaria, string CorEscura, byte[]? Logo)
{
    private const string CorNeutra = "#475569";

    public static async Task<IdentidadeVisualPdf> ResolverAsync(
        IInstituicaoService instituicao,
        IMidiasService midias,
        CancellationToken ct)
    {
        var inst = await instituicao.ObterAsync(ct);
        var cor = string.IsNullOrWhiteSpace(inst.CorPrimaria) ? CorNeutra : inst.CorPrimaria!;

        byte[]? logo = null;
        if (inst.LogoMidiaId is { } logoId)
        {
            try { logo = (await midias.ObterConteudoAsync(logoId, ct))?.Conteudo; }
            catch { /* mídia removida: capa sai sem logo, nunca com o de outro município */ }
        }

        return new IdentidadeVisualPdf(inst.NomeCurto, cor, Escurecer(cor, 0.38), logo);
    }

    /// <summary>Mistura a cor com preto (0..1) — a variante "vinho" das capas.</summary>
    private static string Escurecer(string hex, double fator)
    {
        var n = Convert.ToInt32(hex.TrimStart('#'), 16);
        int C(int v) => Math.Clamp((int)Math.Round(v * (1 - fator)), 0, 255);
        return $"#{C((n >> 16) & 255):X2}{C((n >> 8) & 255):X2}{C(n & 255):X2}";
    }
}
