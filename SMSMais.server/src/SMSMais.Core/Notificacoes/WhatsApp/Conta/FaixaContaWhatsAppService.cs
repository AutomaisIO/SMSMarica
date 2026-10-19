using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMais.Core.Identidade;
using SMSMais.Data;

namespace SMSMais.Core.Notificacoes.WhatsApp.Conta;

/// <summary>O que a faixa vermelha do painel mostra. <c>Mostrar=false</c> não traz mais nada.</summary>
public sealed record FaixaContaWhatsAppDto(
    bool Mostrar,
    DateTime? Desde = null,
    string? Codigo = null,
    string? Motivo = null,
    string? Origem = null,
    DateTime? UltimaConfirmacaoEm = null,
    DateTime? ProximaSondaEm = null,
    int EnviosSegurados = 0,
    DateTime? UltimaConsultaSaudeEm = null);

public interface IFaixaContaWhatsAppService
{
    Task<FaixaContaWhatsAppDto> ObterAsync(CancellationToken ct = default);
}

/// <summary>
/// A faixa de "WhatsApp parado" é para quem resolve (pedido do Bernardo, 19/10/2026: "somente para
/// Bernardo"): os logins de <see cref="ContaWhatsAppOptions.FaixaParaLogins"/>, ou quem tem acesso
/// global se a lista estiver vazia — vazia não pode virar "ninguém vê", que foi o furo dos avisos
/// no celular em 09/2026 (lista vazia, 2.377 avisos sem destino).
///
/// <para>Conta funcionando responde sem ir ao banco: todo painel aberto pergunta de minuto em minuto.</para>
/// </summary>
public sealed class FaixaContaWhatsAppService(
    ISituacaoContaWhatsApp situacao,
    IUsuarioAtualAccessor usuarioAtual,
    SmsMaisDbContext db,
    IOptions<ContaWhatsAppOptions> options) : IFaixaContaWhatsAppService
{
    public async Task<FaixaContaWhatsAppDto> ObterAsync(CancellationToken ct = default)
    {
        var parada = situacao.Parada;
        if (parada is null || usuarioAtual.UsuarioId is not { } usuarioId) return new FaixaContaWhatsAppDto(false);

        var usuario = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId && u.Ativo && u.ExcluidoEm == null)
            .Select(u => new { u.Login, u.AcessoGlobal })
            .FirstOrDefaultAsync(ct);
        if (usuario is null) return new FaixaContaWhatsAppDto(false);

        var logins = options.Value.FaixaParaLogins.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        var ve = logins.Length > 0
            ? usuario.Login is { } login && logins.Contains(login.Trim(), StringComparer.OrdinalIgnoreCase)
            : usuario.AcessoGlobal;
        if (!ve) return new FaixaContaWhatsAppDto(false);

        return new FaixaContaWhatsAppDto(
            true, parada.Desde, parada.Codigo, parada.Motivo, parada.Origem,
            parada.UltimaConfirmacaoEm, parada.ProximaSondaEm, parada.EnviosSegurados,
            situacao.UltimaConsultaSaudeEm);
    }
}
