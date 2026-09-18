using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.Mensageria;

/// <summary>Categorias de cobrança da Meta para mensagens de template.</summary>
public static class CategoriaCobrancaMeta
{
    public const string Utility = "utility";
    public const string Marketing = "marketing";
    public const string Authentication = "authentication";

    public static readonly IReadOnlyList<string> Todas = [Utility, Marketing, Authentication];

    /// <summary>Categoria de um template: o mapa configurado vence; sem entrada, o OTP
    /// (<c>authzap</c>, ou qualquer nome com "auth") é autenticação e o resto é utility — é o
    /// caso de todos os nossos templates aprovados.</summary>
    public static string DoTemplate(string template, IReadOnlyDictionary<string, string> mapa)
    {
        if (mapa.TryGetValue(template, out var c) && Todas.Contains(c)) return c;
        return template.Contains("auth", StringComparison.OrdinalIgnoreCase) ? Authentication : Utility;
    }
}

public sealed record MensageriaConfiguracaoDto(
    decimal? TarifaUtilityUsd,
    decimal? TarifaMarketingUsd,
    decimal? TarifaAuthenticationUsd,
    IReadOnlyDictionary<string, string> TemplatesCategorias,
    DateTime? AtualizadoEm)
{
    public bool TarifaCadastrada => TarifaUtilityUsd is not null || TarifaMarketingUsd is not null || TarifaAuthenticationUsd is not null;

    public decimal? Tarifa(string categoria) => categoria switch
    {
        CategoriaCobrancaMeta.Marketing => TarifaMarketingUsd,
        CategoriaCobrancaMeta.Authentication => TarifaAuthenticationUsd,
        _ => TarifaUtilityUsd,
    };
}

public sealed record SalvarMensageriaConfiguracaoRequest(
    decimal? TarifaUtilityUsd,
    decimal? TarifaMarketingUsd,
    decimal? TarifaAuthenticationUsd,
    IReadOnlyDictionary<string, string>? TemplatesCategorias);

/// <summary>Tarifas (USD) e mapa template → categoria para a ESTIMATIVA de custo Meta.</summary>
public interface IMensageriaConfiguracaoService
{
    Task<MensageriaConfiguracaoDto> ObterAsync(CancellationToken ct = default);
    Task<MensageriaConfiguracaoDto> SalvarAsync(SalvarMensageriaConfiguracaoRequest request, CancellationToken ct = default);
}

public sealed class MensageriaConfiguracaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IMensageriaConfiguracaoService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<MensageriaConfiguracaoDto> ObterAsync(CancellationToken ct = default)
    {
        var c = await db.MensageriaConfiguracoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == MensageriaConfiguracao.IdFixo, ct)
            ?? new MensageriaConfiguracao();
        return Mapear(c);
    }

    public async Task<MensageriaConfiguracaoDto> SalvarAsync(
        SalvarMensageriaConfiguracaoRequest request, CancellationToken ct = default)
    {
        foreach (var (nome, valor) in new[]
                 {
                     ("utility", request.TarifaUtilityUsd), ("marketing", request.TarifaMarketingUsd),
                     ("authentication", request.TarifaAuthenticationUsd),
                 })
        {
            if (valor is < 0 or > 10)
                throw new ValidacaoException("mensageria.tarifa_invalida",
                    $"A tarifa de {nome} deve ficar entre 0 e 10 USD por mensagem.");
        }

        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (template, categoria) in request.TemplatesCategorias ?? new Dictionary<string, string>())
        {
            var t = template.Trim();
            var cat = categoria.Trim().ToLowerInvariant();
            if (t.Length == 0) continue;
            if (!CategoriaCobrancaMeta.Todas.Contains(cat))
                throw new ValidacaoException("mensageria.categoria_invalida",
                    $"Categoria \"{categoria}\" do template {t} não existe (utility, marketing ou authentication).");
            mapa[t] = cat;
        }

        var agora = DateTime.UtcNow;
        var c = await db.MensageriaConfiguracoes
            .FirstOrDefaultAsync(x => x.Id == MensageriaConfiguracao.IdFixo, ct);
        if (c is null)
        {
            c = new MensageriaConfiguracao { CriadoEm = agora };
            db.MensageriaConfiguracoes.Add(c);
        }
        c.TarifaUtilityUsd = request.TarifaUtilityUsd;
        c.TarifaMarketingUsd = request.TarifaMarketingUsd;
        c.TarifaAuthenticationUsd = request.TarifaAuthenticationUsd;
        c.TemplatesCategoriasJson = mapa.Count == 0 ? null : JsonSerializer.Serialize(mapa, JsonOpts);
        c.AtualizadoEm = agora;
        c.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        return Mapear(c);
    }

    private static MensageriaConfiguracaoDto Mapear(MensageriaConfiguracao c)
    {
        IReadOnlyDictionary<string, string> mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(c.TemplatesCategoriasJson))
        {
            try
            {
                var lido = JsonSerializer.Deserialize<Dictionary<string, string>>(c.TemplatesCategoriasJson, JsonOpts);
                if (lido is not null) mapa = new Dictionary<string, string>(lido, StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                // JSON corrompido à mão no banco: ignora o mapa, as tarifas continuam valendo.
            }
        }
        return new MensageriaConfiguracaoDto(
            c.TarifaUtilityUsd, c.TarifaMarketingUsd, c.TarifaAuthenticationUsd, mapa, c.AtualizadoEm);
    }
}
