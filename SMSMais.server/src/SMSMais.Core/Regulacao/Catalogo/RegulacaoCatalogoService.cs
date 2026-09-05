using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pgvector;
using Pgvector.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Sigtap;
using SMSMais.Core.Inteligencia.Provedores;
using SMSMais.Core.Regulacao.Catalogo.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Regulacao.Catalogo;

/// <inheritdoc cref="IRegulacaoCatalogoService"/>
public sealed class RegulacaoCatalogoService(
    SmsMaisDbContext db,
    IServicoEmbeddings embeddings,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<RegulacaoCatalogoService> log) : IRegulacaoCatalogoService
{
    /// <summary>Lote do provedor de embeddings. 560 origens cabem em 5 chamadas.</summary>
    private const int TamanhoLote = 128;

    /// <summary>
    /// Corte da sugestão de pareamento. Abaixo disso o par é ruído: medido no spike c, os pares
    /// legítimos que a igualdade de chave não pega são de contenção, com cosseno alto.
    /// </summary>
    private const double CorteSugestao = 0.85;

    private const string ModeloEmbeddingsPadrao = "voyage-3";

    public async Task<RegulacaoCatalogoSyncResultadoDto> SincronizarAsync(CancellationToken ct)
    {
        var desejadas = await LerOrigensDosSistemasAsync(ct);

        var existentes = await db.RegulacaoProcedimentoOrigens
            .Include(o => o.Procedimento)
            .ToListAsync(ct);
        var porChave = existentes.ToDictionary(o => (o.Sistema, o.ChaveExterna));

        var agora = DateTime.UtcNow;
        var usuario = usuarioAtual.UsuarioId;
        int novas = 0, canonicosNovos = 0;

        foreach (var d in desejadas)
        {
            if (porChave.TryGetValue((d.Sistema, d.ChaveExterna), out var origem))
            {
                // Origem que já existe: o rótulo pode ter mudado quando a SES recompila o
                // catálogo. Reativar é de propósito — recurso volta a ser ofertado.
                var mudou = origem.RotuloExterno != d.Rotulo || origem.Ramo != d.Ramo || !origem.Ativo;
                if (mudou)
                {
                    origem.RotuloExterno = d.Rotulo;
                    origem.Ramo = d.Ramo;
                    origem.Ativo = true;
                    origem.AtualizadoEm = agora;
                }
                LigarEspelho(origem, d);
                continue;
            }

            var canonico = new RegulacaoProcedimento
            {
                Id = Guid.CreateVersion7(),
                NomeCanonico = d.Rotulo,
                NomeNormalizado = SugestaoSigtap.Normalizar(d.Rotulo),
                Tipo = d.Tipo,
                Ativo = true,
                CriadoEm = agora,
                CriadoPor = usuario,
            };
            db.RegulacaoProcedimentos.Add(canonico);
            canonicosNovos++;

            var nova = new RegulacaoProcedimentoOrigem
            {
                Id = Guid.CreateVersion7(),
                ProcedimentoId = canonico.Id,
                Sistema = d.Sistema,
                ChaveExterna = d.ChaveExterna,
                RotuloExterno = d.Rotulo,
                Ramo = d.Ramo,
                Vinculo = VinculoOrigemRegulacao.Automatico,
                Ativo = true,
                CriadoEm = agora,
            };
            LigarEspelho(nova, d);
            db.RegulacaoProcedimentoOrigens.Add(nova);
            porChave[(d.Sistema, d.ChaveExterna)] = nova;
            existentes.Add(nova);
            novas++;
        }

        // Sumiu do catálogo de origem → inativa, nunca apaga: solicitação antiga continua
        // apontando para o recurso que foi escolhido no dia.
        var chavesVivas = desejadas.Select(d => (d.Sistema, d.ChaveExterna)).ToHashSet();
        var desativadas = 0;
        foreach (var o in existentes.Where(o => o.Ativo && !chavesVivas.Contains((o.Sistema, o.ChaveExterna))))
        {
            o.Ativo = false;
            o.AtualizadoEm = agora;
            desativadas++;
        }

        await db.SaveChangesAsync(ct);

        var (gerados, semEmbedding) = await AtualizarEmbeddingsAsync(ct);
        var sugestoes = await CalcularSugestoesAsync(ct);

        log.LogInformation(
            "Catálogo da regulação sincronizado: {Novas} origens novas, {Desativadas} desativadas, "
            + "{Canonicos} canônicos novos, {Embeddings} embeddings, {Sem} sem embedding, {Sugestoes} sugestões.",
            novas, desativadas, canonicosNovos, gerados, semEmbedding, sugestoes);

        return new RegulacaoCatalogoSyncResultadoDto(
            novas, desativadas, canonicosNovos, gerados, semEmbedding, sugestoes);
    }

    // ---------------------------------------------------------------- origens

    private sealed record OrigemDesejada(
        SistemaRegulacao Sistema,
        string ChaveExterna,
        string Rotulo,
        string? Ramo,
        TipoProcedimentoRegulacao Tipo,
        Guid? SisregId,
        Guid? SerId,
        Guid? SernitId);

    private async Task<List<OrigemDesejada>> LerOrigensDosSistemasAsync(CancellationToken ct)
    {
        var lista = new List<OrigemDesejada>();

        var sisreg = await db.SisregProcedimentosSigtap.AsNoTracking()
            .Select(p => new { p.Id, p.Codigo, p.Nome, p.Grupo })
            .ToListAsync(ct);
        lista.AddRange(sisreg.Select(p => new OrigemDesejada(
            SistemaRegulacao.Sisreg, p.Codigo, p.Nome, null,
            TipoDoNomeSisreg(p.Nome), p.Id, null, null)));

        var ser = await db.SerCatalogoRecursos.AsNoTracking()
            .Select(r => new { r.Id, r.Tipo, r.Valor, r.Rotulo, r.AmbulatorioEstadual })
            .ToListAsync(ct);
        lista.AddRange(ser.Select(r => new OrigemDesejada(
            SistemaRegulacao.Ser,
            // O ramo entra na chave: o mesmo `valor` existe nos dois com formulários diferentes.
            $"{(int)r.Tipo}|{r.Valor}|{(r.AmbulatorioEstadual ? "AE" : "NAO_AE")}",
            r.Rotulo,
            r.AmbulatorioEstadual ? "AE" : "NAO_AE",
            r.Tipo == TipoRecursoSer.Exame ? TipoProcedimentoRegulacao.Exame : TipoProcedimentoRegulacao.Consulta,
            null, r.Id, null)));

        var sernit = await db.SernitCatalogoRecursos.AsNoTracking()
            .Select(r => new { r.Id, r.Tipo, r.Valor, r.Rotulo })
            .ToListAsync(ct);
        lista.AddRange(sernit.Select(r => new OrigemDesejada(
            SistemaRegulacao.Sernit,
            $"{(int)r.Tipo}|{r.Valor}",
            r.Rotulo,
            null,
            r.Tipo == TipoRecursoSernit.Exame ? TipoProcedimentoRegulacao.Exame : TipoProcedimentoRegulacao.Consulta,
            null, null, r.Id)));

        // Chave duplicada na origem seria bug do espelho, mas o unique do banco derrubaria o
        // sync inteiro — melhor ficar com a primeira e registrar.
        var vistas = new HashSet<(SistemaRegulacao, string)>();
        var unicas = new List<OrigemDesejada>(lista.Count);
        foreach (var d in lista)
        {
            if (vistas.Add((d.Sistema, d.ChaveExterna))) unicas.Add(d);
            else log.LogWarning("Chave duplicada no catálogo de origem: {Sistema} {Chave}", d.Sistema, d.ChaveExterna);
        }
        return unicas;
    }

    private static void LigarEspelho(RegulacaoProcedimentoOrigem origem, OrigemDesejada d)
    {
        origem.SisregProcedimentoSigtapId = d.SisregId;
        origem.SerCatalogoRecursoId = d.SerId;
        origem.SernitCatalogoRecursoId = d.SernitId;
    }

    /// <summary>
    /// O SISREG não separa consulta de exame no catálogo; o nome é a única pista. Palavra que
    /// não decide fica <c>Outro</c> — errar para "Outro" é inofensivo, errar para "Consulta"
    /// esconderia o procedimento de um filtro por tipo.
    /// </summary>
    private static TipoProcedimentoRegulacao TipoDoNomeSisreg(string nome)
    {
        var n = SugestaoSigtap.Normalizar(nome);
        if (n.Contains("CONSULTA", StringComparison.Ordinal)) return TipoProcedimentoRegulacao.Consulta;
        if (n.Contains("CIRURGIA", StringComparison.Ordinal)) return TipoProcedimentoRegulacao.Cirurgia;
        if (n.Contains("EXAME", StringComparison.Ordinal)
            || n.Contains("TOMOGRAFIA", StringComparison.Ordinal)
            || n.Contains("RESSONANCIA", StringComparison.Ordinal)
            || n.Contains("ULTRASSON", StringComparison.Ordinal)
            || n.Contains("RADIOGRAFIA", StringComparison.Ordinal)
            || n.Contains("ENDOSCOPIA", StringComparison.Ordinal)
            || n.Contains("BIOPSIA", StringComparison.Ordinal)
            || n.Contains("MAMOGRAFIA", StringComparison.Ordinal))
        {
            return TipoProcedimentoRegulacao.Exame;
        }
        return TipoProcedimentoRegulacao.Outro;
    }

    // ---------------------------------------------------------------- embeddings

    private async Task<(int Gerados, int Sem)> AtualizarEmbeddingsAsync(CancellationToken ct)
    {
        var modelo = await ObterModeloEmbeddingsAsync(ct);

        var ativas = await db.RegulacaoProcedimentoOrigens
            .Where(o => o.Ativo)
            .ToListAsync(ct);

        var pendentes = ativas
            .Select(o => (Origem: o, Texto: TextoParaEmbedding(o), Hash: string.Empty))
            .Select(x => (x.Origem, x.Texto, Hash: Hash(modelo, x.Texto)))
            .Where(x => x.Origem.EmbeddingHash != x.Hash || x.Origem.Embedding is null)
            .ToList();

        if (pendentes.Count == 0) return (0, 0);

        int gerados = 0, sem = 0;
        foreach (var lote in pendentes.Chunk(TamanhoLote))
        {
            try
            {
                var vetores = await embeddings.EmbeddarLoteAsync([.. lote.Select(x => x.Texto)], ct);
                for (var i = 0; i < lote.Length; i++)
                {
                    lote[i].Origem.Embedding = new Vector(vetores[i]);
                    lote[i].Origem.EmbeddingHash = lote[i].Hash;
                    lote[i].Origem.EmbeddingEm = DateTime.UtcNow;
                }
                gerados += lote.Length;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Um lote que falha não pode derrubar o sync: o catálogo (rótulos, oferta,
                // pareamento por igualdade) vale sem embedding, e a busca lexical continua de pé.
                sem += lote.Length;
                log.LogWarning(ex, "Lote de embeddings do catálogo da regulação falhou ({N} origens).", lote.Length);
            }
        }

        await db.SaveChangesAsync(ct);
        return (gerados, sem);
    }

    private async Task<string> ObterModeloEmbeddingsAsync(CancellationToken ct)
    {
        var modelo = await db.IaConfiguracoes.AsNoTracking()
            .Select(c => c.ModeloEmbeddings)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(modelo) ? ModeloEmbeddingsPadrao : modelo;
    }

    /// <summary>
    /// O sistema entra no texto embedado porque a mesma palavra tem peso diferente em cada
    /// catálogo, e o ramo do SER separa dois recursos que se escrevem igual.
    /// </summary>
    private static string TextoParaEmbedding(RegulacaoProcedimentoOrigem o) =>
        o.Ramo is null
            ? $"{o.RotuloExterno} ({o.Sistema})"
            : $"{o.RotuloExterno} ({o.Sistema} {o.Ramo})";

    /// <summary>
    /// O modelo entra no hash: trocar de modelo tem de invalidar tudo sozinho, senão o catálogo
    /// fica com metade dos vetores de um espaço e metade de outro, e a busca vira loteria.
    /// </summary>
    private static string Hash(string modelo, string texto) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{modelo}|{texto}")));

    // ---------------------------------------------------------------- pareamento

    private async Task<int> CalcularSugestoesAsync(CancellationToken ct)
    {
        // As candidatas vêm rastreadas e com o vetor já em mãos: a alternativa era buscar o
        // vetor e recarregar a entidade por candidata, o que dava três idas ao banco por origem
        // (~1.700 num catálogo de 560). Sobra uma consulta por candidata, a do vizinho mais
        // próximo, que é justamente a que o índice HNSW existe para atender.
        var candidatas = await db.RegulacaoProcedimentoOrigens
            .Where(o => o.Ativo && o.ConfirmadoEm == null && o.Embedding != null)
            .ToListAsync(ct);

        var sugestoes = 0;
        foreach (var origem in candidatas)
        {
            var vetor = origem.Embedding!;

            var melhor = await db.RegulacaoProcedimentoOrigens
                .Where(o => o.Ativo
                    && o.Embedding != null
                    && o.Sistema != origem.Sistema                 // par é ENTRE sistemas
                    && o.ProcedimentoId != origem.ProcedimentoId)  // já estão juntos: nada a sugerir
                .OrderBy(o => o.Embedding!.CosineDistance(vetor))
                .Select(o => new { o.ProcedimentoId, Distancia = o.Embedding!.CosineDistance(vetor) })
                .FirstOrDefaultAsync(ct);

            var score = melhor is null ? 0 : 1 - melhor.Distancia;

            if (melhor is not null && score >= CorteSugestao)
            {
                origem.SugeridoProcedimentoId = melhor.ProcedimentoId;
                origem.SugeridoScore = score;
                sugestoes++;
            }
            else if (origem.SugeridoProcedimentoId is not null)
            {
                // O catálogo mudou e a sugestão de antes não se sustenta mais.
                origem.SugeridoProcedimentoId = null;
                origem.SugeridoScore = null;
            }
        }

        await db.SaveChangesAsync(ct);
        return sugestoes;
    }

    public async Task<IReadOnlyList<RegulacaoSugestaoPareamentoDto>> ListarSugestoesAsync(CancellationToken ct)
    {
        var brutas = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.Ativo && o.SugeridoProcedimentoId != null && o.ConfirmadoEm == null)
            .OrderByDescending(o => o.SugeridoScore)
            .Select(o => new
            {
                o.Id,
                o.Sistema,
                o.RotuloExterno,
                o.ProcedimentoId,
                CanonicoAtual = o.Procedimento!.NomeCanonico,
                SugeridoId = o.SugeridoProcedimentoId!.Value,
                o.SugeridoScore,
            })
            .ToListAsync(ct);

        var ids = brutas.Select(b => b.SugeridoId).Distinct().ToList();
        var nomes = await db.RegulacaoProcedimentos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NomeCanonico, ct);

        return [.. brutas.Select(b => new RegulacaoSugestaoPareamentoDto(
            b.Id, b.Sistema, b.RotuloExterno, b.ProcedimentoId, b.CanonicoAtual,
            b.SugeridoId, nomes.GetValueOrDefault(b.SugeridoId, "(removido)"), b.SugeridoScore ?? 0))];
    }

    public async Task ConfirmarPareamentoAsync(Guid origemId, Guid procedimentoId, CancellationToken ct)
    {
        var origem = await db.RegulacaoProcedimentoOrigens.FirstOrDefaultAsync(o => o.Id == origemId, ct)
            ?? throw new NaoEncontradoException("Origem de procedimento da regulação", origemId);

        var destino = await db.RegulacaoProcedimentos.FirstOrDefaultAsync(p => p.Id == procedimentoId, ct)
            ?? throw new NaoEncontradoException("Procedimento canônico da regulação", procedimentoId);

        var anterior = origem.ProcedimentoId;
        origem.ProcedimentoId = destino.Id;
        origem.Vinculo = VinculoOrigemRegulacao.Confirmado;
        origem.ConfirmadoEm = DateTime.UtcNow;
        origem.ConfirmadoPor = usuarioAtual.UsuarioId;
        origem.SugeridoProcedimentoId = null;
        origem.SugeridoScore = null;
        origem.AtualizadoEm = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await DesativarCanonicoOrfaoAsync(anterior, ct);
    }

    public async Task RejeitarPareamentoAsync(Guid origemId, CancellationToken ct)
    {
        var origem = await db.RegulacaoProcedimentoOrigens.FirstOrDefaultAsync(o => o.Id == origemId, ct)
            ?? throw new NaoEncontradoException("Origem de procedimento da regulação", origemId);

        origem.SugeridoProcedimentoId = null;
        origem.SugeridoScore = null;
        // Rejeitar também confirma: sem isso o sync proporia o mesmo par na próxima passada.
        origem.Vinculo = VinculoOrigemRegulacao.Confirmado;
        origem.ConfirmadoEm = DateTime.UtcNow;
        origem.ConfirmadoPor = usuarioAtual.UsuarioId;
        origem.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RenomearCanonicoAsync(Guid procedimentoId, string nome, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ValidacaoException("nome", "Informe o nome do procedimento.");
        if (nome.Length > 300)
            throw new ValidacaoException("nome", "O nome deve ter no máximo 300 caracteres.");

        var p = await db.RegulacaoProcedimentos.FirstOrDefaultAsync(x => x.Id == procedimentoId, ct)
            ?? throw new NaoEncontradoException("Procedimento canônico da regulação", procedimentoId);

        p.NomeCanonico = nome.Trim();
        p.NomeNormalizado = SugestaoSigtap.Normalizar(p.NomeCanonico);
        p.AtualizadoEm = DateTime.UtcNow;
        p.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Canônico que perdeu a última origem ativa sai da busca, mas não é apagado.</summary>
    private async Task DesativarCanonicoOrfaoAsync(Guid procedimentoId, CancellationToken ct)
    {
        var temOrigem = await db.RegulacaoProcedimentoOrigens
            .AnyAsync(o => o.ProcedimentoId == procedimentoId && o.Ativo, ct);
        if (temOrigem) return;

        var p = await db.RegulacaoProcedimentos.FirstOrDefaultAsync(x => x.Id == procedimentoId, ct);
        if (p is null || !p.Ativo) return;

        p.Ativo = false;
        p.AtualizadoEm = DateTime.UtcNow;
        p.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }
}
