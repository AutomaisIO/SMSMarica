using System.Text.Json;
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
