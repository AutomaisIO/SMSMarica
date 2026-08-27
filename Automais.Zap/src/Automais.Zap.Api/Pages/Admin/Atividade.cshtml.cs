using System.Globalization;
using System.Text;
using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Meta;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>
/// Atividade do canal: a trilha local da plataforma dia a dia e as métricas que a própria
/// Meta reporta por WABA (mensagens e conversas). Gráficos são SVG gerados aqui — sem
/// biblioteca, sem chamada externa.
/// </summary>
public sealed class AtividadeModel(ZapDbContext db, EscopoUsuario escopo, IGraphMetaClient graph) : PageModel
{
    /// <summary>Regra única de fuso do produto: Brasília, fixo.</summary>
    private static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    private static readonly CultureInfo PtBr = new("pt-BR");

    public sealed record Dia(DateOnly Data, int Entregues, int Falhas, int SemRota);

    public sealed record VisaoWaba(
        string Nome,
        string? Moeda,
        IReadOnlyList<PontoAnalytics> Mensagens,
        string? ErroMensagens,
        IReadOnlyList<CategoriaCobranca> Cobranca,
        string? ErroCobranca);

    public Data.Entities.Tenant? Alvo { get; private set; }
    public List<Dia> Dias { get; private set; } = [];
    public List<VisaoWaba> Wabas { get; private set; } = [];

    public int Entregues7d { get; private set; }
    public int Falhas7d { get; private set; }
    public int SemRota7d { get; private set; }
    public int MediaMs7d { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Alvo = await escopo.SelecionadoAsync(ct);
        if (Alvo is null) return Page();

        var agora = DateTimeOffset.UtcNow;
        var corte14 = agora.AddDays(-14);

        // Projeção mínima e agregação aqui: agrupar por dia de Brasília não traduz para SQL.
        var eventos = await db.EntregasLog.AsNoTracking()
            .Where(x => x.TenantId == Alvo.Id && x.RecebidoEm >= corte14)
            .Select(x => new { x.RecebidoEm, x.Sucesso, SemRota = x.Erro == "sem rota cadastrada", x.DuracaoMs })
            .ToListAsync(ct);

        var hoje = DateOnly.FromDateTime(agora.ToOffset(Brasilia).DateTime);
        var porDia = eventos
            .GroupBy(e => DateOnly.FromDateTime(e.RecebidoEm.ToOffset(Brasilia).DateTime))
            .ToDictionary(g => g.Key, g => g);

        for (var i = 13; i >= 0; i--)
        {
            var dia = hoje.AddDays(-i);
            var grupo = porDia.GetValueOrDefault(dia);
            Dias.Add(new Dia(
                dia,
                grupo?.Count(e => e.Sucesso) ?? 0,
                grupo?.Count(e => !e.Sucesso && !e.SemRota) ?? 0,
                grupo?.Count(e => e.SemRota) ?? 0));
        }

        var corte7 = agora.AddDays(-7);
        var ultimos7 = eventos.Where(e => e.RecebidoEm >= corte7).ToList();
        Entregues7d = ultimos7.Count(e => e.Sucesso);
        Falhas7d = ultimos7.Count(e => !e.Sucesso && !e.SemRota);
        SemRota7d = ultimos7.Count(e => e.SemRota);
        MediaMs7d = ultimos7.Count(e => e.Sucesso) > 0
            ? (int)ultimos7.Where(e => e.Sucesso).Average(e => e.DuracaoMs)
            : 0;

        // Métricas da Meta, por WABA. Cada bloco falha sozinho — um WABA sem permissão não
        // derruba a página.
        var wabas = await db.Wabas.AsNoTracking()
            .Where(w => w.TenantId == Alvo.Id)
            .OrderBy(w => w.Nome ?? w.WabaId)
            .ToListAsync(ct);

        var inicio30 = agora.AddDays(-30);
        foreach (var w in wabas)
        {
            var info = await graph.ObterWabaAsync(w.WabaId, ct);
            var mensagens = await graph.ObterAnalyticsAsync(w.WabaId, inicio30, agora, ct);
            var cobranca = await graph.ObterCobrancaAsync(w.WabaId, inicio30, agora, ct);

            Wabas.Add(new VisaoWaba(
                w.Nome ?? w.WabaId,
                info.Sucesso ? info.Valor!.Moeda : null,
                mensagens.Sucesso ? mensagens.Valor! : [],
                mensagens.Sucesso ? null : mensagens.Erro,
                cobranca.Sucesso ? cobranca.Valor! : [],
                cobranca.Sucesso ? null : cobranca.Erro));
        }

        return Page();
    }

    // ------------------------------------------------------------- gráficos

    private const int Largura = 720;

    public static string FormatarInt(long n) => n.ToString("N0", PtBr);

    public static string RotuloCategoria(string categoria) => categoria.ToUpperInvariant() switch
    {
        "MARKETING" => "Marketing",
        "UTILITY" => "Utilidade",
        "AUTHENTICATION" => "Autenticação",
        "AUTHENTICATION_INTERNATIONAL" => "Autenticação internacional",
        "SERVICE" => "Atendimento",
        "REFERRAL_CONVERSION" => "Ponto de entrada gratuito",
        var outra => outra,
    };

    /// <summary>
    /// Barras diárias de uma série só. Topo arredondado ancorado na base, folga de 2px entre
    /// barras, grade recessiva, hover nativo por <c>&lt;title&gt;</c>.
    /// </summary>
    public IHtmlContent Barras(IReadOnlyList<(DateOnly Dia, int Valor)> pontos, string cor, string nomeSerie)
    {
        const int alturaPlot = 120, alturaTotal = 148, topo = 8;
        var max = Math.Max(1, pontos.Count == 0 ? 1 : pontos.Max(p => p.Valor));
        var passo = (double)Largura / Math.Max(1, pontos.Count);
        var larguraBarra = Math.Max(4.0, passo - 4);

        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 {Largura} {alturaTotal}\" width=\"100%\" role=\"img\" aria-label=\"{nomeSerie} por dia\" style=\"display:block\">");

        // grade: três linhas discretas
        for (var g = 1; g <= 3; g++)
        {
            var y = topo + alturaPlot - alturaPlot * g / 3.0;
            sb.Append($"<line x1=\"0\" y1=\"{Num(y)}\" x2=\"{Largura}\" y2=\"{Num(y)}\" stroke=\"#EDF0F6\" stroke-width=\"1\"/>");
        }
        sb.Append($"<text x=\"{Largura}\" y=\"{topo + 4}\" text-anchor=\"end\" font-size=\"10\" fill=\"#8A93AA\">{FormatarInt(max)}</text>");

        for (var i = 0; i < pontos.Count; i++)
        {
            var (dia, valor) = pontos[i];
            var h = valor == 0 ? 0 : Math.Max(3.0, alturaPlot * valor / max);
            var x = i * passo + (passo - larguraBarra) / 2;
            var y = topo + alturaPlot - h;
            var titulo = $"{dia.ToString("dd/MM", PtBr)}: {FormatarInt(valor)} {nomeSerie}";
            if (valor > 0)
            {
                sb.Append($"<path d=\"M{Num(x)} {Num(topo + alturaPlot)} V{Num(y + 4)} Q{Num(x)} {Num(y)} {Num(x + 4)} {Num(y)} H{Num(x + larguraBarra - 4)} Q{Num(x + larguraBarra)} {Num(y)} {Num(x + larguraBarra)} {Num(y + 4)} V{Num(topo + alturaPlot)} Z\" fill=\"{cor}\"><title>{titulo}</title></path>");
            }
            else
            {
                sb.Append($"<rect x=\"{Num(x)}\" y=\"{topo + alturaPlot - 1}\" width=\"{Num(larguraBarra)}\" height=\"1\" fill=\"#D5DBE7\"><title>{titulo}</title></rect>");
            }

            if (i % 2 == 0)
            {
                sb.Append($"<text x=\"{Num(x + larguraBarra / 2)}\" y=\"{alturaTotal - 6}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#8A93AA\">{dia.ToString("dd/MM", PtBr)}</text>");
            }
        }

        sb.Append($"<line x1=\"0\" y1=\"{topo + alturaPlot}\" x2=\"{Largura}\" y2=\"{topo + alturaPlot}\" stroke=\"#D5DBE7\" stroke-width=\"1\"/>");
        sb.Append("</svg>");
        return new HtmlString(sb.ToString());
    }

    /// <summary>Duas linhas (enviadas × entregues) com rótulo direto no fim de cada uma.</summary>
    public IHtmlContent Linhas(IReadOnlyList<PontoAnalytics> pontos)
    {
        const int alturaPlot = 130, alturaTotal = 168, topo = 10, margemDireita = 84;
        if (pontos.Count == 0) return new HtmlString("");

        var larguraPlot = Largura - margemDireita;
        var max = Math.Max(1, pontos.Max(p => Math.Max(p.Enviadas, p.Entregues)));
        double X(int i) => pontos.Count == 1 ? larguraPlot / 2.0 : (double)larguraPlot * i / (pontos.Count - 1);
        double Y(long v) => topo + alturaPlot - (double)alturaPlot * v / max;

        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 {Largura} {alturaTotal}\" width=\"100%\" role=\"img\" aria-label=\"Mensagens por dia segundo a Meta\" style=\"display:block\">");

        for (var g = 1; g <= 3; g++)
        {
            var y = topo + alturaPlot - alturaPlot * g / 3.0;
            sb.Append($"<line x1=\"0\" y1=\"{Num(y)}\" x2=\"{larguraPlot}\" y2=\"{Num(y)}\" stroke=\"#EDF0F6\" stroke-width=\"1\"/>");
        }
        sb.Append($"<text x=\"{larguraPlot}\" y=\"{topo + 4}\" text-anchor=\"end\" font-size=\"10\" fill=\"#8A93AA\">{FormatarInt(max)}</text>");

        foreach (var (serie, cor, rotulo) in new (Func<PontoAnalytics, long> Serie, string Cor, string Rotulo)[]
                 {
                     (p => p.Enviadas, "#0899B8", "enviadas"),
                     (p => p.Entregues, "#7A2FE0", "entregues"),
                 })
        {
            var caminho = new StringBuilder();
            for (var i = 0; i < pontos.Count; i++)
            {
                caminho.Append(i == 0 ? "M" : "L").Append(Num(X(i))).Append(' ').Append(Num(Y(serie(pontos[i]))));
            }
            sb.Append($"<path d=\"{caminho}\" fill=\"none\" stroke=\"{cor}\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");

            for (var i = 0; i < pontos.Count; i++)
            {
                var titulo = $"{pontos[i].Inicio.ToOffset(Brasilia).ToString("dd/MM", PtBr)}: {FormatarInt(serie(pontos[i]))} {rotulo}";
                sb.Append($"<circle cx=\"{Num(X(i))}\" cy=\"{Num(Y(serie(pontos[i])))}\" r=\"7\" fill=\"transparent\"><title>{titulo}</title></circle>");
                if (pontos.Count <= 20)
                {
                    sb.Append($"<circle cx=\"{Num(X(i))}\" cy=\"{Num(Y(serie(pontos[i])))}\" r=\"2\" fill=\"{cor}\"/>");
                }
            }

            var fimY = Y(serie(pontos[^1]));
            sb.Append($"<text x=\"{larguraPlot + 8}\" y=\"{Num(fimY + 3.5)}\" font-size=\"11\" font-weight=\"600\" fill=\"{cor}\">{rotulo}</text>");
        }

        for (var i = 0; i < pontos.Count; i += Math.Max(1, pontos.Count / 6))
        {
            sb.Append($"<text x=\"{Num(X(i))}\" y=\"{alturaTotal - 6}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#8A93AA\">{pontos[i].Inicio.ToOffset(Brasilia).ToString("dd/MM", PtBr)}</text>");
        }

        sb.Append($"<line x1=\"0\" y1=\"{topo + alturaPlot}\" x2=\"{larguraPlot}\" y2=\"{topo + alturaPlot}\" stroke=\"#D5DBE7\" stroke-width=\"1\"/>");
        sb.Append("</svg>");
        return new HtmlString(sb.ToString());
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
