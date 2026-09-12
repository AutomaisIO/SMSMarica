using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using SMSMais.Core.Inteligencia.Provedores;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Inteligencia.Conhecimento;

/// <summary>
/// Recuperador de contexto para o provedor de IA. Dois modos, conforme <c>Ia:Embeddings:Habilitado</c>:
/// <list type="bullet">
/// <item><b>Desligado (padrão — Opção B)</b>: manda o conhecimento <b>inteiro</b> da fonte (lê todos
/// os .md de <c>Bases/&lt;fonte&gt;/</c>). Simples e sem depender de provedor de embeddings.</item>
/// <item><b>Ligado</b>: busca por similaridade vetorial (RAG) os top-k chunks via pgvector
/// (<c>L2Distance</c>). Vale a pena quando a base ficar grande demais para enviar inteira.</item>
/// </list>
/// Em ambos, soma os aprendizados ativos da fonte.
/// </summary>
public sealed class RecuperadorContexto(
    SmsMaisDbContext db, IServicoEmbeddings embeddings, IConfiguration configuration)
    : IRecuperadorContexto
{
    private const int TopKChunks = 8;
    private const int MaxAprendizados = 20;
    private const string PastaRelativa = "Inteligencia/Conhecimento/Bases";

    private readonly bool _embeddingsHabilitado = configuration.GetValue("Ia:Embeddings:Habilitado", false);

    /// <summary>
    /// Bases cujo conhecimento é CURADO à mão e pequeno: vai inteiro em toda pergunta, com ou sem
    /// RAG. O RAG só indexa o que foi extraído pela tela; os .md do repositório nunca entram no
    /// índice vetorial — e é neles que estão as regras que não podem faltar (interno × externo,
    /// "pergunte quando houver dúvida", como juntar SISREG com SER).
    /// </summary>
    private static readonly HashSet<TipoFonte> ConhecimentoSempreInteiro = [TipoFonte.Regulacao, TipoFonte.Atendimento];

    public async Task<ContextoRecuperado> RecuperarAsync(
        Guid fonteId, string pergunta, CancellationToken cancellationToken = default)
    {
        var tipo = await db.IaFontes
            .Where(f => f.Id == fonteId)
            .Select(f => f.Tipo)
            .FirstOrDefaultAsync(cancellationToken);

        var conhecimento = _embeddingsHabilitado && !ConhecimentoSempreInteiro.Contains(tipo)
            ? await RecuperarPorSimilaridadeAsync(fonteId, pergunta, cancellationToken)
            : await CarregarConhecimentoCompletoAsync(fonteId, cancellationToken);

        var aprendizados = await db.IaAprendizados
            .Where(a => a.FonteId == fonteId && a.Ativo && a.ExcluidoEm == null)
            .OrderByDescending(a => a.CriadoEm)
            .Take(MaxAprendizados)
            .Select(a => a.Conteudo)
            .ToListAsync(cancellationToken);

        return new ContextoRecuperado(conhecimento, Juntar(aprendizados));
    }

    /// <summary>Opção B: lê todo o conhecimento (.md) da fonte direto do disco — sem embeddings.</summary>
    private async Task<string> CarregarConhecimentoCompletoAsync(Guid fonteId, CancellationToken cancellationToken)
    {
        var tipo = await db.IaFontes
            .Where(f => f.Id == fonteId)
            .Select(f => f.Tipo)
            .FirstOrDefaultAsync(cancellationToken);

        var raiz = Path.Combine(AppContext.BaseDirectory, PastaRelativa.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(raiz))
        {
            return string.Empty;
        }

        // Casa a subpasta com o Tipo de forma case-insensitive (FS do Linux é case-sensitive).
        var pasta = Directory.EnumerateDirectories(raiz)
            .FirstOrDefault(d => string.Equals(Path.GetFileName(d), tipo.ToString(), StringComparison.OrdinalIgnoreCase));
        if (pasta is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.md", SearchOption.AllDirectories).OrderBy(a => a))
        {
            sb.AppendLine(await File.ReadAllTextAsync(arquivo, cancellationToken)).AppendLine();
        }

        // Também os documentos geridos pela tela / extraídos do schema (guardados no banco), para
        // que valham no modo sem embeddings. Só os geridos (modelo/ e manual/) — os do repo já vêm
        // do disco acima, não duplicar. ATENÇÃO: sem RAG isto concatena tudo; bases com modelo
        // extraído grande devem rodar com embeddings ligados. Ver ADR-0023.
        var doBanco = await db.IaDocumentosConhecimento
            .Where(d => d.FonteId == fonteId
                        && (d.Caminho.StartsWith("modelo/") || d.Caminho.StartsWith("manual/")))
            .OrderBy(d => d.Caminho)
            .Select(d => d.Conteudo)
            .ToListAsync(cancellationToken);
        foreach (var conteudo in doBanco)
        {
            sb.AppendLine(conteudo).AppendLine();
        }

        return sb.ToString().Trim();
    }

    /// <summary>Modo RAG: embeda a pergunta e busca os top-k chunks mais próximos (pgvector).</summary>
    private async Task<string> RecuperarPorSimilaridadeAsync(
        Guid fonteId, string pergunta, CancellationToken cancellationToken)
    {
        var vetorPergunta = new Vector(await embeddings.EmbeddarAsync(pergunta, cancellationToken));

        // Bases de MESMA família (ex.: UPA e Santa Rita = klinikos) compartilham conhecimento: o
        // que foi extraído/enriquecido em uma vale para a outra — inclusive uma base recém-cadastrada
        // aproveita a extração da irmã sem re-extrair. Ver ADR-0023.
        var familia = await db.IaFontes
            .Where(f => f.Id == fonteId)
            .Select(f => f.Familia)
            .FirstOrDefaultAsync(cancellationToken);

        var query = db.IaChunksConhecimento.Where(c => c.Embedding != null);
        query = string.IsNullOrEmpty(familia)
            ? query.Where(c => c.FonteId == fonteId)
            : query.Where(c => c.FonteId == fonteId
                || db.IaFontes.Any(f => f.Id == c.FonteId && f.Familia == familia && f.ExcluidoEm == null));

        var chunks = await query
            .OrderBy(c => c.Embedding!.L2Distance(vetorPergunta))
            .Take(TopKChunks)
            .Select(c => c.Conteudo)
            .ToListAsync(cancellationToken);

        return Juntar(chunks);
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
