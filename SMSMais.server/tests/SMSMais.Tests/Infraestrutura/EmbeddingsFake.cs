using System.Text;

using SMSMais.Core.Inteligencia.Provedores;

namespace SMSMais.Tests.Infraestrutura;

/// <summary>
/// Provedor de embeddings determinístico para teste — sem rede, sem custo.
///
/// <para>Projeta trigramas do texto em 1024 posições e normaliza em L2. As duas propriedades que
/// os testes dependem saem disso: texto igual dá vetor igual, e texto parecido dá cosseno alto,
/// porque trigramas compartilhados caem nas mesmas posições.</para>
///
/// <para>Conta as chamadas: é assim que se prova que o cache de consulta e o hash de embedding
/// evitam ida ao provedor.</para>
/// </summary>
public sealed class EmbeddingsFake : IServicoEmbeddings
{
    public const int Dimensao = 1024;

    private readonly Lock _trava = new();

    public int ChamadasUnitarias { get; private set; }
    public int ChamadasEmLote { get; private set; }
    public int TextosEmbedados { get; private set; }

    /// <summary>Quando definido, toda chamada estoura — simula o provedor fora do ar.</summary>
    public Exception? FalharCom { get; set; }

    public Task<float[]> EmbeddarAsync(string texto, CancellationToken cancellationToken = default)
    {
        lock (_trava)
        {
            ChamadasUnitarias++;
            TextosEmbedados++;
        }
        if (FalharCom is not null) throw FalharCom;
        return Task.FromResult(Vetorizar(texto));
    }

    public Task<IReadOnlyList<float[]>> EmbeddarLoteAsync(
        IReadOnlyList<string> textos, CancellationToken cancellationToken = default)
    {
        lock (_trava)
        {
            ChamadasEmLote++;
            TextosEmbedados += textos.Count;
        }
        if (FalharCom is not null) throw FalharCom;
        return Task.FromResult<IReadOnlyList<float[]>>([.. textos.Select(Vetorizar)]);
    }

    public static float[] Vetorizar(string texto)
    {
        var v = new float[Dimensao];
        var t = $"  {(texto ?? string.Empty).ToUpperInvariant()}  ";

        for (var i = 0; i + 3 <= t.Length; i++)
        {
            var trigrama = t.AsSpan(i, 3);
            var pos = (int)(Fnv1a(trigrama) % Dimensao);
            v[pos] += 1f;
        }

        var norma = MathF.Sqrt(v.Sum(x => x * x));
        if (norma > 0)
        {
            for (var i = 0; i < v.Length; i++) v[i] /= norma;
        }
        else
        {
            // Texto vazio: vetor nulo não tem cosseno definido e o pgvector reclama.
            v[0] = 1f;
        }
        return v;
    }

    private static uint Fnv1a(ReadOnlySpan<char> s)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(s.ToArray()))
        {
            hash = (hash ^ b) * 16777619u;
        }
        return hash;
    }
}
