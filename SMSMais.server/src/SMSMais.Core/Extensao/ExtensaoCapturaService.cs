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
            return new CapturaResumoDto(0, null, [], [], [], [], [], []);

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

        // Projeção para o construtor do record DENTRO do GroupBy não traduz no Npgsql: agrupa em
        // tipo anônimo no SQL e mapeia/ordena em memória.
        var porKindRaw = await q
            .GroupBy(x => x.Kind)
            .Select(g => new { g.Key, Total = g.LongCount() })
            .ToListAsync(ct);
        var porKind = porKindRaw
            .OrderByDescending(c => c.Total)
            .Select(c => new CapturaContagemDto(c.Key, c.Total))
            .ToList();

        var porEventoRaw = await q
            .Where(x => x.Evento != null)
            .GroupBy(x => x.Evento!)
            .Select(g => new { g.Key, Total = g.LongCount() })
            .ToListAsync(ct);
        var porEvento = porEventoRaw
            .OrderByDescending(c => c.Total)
            .Select(c => new CapturaContagemDto(c.Key, c.Total))
            .ToList();

        var porCaminhoRaw = await q
            .Where(x => x.Caminho != null)
            .GroupBy(x => x.Caminho!)
            .Select(g => new { g.Key, Total = g.LongCount() })
            .ToListAsync(ct);
        var porCaminho = porCaminhoRaw
            .OrderByDescending(c => c.Total)
            .Select(c => new CapturaContagemDto(c.Key, c.Total))
            .ToList();

        // Quebra por caminho+etapa: revela as ações de ESCRITA reais (ex.: o APLICAR do
        // autorizador, a gravação do marcar) para sabermos o que dá para deduzir. Só metadados.
        var porEtapaRaw = await q
            .Where(x => x.Etapa != null)
            .GroupBy(x => new { x.Caminho, x.Etapa })
            .Select(g => new { g.Key.Caminho, g.Key.Etapa, Total = g.LongCount() })
            .ToListAsync(ct);
        var porEtapa = porEtapaRaw
            .OrderByDescending(c => c.Total)
            .Select(c => new CapturaContagemDto($"{c.Caminho} · {c.Etapa}", c.Total))
            .ToList();

        var ultimasRaw = await q
            .OrderByDescending(x => x.CriadoEm)
            .Take(15)
            .Select(x => new
            {
                x.CriadoEm, x.OcorridoEm, x.OperadorSisreg,
                x.Kind, x.Metodo, x.Caminho, x.Etapa, x.Evento, x.Escrita, x.Status,
            })
            .ToListAsync(ct);
        var ultimas = ultimasRaw
            .Select(x => new CapturaRecenteDto(
                x.CriadoEm, x.OcorridoEm, x.OperadorSisreg,
                x.Kind, x.Metodo, x.Caminho, x.Etapa, x.Evento, x.Escrita, x.Status))
            .ToList();

        return new CapturaResumoDto(total, ultimoRecebido, instalacoes, porKind, porEvento, porCaminho, porEtapa, ultimas);
    }

    // Rótulos de página (texto fixo do SISREG, NÃO são PII) que provam quais campos a resposta traz.
    private static readonly string[] Rotulos =
    [
        "Chave de Confirma", "Solicita", "Paciente", "Cartão Nacional", "CNS", "CPF",
        "Nascimento", "Data", "Hora", "Profissional", "Procedimento",
        "Unidade Executante", "Unidade Solicitante", "CID", "Telefone", "Endereço", "Vaga",
    ];

    private static readonly string[] CaminhosEscrita =
        ["/cgi-bin/marcar", "/cgi-bin/cons_verificar", "/cgi-bin/cadweb50", "/cgi-bin/gerenciador_solicitacao"];

    public async Task<CapturaEstruturaDto> ObterEstruturaAsync(CancellationToken ct = default)
    {
        // Envio: nomes dos campos por (caminho, etapa) — nomes de campo NÃO são PII.
        var reqs = await _db.SisregCapturasNavegador.AsNoTracking()
            .Where(x => x.Kind == "requisicao" && x.Caminho != null && CaminhosEscrita.Contains(x.Caminho))
            .OrderByDescending(x => x.CriadoEm)
            .Select(x => new { x.Caminho, x.Etapa, x.PayloadJson })
            .Take(300)
            .ToListAsync(ct);

        var envios = reqs
            .GroupBy(r => new { r.Caminho, r.Etapa })
            .Select(g => new EstruturaEnvioDto(
                g.Key.Caminho!,
                g.Key.Etapa,
                g.Count(),
                g.SelectMany(r => LerCamposKeys(r.PayloadJson)).Distinct().OrderBy(k => k).ToList()))
            .OrderByDescending(e => e.Amostras)
            .ToList();

        // Resposta: quais rótulos (texto fixo) aparecem — prova o que a tela devolve, sem valores.
        var resps = await _db.SisregCapturasNavegador.AsNoTracking()
            .Where(x => x.Kind == "resposta" && x.Conteudo != null
                && (x.Caminho == "/cgi-bin/marcar" || x.Caminho == "/cgi-bin/cons_verificar"))
            .OrderByDescending(x => x.CriadoEm)
            .Select(x => new { x.Caminho, x.Conteudo })
            .Take(10)
            .ToListAsync(ct);

        var respostas = resps
            .GroupBy(r => r.Caminho!)
            .Select(g => new EstruturaRespostaDto(
                g.Key,
                g.Count(),
                Rotulos.Where(rot => g.Any(r => (r.Conteudo ?? "").Contains(rot, StringComparison.OrdinalIgnoreCase)))
                    .ToList()))
            .ToList();

        return new CapturaEstruturaDto(envios, respostas);
    }

    // Vizinhanças SEGURAS de ler (valores não são PII de paciente): número, chave, procedimento,
    // unidade, vaga. Deliberadamente NÃO inclui "Paciente"/"Solicitante" (nome).
    private static readonly string[] RotulosAmostra =
        ["Chave de Confirma", "Solicita", "Procedimento", "Unidade Executante", "Unidade Solicitante", "Vaga"];

    public async Task<CapturaAmostraRespostaDto> ObterAmostraRespostaMarcacaoAsync(CancellationToken ct = default)
    {
        // A resposta da marcação é a tela de confirmação: contém "Chave de Confirma".
        var resp = await _db.SisregCapturasNavegador.AsNoTracking()
            .Where(x => x.Kind == "resposta" && x.Caminho == "/cgi-bin/marcar" && x.Conteudo != null
                && x.Conteudo.Contains("Chave de Confirma"))
            .OrderByDescending(x => x.CriadoEm)
            .Select(x => new { x.CriadoEm, x.Conteudo, x.Caminho })
            .FirstOrDefaultAsync(ct);

        if (resp?.Conteudo is null)
            return new CapturaAmostraRespostaDto(null, null, []);

        var html = resp.Conteudo;
        var trechos = new List<AmostraTrechoDto>();
        foreach (var rotulo in RotulosAmostra)
        {
            var i = html.IndexOf(rotulo, StringComparison.OrdinalIgnoreCase);
            if (i < 0) continue;
            var ini = Math.Max(0, i - 20);
            var fim = Math.Min(html.Length, i + 320);
            var janela = Redigir(html[ini..fim]);
            trechos.Add(new AmostraTrechoDto(rotulo, janela));
        }
        return new CapturaAmostraRespostaDto(resp.Caminho, resp.CriadoEm, trechos);
    }

    // Mascara sequências de 11+ dígitos (CPF, CNS, telefone). co_solicitacao (≤10) fica visível.
    private static string Redigir(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, @"\d{11,}", "•redigido•");

    private static IEnumerable<string> LerCamposKeys(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("campos", out var campos) && campos.ValueKind == JsonValueKind.Object)
                return campos.EnumerateObject().Select(p => p.Name).ToList();
        }
        catch { /* payload malformado */ }
        return [];
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
