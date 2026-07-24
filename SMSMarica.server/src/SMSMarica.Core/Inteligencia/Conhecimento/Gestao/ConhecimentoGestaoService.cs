using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pgvector;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Inteligencia.Fontes;
using SMSMarica.Core.Inteligencia.Provedores;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Core.Inteligencia.Conhecimento.Gestao;

/// <summary>
/// Implementação da gestão de conhecimento por base. Documentos ficam em
/// <c>ia_documento_conhecimento</c> com o conteúdo no banco (editáveis pela tela); os que vieram
/// de arquivo do repo (caminho sem o prefixo <c>modelo/</c> nem <c>manual/</c>) são só-leitura aqui.
/// Ver ADR-0023.
/// </summary>
public sealed class ConhecimentoGestaoService(
    SmsMaricaDbContext db,
    IFonteDadosFactory fonteFactory,
    IServicoEmbeddings embeddings,
    IConfiguration configuracao) : IConhecimentoGestaoService
{
    private readonly bool _embeddingsHabilitado = configuracao.GetValue("Ia:Embeddings:Habilitado", false);

    // Prefixos de caminho que marcam docs geridos pela tela (vs. sincronizados do repo).
    private const string PrefixoModelo = "modelo/";
    private const string PrefixoManual = "manual/";

    public async Task<IReadOnlyList<DocumentoResumoDto>> ListarDocumentosAsync(
        Guid fonteId, CancellationToken ct = default)
    {
        await GarantirFonteAsync(fonteId, ct);

        var docs = await db.IaDocumentosConhecimento
            .AsNoTracking()
            .Where(d => d.FonteId == fonteId)
            .Select(d => new
            {
                d.Id,
                d.Caminho,
                d.Versao,
                Tamanho = d.Conteudo.Length,
                Chunks = d.Chunks.Count,
                d.AtualizadoEm,
                d.CriadoEm,
            })
            .ToListAsync(ct);

        return docs
            .OrderBy(d => d.Caminho, StringComparer.OrdinalIgnoreCase)
            .Select(d => new DocumentoResumoDto(
                d.Id, d.Caminho, d.Versao, d.Tamanho, d.Chunks,
                !EhGerenciavel(d.Caminho), d.AtualizadoEm ?? d.CriadoEm))
            .ToList();
    }

    public async Task<DocumentoDetalheDto> ObterDocumentoAsync(
        Guid fonteId, Guid docId, CancellationToken ct = default)
    {
        var d = await db.IaDocumentosConhecimento
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == docId && x.FonteId == fonteId, ct)
            ?? throw new NaoEncontradoException("Documento de conhecimento", docId);

        return new DocumentoDetalheDto(
            d.Id, d.Caminho, d.Conteudo, d.Versao, !EhGerenciavel(d.Caminho), d.AtualizadoEm ?? d.CriadoEm);
    }

    public async Task<DocumentoDetalheDto> SalvarDocumentoAsync(
        Guid fonteId, Guid? docId, SalvarDocumentoDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await GarantirFonteAsync(fonteId, ct);

        var caminho = NormalizarCaminho(dto.Caminho);
        if (string.IsNullOrWhiteSpace(dto.Conteudo))
        {
            throw new ValidacaoException("conhecimento.conteudo", "O documento não pode ser vazio.");
        }

        IaDocumentoConhecimento doc;
        if (docId is { } id)
        {
            doc = await db.IaDocumentosConhecimento
                .FirstOrDefaultAsync(x => x.Id == id && x.FonteId == fonteId, ct)
                ?? throw new NaoEncontradoException("Documento de conhecimento", id);

            if (!EhGerenciavel(doc.Caminho))
            {
                throw new ConflitoException(
                    "conhecimento.repo", "Este documento vem do repositório e não é editável pela tela.");
            }
        }
        else
        {
            var duplicado = await db.IaDocumentosConhecimento
                .AnyAsync(x => x.FonteId == fonteId && x.Caminho == caminho, ct);
            if (duplicado)
            {
                throw new ConflitoException("conhecimento.caminho", $"Já existe um documento '{caminho}'.");
            }

            doc = new IaDocumentoConhecimento
            {
                Id = Guid.NewGuid(),
                FonteId = fonteId,
                Caminho = caminho,
                Versao = 0,
                CriadoEm = DateTime.UtcNow,
            };
            db.IaDocumentosConhecimento.Add(doc);
        }

        await AplicarConteudoAsync(doc, dto.Conteudo, ct);
        await db.SaveChangesAsync(ct);

        return new DocumentoDetalheDto(
            doc.Id, doc.Caminho, doc.Conteudo, doc.Versao, false, doc.AtualizadoEm ?? doc.CriadoEm);
    }

    public async Task RemoverDocumentoAsync(Guid fonteId, Guid docId, CancellationToken ct = default)
    {
        var doc = await db.IaDocumentosConhecimento
            .FirstOrDefaultAsync(x => x.Id == docId && x.FonteId == fonteId, ct)
            ?? throw new NaoEncontradoException("Documento de conhecimento", docId);

        if (!EhGerenciavel(doc.Caminho))
        {
            throw new ConflitoException(
                "conhecimento.repo", "Documento do repositório não é removível pela tela.");
        }

        var chunks = await db.IaChunksConhecimento.Where(c => c.DocumentoId == doc.Id).ToListAsync(ct);
        db.IaChunksConhecimento.RemoveRange(chunks);
        db.IaDocumentosConhecimento.Remove(doc);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ExtracaoModeloResultado> ExtrairModeloAsync(
        Guid fonteId, ExtrairModeloDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var fonte = await GarantirFonteAsync(fonteId, ct);

        var origem = fonteFactory.Criar(fonte);
        var colunas = await origem.ExecutarAsync(IntrospeccaoSql.Colunas(fonte.Dialeto), ct);
        if (!colunas.Sucesso)
        {
            throw new ConflitoException("conhecimento.introspeccao",
                $"Não foi possível ler o schema da base: {colunas.Erro}");
        }

        var fks = await origem.ExecutarAsync(IntrospeccaoSql.ChavesEstrangeiras(fonte.Dialeto), ct);
        // FK falhando não é fatal — o modelo sai só com as colunas, avisando.

        var maxTabelas = Math.Clamp(dto.MaxTabelas, 1, 5000);
        var modelo = ModeloMarkdown.Montar(colunas, fks, maxTabelas);

        foreach (var d in modelo.Documentos)
        {
            var doc = await db.IaDocumentosConhecimento
                .FirstOrDefaultAsync(x => x.FonteId == fonteId && x.Caminho == d.Caminho, ct);
            if (doc is null)
            {
                doc = new IaDocumentoConhecimento
                {
                    Id = Guid.NewGuid(),
                    FonteId = fonteId,
                    Caminho = d.Caminho,
                    Versao = 0,
                    CriadoEm = DateTime.UtcNow,
                };
                db.IaDocumentosConhecimento.Add(doc);
            }

            await AplicarConteudoAsync(doc, d.Conteudo, ct);
        }

        await db.SaveChangesAsync(ct);

        string? aviso = null;
        if (modelo.Documentadas < modelo.TotalTabelas)
        {
            aviso = $"A base tem {modelo.TotalTabelas} tabelas; documentei as primeiras "
                    + $"{modelo.Documentadas} (teto atual). Aumente o limite para cobrir o resto.";
        }
        if (!fks.Sucesso)
        {
            aviso = (aviso is null ? "" : aviso + " ")
                    + "Os relacionamentos (FKs) não puderam ser lidos; o modelo saiu só com colunas.";
        }

        return new ExtracaoModeloResultado(
            modelo.TotalTabelas, modelo.Documentadas, modelo.TotalFks, modelo.Documentos.Count, aviso);
    }

    /// <summary>Grava o conteúdo, re-chunka e (se habilitado) re-embeda. Idempotente pelo hash.</summary>
    private async Task AplicarConteudoAsync(IaDocumentoConhecimento doc, string conteudo, CancellationToken ct)
    {
        var hash = Hash(conteudo);
        if (doc.Hash == hash && doc.Versao > 0)
        {
            return; // inalterado
        }

        doc.Conteudo = conteudo;
        doc.Hash = hash;
        doc.Versao += 1;
        doc.AtualizadoEm = DateTime.UtcNow;

        var antigos = await db.IaChunksConhecimento.Where(c => c.DocumentoId == doc.Id).ToListAsync(ct);
        db.IaChunksConhecimento.RemoveRange(antigos);

        var pedacos = ChunkificadorMarkdown.Chunkificar(conteudo);
        if (pedacos.Count == 0)
        {
            return;
        }

        IReadOnlyList<float[]>? vetores = null;
        if (_embeddingsHabilitado)
        {
            vetores = await embeddings.EmbeddarLoteAsync(pedacos, ct);
        }

        for (var i = 0; i < pedacos.Count; i++)
        {
            db.IaChunksConhecimento.Add(new IaChunkConhecimento
            {
                Id = Guid.NewGuid(),
                DocumentoId = doc.Id,
                FonteId = doc.FonteId,
                Ordem = i,
                Conteudo = pedacos[i],
                Embedding = vetores is null ? null : new Vector(vetores[i]),
                CriadoEm = DateTime.UtcNow,
            });
        }
    }

    private async Task<IaFonte> GarantirFonteAsync(Guid fonteId, CancellationToken ct) =>
        await db.IaFontes.FirstOrDefaultAsync(f => f.Id == fonteId && f.ExcluidoEm == null, ct)
        ?? throw new NaoEncontradoException("Fonte", fonteId);

    private static bool EhGerenciavel(string caminho) =>
        caminho.StartsWith(PrefixoModelo, StringComparison.OrdinalIgnoreCase)
        || caminho.StartsWith(PrefixoManual, StringComparison.OrdinalIgnoreCase);

    private static string NormalizarCaminho(string caminho)
    {
        var c = (caminho ?? "").Trim().Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(c))
        {
            throw new ValidacaoException("conhecimento.caminho", "Informe um nome/caminho para o documento.");
        }
        // Docs criados/editados pela tela vivem sob 'manual/' (o modelo extraído usa 'modelo/').
        if (!EhGerenciavel(c))
        {
            c = PrefixoManual + c;
        }
        if (!c.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            c += ".md";
        }
        return c;
    }

    private static string Hash(string conteudo) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(conteudo)));
}
