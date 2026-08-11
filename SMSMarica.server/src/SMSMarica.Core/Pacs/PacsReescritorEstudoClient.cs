using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FellowOakDicom;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Core.Pacs;

/// <summary>
/// Reescrita de identidade no dcm4chee. Ver <see cref="IPacsReescritorEstudoClient"/> para o
/// porquê de não bastar coagir os atributos.
///
/// <para>A ordem é deliberada: o estudo novo só é confirmado no PACS ANTES de o original ser
/// tocado. Se qualquer passo falhar antes disso, nada foi destruído — o estudo original continua
/// lá, íntegro, e a operação pode ser repetida.</para>
/// </summary>
public sealed class PacsReescritorEstudoClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<PacsReescritorEstudoClient> logger) : IPacsReescritorEstudoClient
{
    /// <summary>IOCM: "Incorrect Modality Worklist Entry" (DICOM CID 7010). Confirmado aceito pela
    /// instalação no spike de 2026-08-11; a instância tem o AE <c>IOCM_WRONG_MWL</c> provisionado.</summary>
    private const string CodigoRejeicaoWorklistErrada = "113038%5EDCM";

    private readonly string? _wadoUriBase = DerivarWadoUri(configuration["Pacs:Dcm4chee:RsBaseUrl"]);

    public async Task<EstudoReescrito> ReescreverIdentidadeAsync(
        string studyInstanceUID, IdentidadeDicom identidade, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID))
            throw new ValidacaoException("studyInstanceUID", "StudyInstanceUID é obrigatório.");
        if (string.IsNullOrWhiteSpace(identidade.PatientId))
            throw new ValidacaoException("identidade", "Identidade sem PatientID — a reescrita ficaria pior que o erro.");

        var instancias = await ListarInstanciasAsync(studyInstanceUID, cancellationToken);
        if (instancias.Count == 0)
            throw new ConflitoException("pacs.estudo_sem_instancias",
                "O estudo não tem instâncias no PACS — nada a reescrever.");

        // UIDs novos e ESTÁVEIS dentro da operação: séries preservam o agrupamento original
        // (4 séries continuam 4 séries), mas com identificadores próprios.
        var studyNovo = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
        var seriesNovas = new Dictionary<string, string>(StringComparer.Ordinal);

        // UMA instância por vez — baixa, reescreve, armazena e solta. A mamografia do CDT é
        // gravada SEM compressão: 56 MB por instância, ~224 MB o estudo. Acumular tudo em
        // memória antes do STOW (e o multipart duplicando) passaria de 400 MB no droplet.
        foreach (var (series, sop) in instancias)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arquivo = await BaixarInstanciaAsync(studyInstanceUID, series, sop, cancellationToken);
            if (!seriesNovas.TryGetValue(series, out var seriesNova))
            {
                seriesNova = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
                seriesNovas[series] = seriesNova;
            }
            await ArmazenarAsync(Reescrever(arquivo, identidade, studyNovo, seriesNova), cancellationToken);
        }

        // Só confirma que o novo existe ANTES de destruir o antigo. Sem esta prova, uma falha
        // silenciosa do STOW apagaria as imagens do paciente.
        var confirmadas = (await ListarInstanciasAsync(studyNovo, cancellationToken)).Count;
        if (confirmadas < instancias.Count)
            throw new ConflitoException("pacs.reescrita_nao_confirmada",
                $"O PACS confirmou {confirmadas} de {instancias.Count} instâncias reescritas. " +
                "O estudo original foi preservado.");

        await DescartarAsync(studyInstanceUID, cancellationToken);

        logger.LogInformation(
            "Estudo {Antigo} reescrito para {Novo} ({N} instâncias) — identidade {PatientId}/{Accession}.",
            studyInstanceUID, studyNovo, instancias.Count, identidade.PatientId, identidade.AccessionNumber);

        return new EstudoReescrito(studyNovo, instancias.Count);
    }

    public async Task DescartarAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        // A rejeição IOCM é a trilha de auditoria do próprio PACS: registra POR QUE o estudo saiu.
        // 409 = já rejeitado antes; conta como sucesso (idempotente).
        var rejeicao = await http.PostAsync(
            $"studies/{Uri.EscapeDataString(studyInstanceUID)}/reject/{CodigoRejeicaoWorklistErrada}",
            null, cancellationToken);
        if (!rejeicao.IsSuccessStatusCode && rejeicao.StatusCode != HttpStatusCode.Conflict)
            logger.LogWarning("Rejeição IOCM do estudo {Uid} retornou {Status}.",
                studyInstanceUID, (int)rejeicao.StatusCode);

        var exclusao = await http.DeleteAsync(
            $"studies/{Uri.EscapeDataString(studyInstanceUID)}", cancellationToken);
        if (!exclusao.IsSuccessStatusCode && exclusao.StatusCode != HttpStatusCode.NotFound)
            throw new ConflitoException("pacs.exclusao_falhou",
                $"O PACS recusou apagar o estudo {studyInstanceUID} ({(int)exclusao.StatusCode}).");
    }

    // ---- passos ----

    /// <summary>Pares (SeriesInstanceUID, SOPInstanceUID) de todas as instâncias do estudo.</summary>
    private async Task<List<(string Series, string Sop)>> ListarInstanciasAsync(
        string studyUid, CancellationToken ct)
    {
        var resposta = await http.GetAsync(
            $"studies/{Uri.EscapeDataString(studyUid)}/instances?includefield=0020000E&includefield=00080018", ct);
        if (resposta.StatusCode == HttpStatusCode.NotFound) return [];
        if (!resposta.IsSuccessStatusCode)
            throw new ConflitoException("pacs.consulta_falhou",
                $"O PACS retornou {(int)resposta.StatusCode} ao listar as instâncias do estudo.");

        var corpo = await resposta.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(corpo)) return [];

        var lista = new List<(string, string)>();
        using var doc = JsonDocument.Parse(corpo);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var series = PrimeiroValor(item, "0020000E");
            var sop = PrimeiroValor(item, "00080018");
            if (series is not null && sop is not null) lista.Add((series, sop));
        }
        return lista;
    }

    /// <summary>WADO-URI com <c>contentType=application/dicom</c> devolve o arquivo cru (sem
    /// multipart), que é o formato que o fo-dicom abre direto — mesmo caminho do transcode.</summary>
    private async Task<byte[]> BaixarInstanciaAsync(string study, string series, string sop, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_wadoUriBase))
            throw new ConflitoException("pacs.wado_nao_configurado",
                "WADO-URI não pôde ser derivado de Pacs:Dcm4chee:RsBaseUrl.");

        var url = $"{_wadoUriBase}?requestType=WADO&studyUID={Uri.EscapeDataString(study)}" +
                  $"&seriesUID={Uri.EscapeDataString(series)}&objectUID={Uri.EscapeDataString(sop)}" +
                  "&contentType=application/dicom";
        using var resposta = await http.GetAsync(url, ct);
        if (!resposta.IsSuccessStatusCode)
            throw new ConflitoException("pacs.download_falhou",
                $"O PACS retornou {(int)resposta.StatusCode} ao baixar a instância {sop}.");
        return await resposta.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>Troca a identidade e os UIDs. NÃO toca nos pixels nem no transfer-syntax.</summary>
    private static byte[] Reescrever(byte[] original, IdentidadeDicom id, string studyNovo, string seriesNova)
    {
        using var entrada = new MemoryStream(original);
        var arquivo = DicomFile.Open(entrada);
        var ds = arquivo.Dataset;

        var sopNovo = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
        ds.AddOrUpdate(DicomTag.StudyInstanceUID, studyNovo);
        ds.AddOrUpdate(DicomTag.SeriesInstanceUID, seriesNova);
        ds.AddOrUpdate(DicomTag.SOPInstanceUID, sopNovo);
        arquivo.FileMetaInfo.MediaStorageSOPInstanceUID = DicomUID.Parse(sopNovo);

        ds.AddOrUpdate(DicomTag.PatientID, id.PatientId);
        ds.AddOrUpdate(DicomTag.PatientName, id.PatientName);
        ds.AddOrUpdate(DicomTag.AccessionNumber, id.AccessionNumber ?? string.Empty);
        if (id.DataNascimento is { } nasc)
            ds.AddOrUpdate(DicomTag.PatientBirthDate, nasc.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(id.Sexo))
            ds.AddOrUpdate(DicomTag.PatientSex, id.Sexo);

        using var saida = new MemoryStream();
        arquivo.Save(saida);
        return saida.ToArray();
    }

    /// <summary>STOW-RS de UMA instância (multipart/related com uma parte). Uma por requisição
    /// para não segurar o estudo inteiro em memória — ver o laço do chamador.</summary>
    private async Task ArmazenarAsync(byte[] instancia, CancellationToken ct)
    {
        using var conteudo = new MultipartContent("related", $"bd{Guid.NewGuid():N}");
        conteudo.Headers.ContentType!.Parameters.Add(
            new NameValueHeaderValue("type", "\"application/dicom\""));
        var parte = new ByteArrayContent(instancia);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");
        conteudo.Add(parte);

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "studies") { Content = conteudo };
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dicom+json"));
        using var resposta = await http.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
            throw new ConflitoException("pacs.stow_falhou",
                $"O PACS recusou armazenar a instância reescrita ({(int)resposta.StatusCode}). " +
                "O estudo original foi preservado.");
    }

    // ---- utilitários ----

    private static string? PrimeiroValor(JsonElement item, string tag) =>
        item.TryGetProperty(tag, out var t)
        && t.TryGetProperty("Value", out var v)
        && v.ValueKind == JsonValueKind.Array
        && v.GetArrayLength() > 0
            ? v[0].GetString()
            : null;

    /// <summary>".../aets/{ae}/rs" → ".../aets/{ae}/wado" (mesma derivação do transcode).</summary>
    private static string? DerivarWadoUri(string? rsBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(rsBaseUrl)) return null;
        var semBarra = rsBaseUrl.TrimEnd('/');
        return semBarra.EndsWith("/rs", StringComparison.OrdinalIgnoreCase)
            ? semBarra[..^3] + "/wado"
            : null;
    }
}
