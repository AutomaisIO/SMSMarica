using System.Text;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using SMSMarica.Core.Inteligencia.Provedores;
using SMSMarica.Data;

namespace SMSMarica.Core.Inteligencia.Conhecimento;

/// <summary>
/// Recuperador de contexto (RAG): embeda a pergunta e busca os top-k chunks de conhecimento
/// mais próximos por distância L2 (pgvector via <c>L2Distance</c>), além dos aprendizados
/// ativos da fonte. Devolve strings prontas para o turno do usuário do provedor de IA.
/// </summary>
public sealed class RecuperadorContexto(SmsMaricaDbContext db, IServicoEmbeddings embeddings)
    : IRecuperadorContexto
{
    private const int TopKChunks = 8;
    private const int MaxAprendizados = 20;

    public async Task<ContextoRecuperado> RecuperarAsync(
        Guid fonteId, string pergunta, CancellationToken cancellationToken = default)
    {
        var vetorPergunta = new Vector(await embeddings.EmbeddarAsync(pergunta, cancellationToken));

        var chunks = await db.IaChunksConhecimento
            .Where(c => c.FonteId == fonteId && c.Embedding != null)
            .OrderBy(c => c.Embedding!.L2Distance(vetorPergunta))
            .Take(TopKChunks)
            .Select(c => c.Conteudo)
            .ToListAsync(cancellationToken);

        var aprendizados = await db.IaAprendizados
            .Where(a => a.FonteId == fonteId && a.Ativo && a.ExcluidoEm == null)
            .OrderByDescending(a => a.CriadoEm)
            .Take(MaxAprendizados)
            .Select(a => a.Conteudo)
            .ToListAsync(cancellationToken);

        return new ContextoRecuperado(
            ConhecimentoRecuperado: Juntar(chunks),
            AprendizadosAtivos: Juntar(aprendizados));
    }

    private static string Juntar(IReadOnlyList<string> trechos)
    {
        if (trechos.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var t in trechos)
        {
            sb.AppendLine("- ").AppendLine(t).AppendLine();
        }

        return sb.ToString().Trim();
    }
}
