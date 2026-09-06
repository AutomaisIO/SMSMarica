using System.Collections.Concurrent;

using SMSMais.Core.Inteligencia.Provedores;

namespace SMSMais.Core.Regulacao.Catalogo;

/// <summary>
/// Guarda o embedding dos termos já buscados.
///
/// <para>Sem isto, cada tecla digitada na busca vira uma chamada paga ao provedor de embeddings
/// — e a tela de escolher procedimento é usada em toda abertura de solicitação, com termos que
/// se repetem o dia inteiro ("cardiologia", "ressonancia"). O cache é por termo já normalizado,
/// então "Cardiologia" e "CARDIOLOGIA " compartilham a entrada.</para>
///
/// <para>Singleton, com teto e despejo do mais antigo: é cache de conveniência, não índice.
/// Some no restart e a única consequência é a primeira busca de cada termo pagar de novo.</para>
/// </summary>
public sealed class CacheVetorConsulta
{
    private const int Capacidade = 500;

    private readonly ConcurrentDictionary<string, float[]> _vetores = new(StringComparer.Ordinal);

    /// <summary>Ordem de chegada, para saber quem despejar. Só é tocada sob <see cref="_trava"/>.</summary>
    private readonly Queue<string> _ordem = new();
    private readonly Lock _trava = new();

    public int Tamanho => _vetores.Count;

    public async Task<float[]> ObterOuEmbedarAsync(
        string termoNormalizado, IServicoEmbeddings embeddings, CancellationToken ct)
    {
        if (_vetores.TryGetValue(termoNormalizado, out var existente)) return existente;

        // De propósito fora da trava: embedar é ida à rede, e segurar a trava aqui serializaria
        // todas as buscas do sistema. Duas requisições simultâneas do mesmo termo novo podem
        // pagar duas vezes — é mais barato do que uma fila global.
        var vetor = await embeddings.EmbeddarAsync(termoNormalizado, ct);

        lock (_trava)
        {
            if (_vetores.TryAdd(termoNormalizado, vetor))
            {
                _ordem.Enqueue(termoNormalizado);
                while (_ordem.Count > Capacidade && _ordem.TryDequeue(out var antigo))
                {
                    _vetores.TryRemove(antigo, out _);
                }
            }
        }

        return vetor;
    }
}
