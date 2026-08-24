using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;

namespace SMSMais.Core.Inteligencia.Provedores;

/// <summary>
/// Serviço de embeddings via Voyage AI (<c>POST /v1/embeddings</c>). Token e modelo vêm da
/// configuração global (<c>IaConfiguracao</c>, cifrados em repouso). Dimensão = 1024 (voyage-3),
/// casando com a coluna <c>vector(1024)</c>.
/// </summary>
public sealed class VoyageEmbeddings(
    HttpClient http,
    SmsMaisDbContext db,
    IProtetorSegredos protetor) : IServicoEmbeddings
{
    public const string HttpClientName = "IaEmbeddings";
    private const string DefaultEndpoint = "https://api.voyageai.com/v1/embeddings";
    private const string ModeloPadrao = "voyage-3";

    public async Task<float[]> EmbeddarAsync(string texto, CancellationToken cancellationToken = default)
    {
        var lote = await EmbeddarLoteAsync([texto], cancellationToken);
        return lote[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbeddarLoteAsync(
        IReadOnlyList<string> textos, CancellationToken cancellationToken = default)
    {
        if (textos.Count == 0)
        {
            return [];
        }

        var (token, modelo) = await ObterCredenciaisAsync(cancellationToken);

        var requisicao = new HttpRequestMessage(HttpMethod.Post, DefaultEndpoint)
        {
            Content = JsonContent.Create(new VoyageRequest(textos, modelo)),
        };
        requisicao.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var resposta = await http.SendAsync(requisicao, cancellationToken);
        var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            throw new ConflitoException(
                "ia.embeddings",
                $"Provedor de embeddings retornou {(int)resposta.StatusCode}: {corpo}");
        }

        var conteudo = System.Text.Json.JsonSerializer.Deserialize<VoyageResponse>(corpo)
            ?? throw new ConflitoException("ia.embeddings", "Resposta de embeddings vazia/ilegível.");

        return [.. conteudo.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)];
    }

    private async Task<(string Token, string Modelo)> ObterCredenciaisAsync(CancellationToken cancellationToken)
    {
        var config = await db.IaConfiguracoes.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new ValidacaoException("ia.config", "Configuração do módulo IA não definida.");

        if (string.IsNullOrWhiteSpace(config.TokenEmbeddingsCifrado))
        {
            throw new ValidacaoException("ia.config.tokenEmbeddings", "Token de embeddings não configurado.");
        }

        var token = protetor.Revelar(config.TokenEmbeddingsCifrado);
        var modelo = string.IsNullOrWhiteSpace(config.ModeloEmbeddings) ? ModeloPadrao : config.ModeloEmbeddings;
        return (token, modelo);
    }

    private sealed record VoyageRequest(
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input,
        [property: JsonPropertyName("model")] string Model);

    private sealed record VoyageResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<VoyageEmbeddingItem> Data);

    private sealed record VoyageEmbeddingItem(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
