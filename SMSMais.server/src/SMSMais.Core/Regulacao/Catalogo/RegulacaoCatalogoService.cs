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
using SMSMais.Core.Regulacao.Regras;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
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
    /// <summary>
    /// Lote do provedor de embeddings. Era 128 ("560 origens em 5 chamadas") e a primeira
    /// chamada estourou o timeout do HttpClient em produção. 64 dobra o número de idas e divide
    /// por dois o tempo de cada uma — e, com o `catch` corrigido, um lote lento agora custa
    /// aquele lote, não o sync inteiro.
    /// </summary>
    private const int TamanhoLote = 64;

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

        var agora = DateTime.UtcNow;

        // Antes de casar por chave, devolve a cada origem dos combos posicionais a chave que o
        // recurso dela tem HOJE — ver `RealinharPosicionaisAsync`.
        await RealinharPosicionaisAsync(desejadas, existentes, agora, ct);
        var porChave = existentes.ToDictionary(o => (o.Sistema, o.ChaveExterna));

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

    // ---------------------------------------------------------------- combos posicionais

    /// <summary>
    /// Sistemas cuja chave externa é o <c>value</c> de um combo — <b>posição, não identidade</b>.
    /// O SISREG fica de fora: a chave dele é o código SIGTAP, que não muda.
    /// </summary>
    private static readonly SistemaRegulacao[] Posicionais =
        [SistemaRegulacao.Ser, SistemaRegulacao.Sernit, SistemaRegulacao.EsusSg];

    /// <summary>Quem o recurso É, independente do número que o combo deu a ele hoje.</summary>
    private static string Identidade(SistemaRegulacao sistema, string? ramo, string chave, string rotulo) =>
        $"{(int)sistema}|{ramo}|{chave.Split('|')[0]}|{ChaveRotulo.Normalizar(rotulo)}";

    /// <summary>
    /// Realinha as origens dos combos posicionais pelo RÓTULO antes do casamento por chave.
    ///
    /// <para><b>Por que existe:</b> o <c>value</c> do combo do SER desliza quando a SES acrescenta
    /// um recurso (medido em 22/09 e 30/09/2026: 1030 → 1067). Casando por chave, a origem que
    /// tinha o número 1032 era RENOMEADA para o recurso que passou a ocupar o 1032 — e continuava
    /// ligada ao procedimento antigo. Foram 320 origens do SER e 54 do SERNIT ligadas ao
    /// procedimento errado: o formulário de "Genética Pediátrica" saía com os campos de
    /// "Cardiologia - Hipertensão Arterial Resistente".</para>
    ///
    /// <para>Aqui a origem acompanha o recurso dela (mesmo sistema, ramo, tipo e rótulo) para a
    /// chave nova. Quem ocupa uma chave que agora é de outro recurso e não tem para onde ir — o
    /// recurso dela saiu do combo — vira lápide: inativa, com a chave liberada. O laço principal
    /// então cria procedimento novo para o recurso novo, em vez de renomear o velho.</para>
    ///
    /// <para>Duas gravações porque a troca de chaves entre origens esbarraria no índice único
    /// <c>(sistema, chave_externa)</c> no meio do caminho.</para>
    /// </summary>
    private async Task RealinharPosicionaisAsync(
        List<OrigemDesejada> desejadas, List<RegulacaoProcedimentoOrigem> existentes,
        DateTime agora, CancellationToken ct)
    {
        var desejadasPos = desejadas.Where(d => Posicionais.Contains(d.Sistema)).ToList();
        if (desejadasPos.Count == 0) return;

        // A ativa primeiro e a confirmada por pessoa antes da automática: se o mesmo rótulo tiver
        // duas origens, quem fica com o recurso é a que alguém já validou.
        var porIdentidade = new Dictionary<string, RegulacaoProcedimentoOrigem>(StringComparer.Ordinal);
        foreach (var o in existentes
                     .Where(o => Posicionais.Contains(o.Sistema) && !o.ChaveExterna.StartsWith('~'))
                     .OrderByDescending(o => o.Ativo)
                     .ThenByDescending(o => o.ConfirmadoEm.HasValue)
                     .ThenBy(o => o.CriadoEm))
        {
            porIdentidade.TryAdd(Identidade(o.Sistema, o.Ramo, o.ChaveExterna, o.RotuloExterno), o);
        }

        var destino = new Dictionary<RegulacaoProcedimentoOrigem, string>();
        var donas = new HashSet<RegulacaoProcedimentoOrigem>();
        foreach (var d in desejadasPos)
        {
            if (!porIdentidade.TryGetValue(Identidade(d.Sistema, d.Ramo, d.ChaveExterna, d.Rotulo), out var dona)
                || !donas.Add(dona))
            {
                continue;
            }
            if (dona.ChaveExterna != d.ChaveExterna) destino[dona] = d.ChaveExterna;
        }

        var chavesDesejadas = desejadasPos.Select(d => (d.Sistema, d.ChaveExterna)).ToHashSet();
        var lapides = existentes
            .Where(o => Posicionais.Contains(o.Sistema) && !donas.Contains(o)
                        && chavesDesejadas.Contains((o.Sistema, o.ChaveExterna)))
            .ToList();

        if (destino.Count == 0 && lapides.Count == 0) return;

        foreach (var o in destino.Keys.Concat(lapides))
        {
            o.ChaveExterna = $"~{o.Id:N}";
            o.AtualizadoEm = agora;
        }
        foreach (var o in lapides) o.Ativo = false;
        await db.SaveChangesAsync(ct);

        foreach (var (o, chave) in destino) o.ChaveExterna = chave;
        await db.SaveChangesAsync(ct);

        log.LogInformation(
            "Catálogo da regulação: {Movidas} origem(ns) acompanharam o recurso para o número novo; "
            + "{Lapides} ficaram sem recurso no combo e foram inativadas.",
            destino.Count, lapides.Count);
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
        Guid? SernitId,
        Guid? EsusSgId = null);

    private async Task<List<OrigemDesejada>> LerOrigensDosSistemasAsync(CancellationToken ct)
    {
        var lista = new List<OrigemDesejada>();

        var sisreg = await db.SisregProcedimentosSigtap.AsNoTracking()
            .Select(p => new { p.Id, p.Codigo, p.Nome, p.Grupo })
            .ToListAsync(ct);
        lista.AddRange(sisreg.Select(p => new OrigemDesejada(
            SistemaRegulacao.Sisreg, p.Codigo, p.Nome, null,
            TipoDoNomeSisreg(p.Nome), p.Id, null, null)));

        // Só o que estava no combo na ÚLTIMA listagem de cada (tipo, ramo). O espelho guarda os
        // números que saíram (é como se enxerga o que saiu do ar), mas o SER renumera o combo
        // inteiro — em 30/09/2026 os números iam de 1038 em diante, e as linhas de agosto (988 a
        // 1037) ficaram com rótulos velhos. Lidas como vivas, viravam origens duplicadas.
        var serTodos = await db.SerCatalogoRecursos.AsNoTracking()
            .Select(r => new { r.Id, r.Tipo, r.Valor, r.Rotulo, r.AmbulatorioEstadual, r.SincronizadoEm })
            .ToListAsync(ct);
        var ser = serTodos
            .GroupBy(r => (r.Tipo, r.AmbulatorioEstadual))
            .SelectMany(g => SoDaUltimaListagem(g, r => r.SincronizadoEm))
            .ToList();
        lista.AddRange(ser.Select(r => new OrigemDesejada(
            SistemaRegulacao.Ser,
            ChaveDoEspelho((int)r.Tipo, r.AmbulatorioEstadual ? "AE" : "NAO_AE", r.Id),
            r.Rotulo,
            r.AmbulatorioEstadual ? "AE" : "NAO_AE",
            r.Tipo == TipoRecursoSer.Exame ? TipoProcedimentoRegulacao.Exame : TipoProcedimentoRegulacao.Consulta,
            null, r.Id, null)));

        var sernitTodos = await db.SernitCatalogoRecursos.AsNoTracking()
            .Select(r => new { r.Id, r.Tipo, r.Valor, r.Rotulo, r.SincronizadoEm })
            .ToListAsync(ct);
        var sernit = sernitTodos
            .GroupBy(r => r.Tipo)
            .SelectMany(g => SoDaUltimaListagem(g, r => r.SincronizadoEm))
            .ToList();
        lista.AddRange(sernit.Select(r => new OrigemDesejada(
            SistemaRegulacao.Sernit,
            ChaveDoEspelho((int)r.Tipo, null, r.Id),
            r.Rotulo,
            null,
            r.Tipo == TipoRecursoSernit.Exame ? TipoProcedimentoRegulacao.Exame : TipoProcedimentoRegulacao.Consulta,
            null, null, r.Id)));

        // ESUS de São Gonçalo (ADR-0063): o combo "reguláveis por solicitante". Só os ATIVOS —
        // procedimento que saiu do combo deixa de ser ofertado e a origem é inativada abaixo.
        var esusSg = await db.EsusSgCatalogoRecursos.AsNoTracking()
            .Where(r => r.Ativo)
            .Select(r => new { r.Id, r.Tipo, r.Valor, r.Rotulo })
            .ToListAsync(ct);
        lista.AddRange(esusSg.Select(r => new OrigemDesejada(
            SistemaRegulacao.EsusSg,
            ChaveDoEspelho((int)r.Tipo, null, r.Id),
            r.Rotulo,
            null,
            r.Tipo == TipoRecursoEsusSg.Exame ? TipoProcedimentoRegulacao.Exame : TipoProcedimentoRegulacao.Consulta,
            null, null, null, r.Id)));

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

    /// <summary>
    /// A chave externa das origens de SER, SERNIT e ESUS SG: <c>{tipo}|{ramo}|{id da linha do
    /// espelho}</c> (sem o ramo fora do SER) — <b>a nossa numeração</b>, não a do combo.
    ///
    /// <para>Até 07/10/2026 a chave levava o <c>value</c> do combo, que é posição: a SES renumera e
    /// a chave passava a apontar outro recurso. A linha do espelho agora é identificada pelo nome
    /// (<see cref="IdentidadePorNome"/>) e não muda quando o combo renumera — então a chave também
    /// não. Quem precisa conversar com o sistema ao vivo acha o recurso pelo NOME na hora. As
    /// origens com chave no formato antigo migram sozinhas na primeira passada, por
    /// <see cref="RealinharPosicionaisAsync"/> (a identidade dele é tipo + ramo + rótulo).</para>
    /// </summary>
    public static string ChaveDoEspelho(int tipo, string? ramo, Guid recursoId) =>
        ramo is null ? $"{tipo}|{recursoId:N}" : $"{tipo}|{ramo}|{recursoId:N}";

    /// <summary>
    /// As linhas da última listagem de um combo. A sincronização do espelho carimba com a MESMA
    /// hora todas as linhas que vieram na listagem; o que ficou de uma listagem anterior tem hora
    /// mais velha. A folga de uma hora cobre dados de antes de 01/10/2026, quando a leitura dos
    /// campos ainda reescrevia o carimbo alguns minutos depois da listagem.
    /// </summary>
    private static IEnumerable<T> SoDaUltimaListagem<T>(IEnumerable<T> grupo, Func<T, DateTime> carimbo)
    {
        var linhas = grupo.ToList();
        if (linhas.Count == 0) return [];
        var ultima = linhas.Max(carimbo);
        return linhas.Where(l => carimbo(l) >= ultima.AddHours(-1));
    }

    private static void LigarEspelho(RegulacaoProcedimentoOrigem origem, OrigemDesejada d)
    {
        origem.SisregProcedimentoSigtapId = d.SisregId;
        origem.SerCatalogoRecursoId = d.SerId;
        origem.SernitCatalogoRecursoId = d.SernitId;
        origem.EsusSgCatalogoRecursoId = d.EsusSgId;
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
            // `ct.IsCancellationRequested` é o que separa as duas coisas que o .NET representa
            // com a MESMA exceção: o cliente desistiu (propaga) e o HttpClient estourou o próprio
            // timeout (é um lote que falhou). `TaskCanceledException` herda de
            // `OperationCanceledException`, então o filtro anterior — `is not
            // OperationCanceledException` — deixava o timeout do provedor escapar e derrubar o
            // sync inteiro com 500, contrariando o comentário logo abaixo. Medido em produção em
            // 07/09/2026: ERRO-47UCPG.
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
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
    /// Só o rótulo entra no vetor — sem o nome do sistema e sem o ramo.
    ///
    /// <para>O plano 01 mandava embedar <c>"{rótulo} ({Sistema} {Ramo})"</c>. Medido: com o
    /// sistema no texto, dois recursos de <b>nome idêntico</b> em sistemas diferentes ficam em
    /// cosseno <b>0,851</b> contra um corte de 0,85 — ou seja, o par que a sugestão existe para
    /// encontrar fica pendurado na fronteira, e some com qualquer variação de grafia. O nome do
    /// sistema é ruído justamente no eixo que estamos comparando.</para>
    ///
    /// <para>Sistema e ramo continuam sendo colunas, e é por elas que se filtra — não precisam
    /// estar no vetor para cumprir esse papel.</para>
    /// </summary>
    private static string TextoParaEmbedding(RegulacaoProcedimentoOrigem o) => o.RotuloExterno;

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
