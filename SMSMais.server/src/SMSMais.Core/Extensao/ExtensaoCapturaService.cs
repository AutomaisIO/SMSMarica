using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Extensao.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Extensao;

/// <summary>
/// Recebe os lotes de captura da extensão do SISREG e os grava como estão (fase de análise).
/// Não interpreta o negócio nem cria solicitação — isso vem depois, quando os endpoints
/// definitivos forem desenhados a partir deste acervo.
/// </summary>
public sealed class ExtensaoCapturaService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual)
    : IExtensaoCapturaService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    // Guardados fora do jsonb (podem ser grandes); não duplicar no payload.
    private static readonly string[] CamposDeConteudo = ["html", "corpo"];

    public async Task<CapturaLoteResultado> ReceberAsync(CapturaLoteRequest lote, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var usuarioId = _usuarioAtual.UsuarioId;

        var linhas = new List<SisregCapturaNavegador>(lote.Itens.Count);
        foreach (var item in lote.Itens)
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            linhas.Add(new SisregCapturaNavegador
            {
                Id = Guid.CreateVersion7(),
                CriadoEm = agora,
                OcorridoEm = LerData(item, "quando"),
                UsuarioId = usuarioId,
                InstallId = Truncar(lote.InstallId, 64) ?? string.Empty,
                Versao = Truncar(lote.Versao, 20),
                OperadorSisreg = Truncar(LerTexto(item, "operador"), 200),
                Kind = Truncar(LerTexto(item, "kind") ?? "desconhecido", 20)!,
                Metodo = Truncar(LerTexto(item, "metodo"), 10),
                Caminho = Truncar(LerTexto(item, "caminho"), 300),
                Etapa = Truncar(LerTexto(item, "etapa"), 80),
                Evento = Truncar(LerTexto(item, "evento"), 60),
                Escrita = LerBool(item, "escrita"),
                Status = Truncar(LerTexto(item, "status"), 40),
                Conteudo = LerConteudo(item),
                PayloadJson = SerializarSemConteudo(item),
            });
        }

        if (linhas.Count == 0) return new CapturaLoteResultado(0);

        _db.SisregCapturasNavegador.AddRange(linhas);
        await _db.SaveChangesAsync(ct);
        return new CapturaLoteResultado(linhas.Count);
    }

    public async Task<CapturaResumoDto> ObterResumoAsync(CancellationToken ct = default)
    {
        var q = _db.SisregCapturasNavegador.AsNoTracking();

        var total = await q.LongCountAsync(ct);
        if (total == 0)
            return new CapturaResumoDto(0, null, [], [], [], []);

        var ultimoRecebido = await q.MaxAsync(x => (DateTime?)x.CriadoEm, ct);

        // Agregações simples (traduzíveis). UltimaVersao via Max da string — aproximação boa o
        // bastante para um monitor; subquery ordenado dentro do GroupBy não traduz no Npgsql.
        var instalacoesRaw = await q
            .GroupBy(x => x.InstallId)
            .Select(g => new
            {
                InstallId = g.Key,
                Total = g.LongCount(),
                UltimoEm = g.Max(x => x.CriadoEm),
                UltimaVersao = g.Max(x => x.Versao),
            })
            .ToListAsync(ct);

        var instalacoes = instalacoesRaw
            .OrderByDescending(i => i.UltimoEm)
            .Select(i => new CapturaInstalacaoDto(i.InstallId, i.UltimaVersao, i.Total, i.UltimoEm))
            .ToList();

        var porKind = await q
            .GroupBy(x => x.Kind)
            .Select(g => new CapturaContagemDto(g.Key, g.LongCount()))
            .OrderByDescending(c => c.Total)
            .ToListAsync(ct);

        var porEvento = await q
            .Where(x => x.Evento != null)
            .GroupBy(x => x.Evento!)
            .Select(g => new CapturaContagemDto(g.Key, g.LongCount()))
            .OrderByDescending(c => c.Total)
            .ToListAsync(ct);

        var ultimas = await q
            .OrderByDescending(x => x.CriadoEm)
            .Take(15)
            .Select(x => new CapturaRecenteDto(
                x.CriadoEm, x.OcorridoEm, x.OperadorSisreg,
                x.Kind, x.Metodo, x.Caminho, x.Etapa, x.Evento, x.Escrita, x.Status))
            .ToListAsync(ct);

        return new CapturaResumoDto(total, ultimoRecebido, instalacoes, porKind, porEvento, ultimas);
    }

    private static string? LerTexto(JsonElement o, string prop) =>
        o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool LerBool(JsonElement o, string prop) =>
        o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

    private static DateTime? LerData(JsonElement o, string prop) =>
        o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            && DateTime.TryParse(v.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var d)
            ? d
            : null;

    private static string? LerConteudo(JsonElement o)
    {
        foreach (var campo in CamposDeConteudo)
        {
            if (o.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        return null;
    }

    /// <summary>Reserializa o item sem os campos grandes (html/corpo), que vão para a coluna text.</summary>
    private static string SerializarSemConteudo(JsonElement o)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var p in o.EnumerateObject())
        {
            if (Array.IndexOf(CamposDeConteudo, p.Name) >= 0) continue;
            dict[p.Name] = p.Value;
        }
        return JsonSerializer.Serialize(dict);
    }

    private static string? Truncar(string? s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
