using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Armazenamento;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacs;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Exames;

/// <summary>
/// Gera, sob demanda, o PDF consolidado das imagens de um exame (estudo no PACS) e o
/// guarda no armazenamento de objetos (S3) na pasta do paciente. A chave é estável
/// (derivada do StudyInstanceUID), então a 2ª chamada em diante reaproveita o cache.
/// </summary>
public interface IExameImagensPdfService
{
    /// <summary>Gera (ou recupera do cache) o PDF consolidado das imagens do estudo da solicitação.</summary>
    Task<byte[]> GerarOuObterAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default);
}

public sealed class ExameImagensPdfService(
    SmsMaricaDbContext db,
    IArmazenamentoArquivos armazenamento,
    IPacsProxyService pacs,
    IPacientesService pacientes,
    ILogger<ExameImagensPdfService> logger) : IExameImagensPdfService
{
    /// <summary>Teto de imagens incluídas no PDF (estudos de imagem da SMS são pequenos; trava de segurança).</summary>
    private const int MaxImagens = 300;

    private const string DicomJson = "application/dicom+json";
    private const string Jpeg = "image/jpeg";

    private static readonly Lazy<byte[]?> LogoCache = new(CarregarLogo);

    public async Task<byte[]> GerarOuObterAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var sol = await db.SolicitacoesExame.AsNoTracking()
            .Include(s => s.TipoExame)
            .Include(s => s.Unidade)
            .FirstOrDefaultAsync(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), solicitacaoExameId);

        if (string.IsNullOrWhiteSpace(sol.StudyInstanceUID))
            throw new ConflitoException("exame.sem_imagens", "Este exame ainda não tem imagens disponíveis.");

        var chave = $"imagens-exame/{sol.PacienteId}/{sol.StudyInstanceUID}.pdf";

        var cache = await armazenamento.LerAsync(chave, cancellationToken);
        if (cache is { Length: > 0 })
            return cache;

        var instancias = await ListarInstanciasAsync(sol.StudyInstanceUID, cancellationToken);
        if (instancias.Count == 0)
            throw new ConflitoException("exame.sem_imagens", "Este exame ainda não tem imagens disponíveis no PACS.");

        if (instancias.Count > MaxImagens)
        {
            logger.LogWarning(
                "Estudo {Study} tem {Total} instâncias; truncando para {Max} no PDF consolidado.",
                sol.StudyInstanceUID, instancias.Count, MaxImagens);
            instancias = instancias.Take(MaxImagens).ToList();
        }

        var imagens = new List<byte[]>(instancias.Count);
        foreach (var inst in instancias)
        {
            var jpeg = await ObterImagemRenderizadaAsync(
                sol.StudyInstanceUID, inst.SeriesUid, inst.SopUid, cancellationToken);
            if (jpeg is { Length: > 0 })
                imagens.Add(jpeg);
        }

        if (imagens.Count == 0)
            throw new ConflitoException("exame.sem_imagens", "Não foi possível obter as imagens do exame no PACS.");

        var paciente = await pacientes.ObterPorIdAsync(sol.PacienteId, cancellationToken);

        var capa = new ExameImagensCapa(
            PacienteNome: paciente.NomeCompleto,
            PacienteCpf: paciente.Cpf,
            PacienteCns: paciente.Cns,
            PacienteNascimento: paciente.DataNascimento,
            ExameNome: sol.TipoExame?.Nome ?? "Exame de imagem",
            RealizadoEm: sol.RealizadoEm,
            Unidade: sol.Unidade?.Nome,
            Descricao: PrimeiroNaoVazio(sol.Justificativa, sol.Observacoes),
            Anamnese: Resumir(sol.Observacoes, sol.Justificativa));

        var pdf = new ExameImagensPdfDocument(capa, imagens, LogoCache.Value).Gerar();

        await armazenamento.SalvarAsync(chave, pdf, cancellationToken);
        return pdf;
    }

    private sealed record Instancia(string SeriesUid, string SopUid, int SeriesNumero, int InstanciaNumero);

    /// <summary>QIDO-RS: lista as instâncias do estudo (série + SOP), ordenadas por série/instância.</summary>
    private async Task<List<Instancia>> ListarInstanciasAsync(string studyUid, CancellationToken ct)
    {
        using var resposta = await pacs.EncaminharAsync(
            HttpMethod.Get,
            $"studies/{studyUid}/instances",
            "?includefield=00200011&includefield=00200013",
            DicomJson,
            ct);

        if (!resposta.IsSuccessStatusCode)
        {
            // 204 = sem instâncias ainda; demais = falha de consulta.
            if (resposta.StatusCode == System.Net.HttpStatusCode.NoContent) return [];
            logger.LogWarning("QIDO de instâncias falhou ({Status}) para o estudo {Study}.",
                (int)resposta.StatusCode, studyUid);
            return [];
        }

        await using var stream = await resposta.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var lista = new List<Instancia>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var series = ValorTag(item, "0020000E");
            var sop = ValorTag(item, "00080018");
            if (string.IsNullOrEmpty(series) || string.IsNullOrEmpty(sop)) continue;
            lista.Add(new Instancia(
                series, sop,
                InteiroTag(item, "00200011"),
                InteiroTag(item, "00200013")));
        }

        return [.. lista.OrderBy(i => i.SeriesNumero).ThenBy(i => i.InstanciaNumero)];
    }

    /// <summary>WADO-RS /rendered: imagem JPEG já rasterizada pelo dcm4chee (sem decodificar pixel data no .NET).</summary>
    private async Task<byte[]?> ObterImagemRenderizadaAsync(
        string studyUid, string seriesUid, string sopUid, CancellationToken ct)
    {
        using var resposta = await pacs.EncaminharAsync(
            HttpMethod.Get,
            $"studies/{studyUid}/series/{seriesUid}/instances/{sopUid}/rendered",
            string.Empty,
            Jpeg,
            ct);

        if (!resposta.IsSuccessStatusCode)
        {
            logger.LogWarning("WADO /rendered falhou ({Status}) para a instância {Sop}.",
                (int)resposta.StatusCode, sopUid);
            return null;
        }

        return await resposta.Content.ReadAsByteArrayAsync(ct);
    }

    // ---- Helpers de DICOM JSON ----

    private static string? ValorTag(JsonElement item, string tag) =>
        item.TryGetProperty(tag, out var campo)
        && campo.TryGetProperty("Value", out var valor)
        && valor.ValueKind == JsonValueKind.Array
        && valor.GetArrayLength() > 0
            ? valor[0].GetString()
            : null;

    private static int InteiroTag(JsonElement item, string tag) =>
        int.TryParse(ValorTag(item, tag), out var n) ? n : 0;

    // ---- Helpers de texto da capa ----

    private static string? PrimeiroNaoVazio(params string?[] valores) =>
        valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Resumo curto para a capa (sem IA nesta fase): texto truncado em ~600 caracteres.</summary>
    private static string? Resumir(params string?[] valores)
    {
        var texto = PrimeiroNaoVazio(valores);
        if (string.IsNullOrWhiteSpace(texto)) return null;
        texto = texto.Trim();
        return texto.Length <= 600 ? texto : texto[..600].TrimEnd() + "…";
    }

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
