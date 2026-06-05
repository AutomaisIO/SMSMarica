using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using SMSMarica.Core.Inteligencia.Provedores;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Core.Inteligencia.Conhecimento;

/// <summary>
/// Sincroniza os arquivos .md de conhecimento (versionados no repo e copiados para a saída em
/// <c>Inteligencia/Conhecimento/Bases/&lt;fonte&gt;/</c>) com as tabelas <c>ia_documento_conhecimento</c>
/// e <c>ia_chunk_conhecimento</c>. Idempotente: usa hash do conteúdo para re-embeddar só o que mudou.
/// O nome da subpasta &lt;fonte&gt; é casado (case-insensitive) com <see cref="IaFonte.Tipo"/>.
/// </summary>
public sealed class ConhecimentoService(SmsMaricaDbContext db, IServicoEmbeddings embeddings)
    : IConhecimentoService
{
    private const string PastaRelativa = "Inteligencia/Conhecimento/Bases";

    public async Task SincronizarAsync(CancellationToken cancellationToken = default)
    {
        var raiz = Path.Combine(AppContext.BaseDirectory, PastaRelativa.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(raiz))
        {
            return; // Nenhuma base de conhecimento empacotada — nada a sincronizar.
        }

        var fontes = await db.IaFontes
            .Where(f => f.ExcluidoEm == null)
            .ToListAsync(cancellationToken);

        foreach (var diretorioFonte in Directory.EnumerateDirectories(raiz))
        {
            var nomePasta = Path.GetFileName(diretorioFonte);
            var fonte = fontes.FirstOrDefault(f =>
                string.Equals(f.Tipo.ToString(), nomePasta, StringComparison.OrdinalIgnoreCase));

            if (fonte is null)
            {
                continue; // Pasta de conhecimento sem fonte cadastrada correspondente.
            }

            foreach (var arquivo in Directory.EnumerateFiles(diretorioFonte, "*.md", SearchOption.AllDirectories))
            {
                await SincronizarArquivoAsync(fonte.Id, raiz, arquivo, cancellationToken);
            }
        }
    }

    private async Task SincronizarArquivoAsync(
        Guid fonteId, string raiz, string caminhoAbsoluto, CancellationToken cancellationToken)
    {
        var caminhoRelativo = Path.GetRelativePath(raiz, caminhoAbsoluto).Replace('\\', '/');
        var conteudo = await File.ReadAllTextAsync(caminhoAbsoluto, cancellationToken);
        var hash = CalcularHash(conteudo);

        var documento = await db.IaDocumentosConhecimento
            .FirstOrDefaultAsync(d => d.FonteId == fonteId && d.Caminho == caminhoRelativo, cancellationToken);

        if (documento is not null && documento.Hash == hash)
        {
            return; // Inalterado desde a última sincronização.
        }

        var agora = DateTime.UtcNow;

        if (documento is null)
        {
            documento = new IaDocumentoConhecimento
            {
                Id = Guid.NewGuid(),
                FonteId = fonteId,
                Caminho = caminhoRelativo,
                Conteudo = conteudo,
                Hash = hash,
                Versao = 1,
                CriadoEm = agora,
            };
            db.IaDocumentosConhecimento.Add(documento);
        }
        else
        {
            documento.Conteudo = conteudo;
            documento.Hash = hash;
            documento.Versao += 1;
            documento.AtualizadoEm = agora;

            // Remove chunks antigos: serão regerados a partir do novo conteúdo.
            var antigos = await db.IaChunksConhecimento
                .Where(c => c.DocumentoId == documento.Id)
                .ToListAsync(cancellationToken);
            db.IaChunksConhecimento.RemoveRange(antigos);
        }

        var pedacos = Chunkificar(conteudo);
        if (pedacos.Count > 0)
        {
            var vetores = await embeddings.EmbeddarLoteAsync(pedacos, cancellationToken);
            for (var i = 0; i < pedacos.Count; i++)
            {
                db.IaChunksConhecimento.Add(new IaChunkConhecimento
                {
                    Id = Guid.NewGuid(),
                    DocumentoId = documento.Id,
                    FonteId = fonteId,
                    Ordem = i,
                    Conteudo = pedacos[i],
                    Embedding = new Vector(vetores[i]),
                    CriadoEm = agora,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Chunking simples: quebra por seções de markdown (linhas iniciadas por '#') e, dentro
    /// de cada seção, agrupa parágrafos até um limite de caracteres.
    /// </summary>
    private static List<string> Chunkificar(string conteudo)
    {
        const int maxChars = 1500;
        var resultado = new List<string>();

        var secoes = QuebrarPorSecao(conteudo);
        foreach (var secao in secoes)
        {
            var paragrafos = secao.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);
            var atual = new StringBuilder();

            foreach (var paragrafo in paragrafos)
            {
                var trecho = paragrafo.Trim();
                if (trecho.Length == 0)
                {
                    continue;
                }

                if (atual.Length > 0 && atual.Length + trecho.Length > maxChars)
                {
                    resultado.Add(atual.ToString().Trim());
                    atual.Clear();
                }

                if (atual.Length > 0)
                {
                    atual.Append("\n\n");
                }

                atual.Append(trecho);
            }

            if (atual.Length > 0)
            {
                resultado.Add(atual.ToString().Trim());
            }
        }

        return resultado;
    }

    private static List<string> QuebrarPorSecao(string conteudo)
    {
        var linhas = conteudo.Split('\n');
        var secoes = new List<string>();
        var atual = new StringBuilder();

        foreach (var linha in linhas)
        {
            if (linha.StartsWith('#') && atual.Length > 0)
            {
                secoes.Add(atual.ToString());
                atual.Clear();
            }

            atual.Append(linha).Append('\n');
        }

        if (atual.Length > 0)
        {
            secoes.Add(atual.ToString());
        }

        return secoes;
    }

    private static string CalcularHash(string conteudo)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(conteudo));
        return Convert.ToHexStringLower(bytes);
    }
}
