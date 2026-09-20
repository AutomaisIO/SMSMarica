using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Midias;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>
/// As artes que a plataforma hospeda para este cliente — hoje, a imagem de cabeçalho dos modelos
/// com foto no topo.
///
/// <para>A Meta rebaixa a URL a <b>cada</b> mensagem enviada, então a arte precisa de endereço
/// público e estável. Subir aqui resolve isso sem depender de o município ter onde publicar
/// arquivo estático — e sem o passo de "commitar a imagem no front e esperar o deploy".</para>
/// </summary>
public sealed class MidiasModel(
    EscopoUsuario escopo, IMidiaService midias, ITemplateArteService artes) : PageModel
{
    /// <summary>Categoria única por enquanto; o campo existe para não precisar de migration quando
    /// aparecer outro uso (rodapé de documento, por exemplo).</summary>
    private const string Categoria = "cabecalho";

    public Data.Entities.Tenant? Alvo { get; private set; }
    public IReadOnlyList<MidiaGuardada> Artes { get; private set; } = [];

    /// <summary>Modelos que apontam para cada arte — apagar uma em uso quebra aquele envio.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<string>> EmUso { get; private set; } =
        new Dictionary<Guid, IReadOnlyList<string>>();

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public string UrlDe(MidiaGuardada m) => $"{Request.Scheme}://{Request.Host}{m.Caminho}";

    /// <summary>O WhatsApp mostra o cabeçalho deitado; arte muito alta chega cortada no celular.</summary>
    public static bool ProporcaoEstranha(MidiaGuardada m)
        => m is { Largura: > 0, Altura: > 0 } && (double)m.Largura.Value / m.Altura.Value < 1.2;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await CarregarAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostEnviarAsync(IFormFile? arquivo, CancellationToken ct)
    {
        Alvo = await escopo.SelecionadoAsync(ct);
        if (Alvo is null) return RedirectToPage();

        if (arquivo is null || arquivo.Length == 0)
        {
            Erro = "Escolha um arquivo.";
            return RedirectToPage();
        }

        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);

        var (midia, erro) = await midias.GuardarAsync(
            Alvo.Id, arquivo.FileName, arquivo.ContentType, ms.ToArray(),
            Categoria, escopo.UsuarioId, ct);

        if (midia is null) Erro = erro;
        else Recado = $"Arte \"{midia.NomeArquivo}\" publicada.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApagarAsync(Guid midiaId, CancellationToken ct)
    {
        Alvo = await escopo.SelecionadoAsync(ct);
        if (Alvo is null) return RedirectToPage();

        // Apagar arte ainda escolhida por um modelo derrubaria o envio dele em silêncio.
        var emUso = await artes.EmUsoPorAsync(midiaId, ct);
        if (emUso.Count > 0)
        {
            Erro = $"Esta arte ainda é usada por {string.Join(", ", emUso)}. "
                   + "Troque a arte desses modelos antes de apagar.";
            return RedirectToPage();
        }

        Recado = await midias.ApagarAsync(Alvo.Id, midiaId, ct) ? "Arte apagada." : null;
        return RedirectToPage();
    }

    private async Task CarregarAsync(CancellationToken ct)
    {
        Alvo = await escopo.SelecionadoAsync(ct);
        if (Alvo is null) return;

        Artes = await midias.ListarAsync(Alvo.Id, Categoria, ct);

        var uso = new Dictionary<Guid, IReadOnlyList<string>>();
        foreach (var a in Artes)
        {
            var modelos = await artes.EmUsoPorAsync(a.Id, ct);
            if (modelos.Count > 0) uso[a.Id] = modelos;
        }
        EmUso = uso;
    }
}
