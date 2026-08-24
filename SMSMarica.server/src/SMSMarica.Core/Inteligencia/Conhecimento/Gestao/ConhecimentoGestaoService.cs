using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pgvector;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Inteligencia.Fontes;
using SMSMarica.Core.Inteligencia.Provedores;
using SMSMais.Data;
using SMSMais.Data.Entities.Ia;

namespace SMSMarica.Core.Inteligencia.Conhecimento.Gestao;

/// <summary>
/// Implementação da gestão de conhecimento por base. Documentos ficam em
/// <c>ia_documento_conhecimento</c> com o conteúdo no banco (editáveis pela tela); os que vieram
/// de arquivo do repo (caminho sem o prefixo <c>modelo/</c> nem <c>manual/</c>) são só-leitura aqui.
/// Ver ADR-0023.
/// </summary>
public sealed class ConhecimentoGestaoService(
    SmsMaisDbContext db,
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

        // Introspecção é METADADO: um banco grande tem dezenas de milhares de colunas. O teto normal
        // de linhas (1000) truncaria o dicionário — daria "83 tabelas" num banco de 1.600. Por isso
        // o override alto. Ver ADR-0023.
        const int LimiteIntrospeccao = 2_000_000;

        var colunas = await origem.ExecutarAsync(
            IntrospeccaoSql.Colunas(fonte.Dialeto), ct, maxLinhasOverride: LimiteIntrospeccao);
        if (!colunas.Sucesso)
        {
            throw new ConflitoException("conhecimento.introspeccao",
                $"Não foi possível ler o schema da base: {colunas.Erro}");
        }

        var fks = await origem.ExecutarAsync(
            IntrospeccaoSql.ChavesEstrangeiras(fonte.Dialeto), ct, maxLinhasOverride: LimiteIntrospeccao);
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

            // Extração em massa NÃO embeda inline (trava em base grande). Fica para o backfill.
            await AplicarConteudoAsync(doc, d.Conteudo, ct, embeddar: false);
        }

        await db.SaveChangesAsync(ct);

        var totalObjetos = modelo.TotalTabelas + modelo.TotalViews;
        string? aviso = null;
        if (modelo.Documentadas < totalObjetos)
        {
            aviso = $"A base tem {totalObjetos} objetos ({modelo.TotalTabelas} tabelas + "
                    + $"{modelo.TotalViews} views); documentei os primeiros {modelo.Documentadas} "
                    + "(teto atual). Aumente o limite de tabelas para cobrir o resto.";
        }
        if (!fks.Sucesso)
        {
            aviso = (aviso is null ? "" : aviso + " ")
                    + "Os relacionamentos (FKs) não puderam ser lidos; o modelo saiu só com colunas.";
        }
        if (_embeddingsHabilitado)
        {
            aviso = (aviso is null ? "" : aviso + " ")
                    + "Agora clique em \"Gerar embeddings\" para indexar o modelo (RAG).";
        }

        return new ExtracaoModeloResultado(
            modelo.TotalTabelas, modelo.TotalViews, modelo.Documentadas, modelo.TotalFks,
            modelo.Documentos.Count, aviso);
    }

    public async Task<EmbeddingsBackfillResultado> GerarEmbeddingsPendentesAsync(
        Guid fonteId, CancellationToken ct = default)
    {
        await GarantirFonteAsync(fonteId, ct);

        var total = await db.IaChunksConhecimento.CountAsync(c => c.FonteId == fonteId, ct);
        var jaTinham = await db.IaChunksConhecimento
            .CountAsync(c => c.FonteId == fonteId && c.Embedding != null, ct);

        // Lote pequeno o suficiente pra caber num request do provedor (limite de itens/tokens);
        // teto por chamada segura contra timeout — como é idempotente, chamar de novo continua.
        const int TamanhoLote = 100;
        const int TetoPorChamada = 8000;
        var gerados = 0;

        while (gerados < TetoPorChamada)
        {
            var pendentes = await db.IaChunksConhecimento
                .Where(c => c.FonteId == fonteId && c.Embedding == null)
                .OrderBy(c => c.Id)
                .Take(TamanhoLote)
                .ToListAsync(ct);

            if (pendentes.Count == 0)
            {
                break;
            }

            var vetores = await embeddings.EmbeddarLoteAsync(
                pendentes.Select(c => c.Conteudo).ToList(), ct);

            for (var i = 0; i < pendentes.Count; i++)
            {
                pendentes[i].Embedding = new Vector(vetores[i]);
            }

            await db.SaveChangesAsync(ct);
            gerados += pendentes.Count;
        }

        var restantes = await db.IaChunksConhecimento
            .CountAsync(c => c.FonteId == fonteId && c.Embedding == null, ct);

        var aviso = restantes > 0
            ? $"Faltam {restantes} chunks (parou no teto de segurança). Rode de novo pra continuar — é idempotente."
            : null;

        return new EmbeddingsBackfillResultado(total, jaTinham, gerados, restantes, aviso);
    }

    /// <summary>
    /// Grava o conteúdo, re-chunka e (se habilitado E <paramref name="embeddar"/>) re-embeda.
    /// Idempotente pelo hash. Na extração em massa passamos <c>embeddar: false</c>: embedar
    /// milhares de docs DENTRO do request trava a extração (uma base grande = milhares de
    /// chamadas ao provedor). O embedding fica para o backfill (botão "Gerar embeddings"),
    /// que é idempotente. Ver ADR-0023.
    /// </summary>
    private async Task AplicarConteudoAsync(
        IaDocumentoConhecimento doc, string conteudo, CancellationToken ct, bool embeddar = true)
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
        if (_embeddingsHabilitado && embeddar)
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
