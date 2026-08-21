using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class IndexModel(ZapDbContext db, TimeProvider relogio) : PageModel
{
    public List<Destino> Destinos { get; private set; } = [];

    [TempData]
    public string? Recado { get; set; }

    [TempData]
    public string? Erro { get; set; }

    public async Task OnGetAsync(CancellationToken ct) => await CarregarAsync(ct);

    private async Task CarregarAsync(CancellationToken ct)
    {
        Destinos = await db.Destinos
            .AsNoTracking()
            .Include(d => d.Numeros.OrderBy(n => n.PhoneNumberId))
            .OrderBy(d => d.Nome)
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostNovoDestinoAsync(
        string nome, string urlWebhook, string? observacao, CancellationToken ct)
    {
        nome = (nome ?? string.Empty).Trim();
        urlWebhook = (urlWebhook ?? string.Empty).Trim();

        if (nome.Length == 0 || urlWebhook.Length == 0)
        {
            Erro = "Nome e URL do webhook são obrigatórios.";
            return RedirectToPage();
        }

        // Só http/https absoluto: a URL vira destino de um POST feito pelo servidor, então
        // um esquema exótico aqui é superfície de SSRF, não conveniência.
        if (!Uri.TryCreate(urlWebhook, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Erro = "A URL do webhook precisa ser http(s) absoluta.";
            return RedirectToPage();
        }

        if (await db.Destinos.AnyAsync(d => d.Nome == nome, ct))
        {
            Erro = $"Já existe um destino chamado \"{nome}\".";
            return RedirectToPage();
        }

        db.Destinos.Add(new Destino
        {
            Nome = nome,
            UrlWebhook = urlWebhook,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            Ativo = true,
            CriadoEm = relogio.GetUtcNow(),
        });

        await db.SaveChangesAsync(ct);
        Recado = $"Destino \"{nome}\" criado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAlternarDestinoAsync(Guid id, CancellationToken ct)
    {
        var destino = await db.Destinos.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (destino is null) return RedirectToPage();

        destino.Ativo = !destino.Ativo;
        destino.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);

        Recado = destino.Ativo
            ? $"Canal de \"{destino.Nome}\" religado."
            : $"Canal de \"{destino.Nome}\" suspenso — os números dele param de receber agora.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditarDestinoAsync(
        Guid id, string urlWebhook, CancellationToken ct)
    {
        urlWebhook = (urlWebhook ?? string.Empty).Trim();
        if (!Uri.TryCreate(urlWebhook, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Erro = "A URL do webhook precisa ser http(s) absoluta.";
            return RedirectToPage();
        }

        var destino = await db.Destinos.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (destino is null) return RedirectToPage();

        destino.UrlWebhook = urlWebhook;
        destino.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);

        Recado = $"URL de \"{destino.Nome}\" atualizada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostNovoNumeroAsync(
        Guid destinoId, string phoneNumberId, string? wabaId,
        string? displayPhoneNumber, string? rotulo, CancellationToken ct)
    {
        phoneNumberId = (phoneNumberId ?? string.Empty).Trim();
        if (phoneNumberId.Length == 0)
        {
            Erro = "O phone_number_id é obrigatório — é por ele que o roteamento acontece.";
            return RedirectToPage();
        }

        if (await db.Numeros.AnyAsync(n => n.PhoneNumberId == phoneNumberId, ct))
        {
            Erro = $"O número {phoneNumberId} já está cadastrado em algum destino.";
            return RedirectToPage();
        }

        if (!await db.Destinos.AnyAsync(d => d.Id == destinoId, ct))
        {
            Erro = "Destino inexistente.";
            return RedirectToPage();
        }

        db.Numeros.Add(new Numero
        {
            DestinoId = destinoId,
            PhoneNumberId = phoneNumberId,
            WabaId = Vazio(wabaId),
            DisplayPhoneNumber = Vazio(displayPhoneNumber),
            Rotulo = Vazio(rotulo),
            Ativo = true,
            CriadoEm = relogio.GetUtcNow(),
        });

        await db.SaveChangesAsync(ct);
        Recado = $"Número {phoneNumberId} cadastrado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAlternarNumeroAsync(Guid id, CancellationToken ct)
    {
        var numero = await db.Numeros.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (numero is null) return RedirectToPage();

        numero.Ativo = !numero.Ativo;
        numero.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);

        Recado = $"Número {numero.PhoneNumberId} {(numero.Ativo ? "religado" : "desligado")}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoverNumeroAsync(Guid id, CancellationToken ct)
    {
        var numero = await db.Numeros.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (numero is null) return RedirectToPage();

        db.Numeros.Remove(numero);
        await db.SaveChangesAsync(ct);

        Recado = $"Número {numero.PhoneNumberId} removido. Eventos dele passam a cair sem rota.";
        return RedirectToPage();
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
