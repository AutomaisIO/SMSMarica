using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SMSMais.Core.AgendaRegulacao;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.EstrategiasFila.Dtos;
using SMSMais.Core.Integracoes.SisregWeb.Comum;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.EstrategiasFila;

/// <summary>
/// Monta o cenário atual de um procedimento a partir do que já está no banco — nunca fala com o
/// SISREG.
///
/// <para><b>De onde sai cada número (e por quê).</b></para>
/// <list type="bullet">
/// <item><b>Fila</b>: <c>sisreg_fila_pendente</c> com <c>saiu_em is null</c>, nomes da família.
/// É quem espera HOJE — a fila é um estado, não um histórico.</item>
/// <item><b>Entrada</b>: quem PEDIU em cada semana — união, por número de solicitação, da fila
/// (quem ainda espera ou saiu sem agendar) com as marcações (quem já foi agendado). Só a fila
/// subestimaria: quem foi rápido já saiu dela.</item>
/// <item><b>Vazão</b>: marcações por semana da data agendada (dia de Brasília). É o que a rede
/// realmente atende, não o que a escala promete.</item>
/// <item><b>Oferta</b>: escalas ativas expandidas nas próximas 4 semanas e divididas por 4 — o
/// mesmo método da Análise de vagas, que trata escala de um dia só e vigência que acaba no meio
/// sem ramo especial. Vaga da regulação = 1ª vez + reserva (medido em 10/09/2026: o ECO declara
/// tudo como reserva). <b>Agenda local fica fora da capacidade</b>: a regulação não a vê.</item>
/// <item><b>Aproveitamento</b>: marcações ÷ vagas ofertadas nas últimas 8 semanas. É o que
/// impede o simulador de mentir — escala viva com vaga morta (ECG do CDT) aparece aqui.</item>
/// </list>
///
/// <para><b>Semanas são segunda a domingo</b> (<c>date_trunc('week')</c>), e a semana corrente,
/// incompleta, fica fora das médias — senão toda série terminaria com uma queda falsa.</para>
/// </summary>
public sealed class CenarioFilaService(
    SmsMaisDbContext db,
    IAgendaDemandaService demanda,
    IMemoryCache cache) : ICenarioFilaService
{
    private const int SemanasEntrada = 26;
    private const int SemanasOcupacao = 8;
    private const int SemanasOferta = 4;

    /// <summary>Sem medição (procedimento sem escala), a fração de vaga que vira atendimento.
    /// Conservador de propósito: melhor prometer menos.</summary>
    private const double AproveitamentoPadrao = 0.85;

    private static readonly TimeSpan CacheLista = TimeSpan.FromMinutes(10);
    private const string ChaveCacheLista = "estrategias-fila:procedimentos";

    // Faixas iguais às da Demanda regulada, para as duas telas se compararem.
    private static readonly (int Ordem, string Rotulo, int Lo, int Hi)[] Faixas =
    [
        (1, "Até 7 dias", 0, 7), (2, "8 a 15 dias", 8, 15), (3, "16 a 30 dias", 16, 30),
        (4, "31 a 60 dias", 31, 60), (5, "61 a 90 dias", 61, 90), (6, "91 a 180 dias", 91, 180),
        (7, "Mais de 180 dias", 181, int.MaxValue),
    ];

    // ------------------------------------------------------------------ lista

    public async Task<IReadOnlyList<ProcedimentoComFilaDto>> ListarProcedimentosAsync(
        string? busca, string? ordenar, CancellationToken ct = default)
    {
        var lista = await cache.GetOrCreateAsync(ChaveCacheLista, async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = CacheLista;
            return await MontarListaAsync(ct);
        }) ?? [];

        // "Tem estratégia" não entra no cache: muda a cada salvamento.
        var comEstrategia = await db.EstrategiasFila.AsNoTracking()
            .Where(e => e.ExcluidoEm == null && e.Status != StatusEstrategiaFila.Arquivada)
            .Select(e => new { e.ProcedimentoCodigo, e.ProcedimentoNome })
            .ToListAsync(ct);
        var chaves = comEstrategia
            .Select(e => e.ProcedimentoCodigo ?? e.ProcedimentoNome)
            .ToHashSet(StringComparer.Ordinal);

        IEnumerable<ProcedimentoComFilaDto> q = lista
            .Select(p => p with { TemEstrategia = chaves.Contains(p.Codigo ?? p.Nome) });

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            q = q.Where(p =>
                p.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)
                || (p.NomeCanonico?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.Codigo?.StartsWith(termo, StringComparison.Ordinal) ?? false));
        }

        q = (ordenar?.Trim().ToLowerInvariant()) switch
        {
            "espera" => q.OrderByDescending(p => p.EsperaMedianaDias ?? -1).ThenByDescending(p => p.NaFila),
            "nome" => q.OrderBy(p => p.Nome, StringComparer.Ordinal),
            _ => q.OrderByDescending(p => p.NaFila).ThenBy(p => p.Nome, StringComparer.Ordinal),
        };

        return [.. q];
    }

    private async Task<List<ProcedimentoComFilaDto>> MontarListaAsync(CancellationToken ct)
    {
        var hoje = Hoje();

        // 1. A fila aberta, linha a linha (nome + data do pedido). São dezenas de milhares de
        //    linhas pequenas; trazer tudo permite calcular a mediana de cada FAMÍLIA de forma exata,
        //    o que não sai de medianas por nome.
        var fila = await db.SisregFilaPendentes.AsNoTracking()
            .Where(f => f.SaiuEm == null && f.ProcedimentoNome != null)
            .Select(f => new { Nome = f.ProcedimentoNome!, f.DataSolicitacao })
            .ToListAsync(ct);
        var filaPorNome = fila
            .GroupBy(f => f.Nome, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.DataSolicitacao).ToList(), StringComparer.Ordinal);

        // 2. A oferta vigente, por procedimento da escala: unidades, profissionais e vagas de
        //    regulação por semana (próximas 4 semanas ÷ 4, agenda local fora).
        var ofertas = await db.Database.SqlQueryRaw<OfertaPorProcedimentoLinha>(
            Formatar(SqlOfertaPorProcedimento), hoje, hoje.AddDays(7 * SemanasOferta)).ToListAsync(ct);

        // 3. Nomes conhecidos por código (escala + marcações), para expandir as famílias.
        var nomesEscala = await db.SisregEscalas.AsNoTracking()
            .Select(e => new { e.ProcedimentoCodigo, e.ProcedimentoNome })
            .Distinct().ToListAsync(ct);
        var nomesMarcacao = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.ProcedimentoCodigoSisreg != null && s.ProcedimentoTexto != null)
            .Select(s => new { Codigo = s.ProcedimentoCodigoSisreg!, Nome = s.ProcedimentoTexto! })
            .Distinct().ToListAsync(ct);
        var porCodigo = nomesEscala.Select(n => (n.ProcedimentoCodigo, n.ProcedimentoNome))
            .Concat(nomesMarcacao.Select(n => (n.Codigo, n.Nome)))
            .Distinct().ToList();

        // 4. Canônicos do catálogo, por código do SISREG.
        var canonicos = await CanonicosPorCodigoAsync(ct);

        var resultado = new List<ProcedimentoComFilaDto>();
        var nomesCobertos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var o in ofertas)
        {
            var familia = FamiliaEmMemoria(o.Codigo, o.Nome, porCodigo);
            nomesCobertos.UnionWith(familia);
            var esperas = familia.SelectMany(n => filaPorNome.GetValueOrDefault(n) ?? [])
                .Where(d => d is not null).Select(d => hoje.DayNumber - d!.Value.DayNumber)
                .Where(x => x >= 0).Order().ToList();
            var total = familia.Sum(n => filaPorNome.GetValueOrDefault(n)?.Count ?? 0);

            var canonico = canonicos.TryGetValue(o.Codigo, out var c) ? c : null;
            resultado.Add(new ProcedimentoComFilaDto(
                o.Codigo, o.Nome, canonico?.Nome, FamiliaProcedimentoSisreg.EhCodigoDeGrupo(o.Codigo),
                total, esperas.Count > 0 ? esperas[esperas.Count / 2] : null,
                (int)Math.Round(o.VagasRegulacao / (double)SemanasOferta),
                o.Unidades, o.Profissionais, TemEstrategia: false));
        }

        // 5. Quem só existe na fila: pediram, e ninguém oferta. É o caso mais grave, e é o que
        //    uma lista montada a partir da escala deixaria de fora.
        foreach (var (nome, datas) in filaPorNome)
        {
            if (nomesCobertos.Contains(nome)) continue;
            var esperas = datas.Where(d => d is not null).Select(d => hoje.DayNumber - d!.Value.DayNumber)
                .Where(x => x >= 0).Order().ToList();
            resultado.Add(new ProcedimentoComFilaDto(
                null, nome, null, false, datas.Count,
                esperas.Count > 0 ? esperas[esperas.Count / 2] : null, 0, 0, 0, false));
        }

        return resultado;
    }

    /// <summary>Mesma regra de <see cref="FamiliaProcedimentoSisreg.NomesDaFamiliaAsync"/>, mas
    /// sobre listas já carregadas — a lista tem centenas de procedimentos e não pode ir ao banco
    /// por linha.</summary>
    private static HashSet<string> FamiliaEmMemoria(
        string codigo, string nome, List<(string Codigo, string Nome)> porCodigo)
    {
        var nomes = new HashSet<string>(StringComparer.Ordinal) { nome };
        if (FamiliaProcedimentoSisreg.GrupoDoCodigo(codigo) is not { } grupo) return nomes;

        if (FamiliaProcedimentoSisreg.EhCodigoDeGrupo(codigo))
        {
            var prefixo = codigo[..4];
            nomes.UnionWith(porCodigo.Where(p => p.Codigo.StartsWith(prefixo, StringComparison.Ordinal)).Select(p => p.Nome));
        }
        else
        {
            nomes.UnionWith(porCodigo.Where(p => p.Codigo == grupo).Select(p => p.Nome));
        }

        return nomes;
    }

    // ------------------------------------------------------------------ cenário

    public async Task<CenarioFilaDto> MontarAsync(
        string? procedimentoCodigo, string procedimentoNome, CancellationToken ct = default)
    {
        var nome = (procedimentoNome ?? string.Empty).Trim();
        if (nome.Length == 0)
            throw new ValidacaoException("procedimento.nome_obrigatorio", "Informe o procedimento.");

        var codigo = string.IsNullOrWhiteSpace(procedimentoCodigo) ? null : procedimentoCodigo.Trim();
        var hoje = Hoje();
        var inicioSemanaAtual = InicioDaSemana(hoje);

        var familia = await FamiliaProcedimentoSisreg.NomesDaFamiliaAsync(db, nome, codigo, ct);
        var nomes = familia.ToArray();
        var (codigos, prefixo) = RecorteDeCodigos(codigo);

        // ---- fila ----
        var filaLinhas = await db.SisregFilaPendentes.AsNoTracking()
            .Where(f => f.SaiuEm == null && f.ProcedimentoNome != null && nomes.Contains(f.ProcedimentoNome))
            .Select(f => new { f.Risco, f.DataSolicitacao })
            .ToListAsync(ct);
        var fila = MontarFila(filaLinhas.Select(f => (f.Risco, f.DataSolicitacao)).ToList(), hoje);

        // ---- entrada e vazão (semanas cheias) ----
        var inicioSerie = inicioSemanaAtual.AddDays(-7 * SemanasEntrada);
        var entradaSerie = await db.Database.SqlQueryRaw<SemanaDto>(
            Formatar(SqlEntradaSemanal), nomes, inicioSerie, codigos, (object?)prefixo ?? DBNull.Value, inicioSemanaAtual)
            .ToListAsync(ct);
        var vazaoSerie = await db.Database.SqlQueryRaw<SemanaDto>(
            Formatar(SqlVazaoSemanal), nomes, inicioSerie, codigos, (object?)prefixo ?? DBNull.Value, inicioSemanaAtual)
            .ToListAsync(ct);
        var entrada = MontarRitmo(entradaSerie, inicioSerie, inicioSemanaAtual);
        var vazao = MontarRitmo(vazaoSerie, inicioSerie, inicioSemanaAtual);

        var saidas = await db.SisregFilaPendentes.AsNoTracking()
            .Where(f => f.SaiuEm != null && f.ProcedimentoNome != null && nomes.Contains(f.ProcedimentoNome))
            .GroupBy(f => f.SaiuPara)
            .Select(g => new { Para = g.Key, Qtd = g.Count() })
            .ToListAsync(ct);
        var totalSaidas = saidas.Sum(s => s.Qtd);
        double? semAgendar = totalSaidas == 0
            ? null
            : saidas.Where(s => s.Para == SaidaDaFilaSisreg.SaiuSemAgendar).Sum(s => s.Qtd) / (double)totalSaidas;

        // ---- oferta (próximas 4 semanas) ----
        var blocos = await db.Database.SqlQueryRaw<BlocoExpandido>(
            Formatar(SqlBlocosExpandidos), nomes, hoje, hoje.AddDays(7 * SemanasOferta), codigos, (object?)prefixo ?? DBNull.Value)
            .ToListAsync(ct);
        var oferta = MontarOferta(blocos);

        // ---- ocupação (últimas 8 semanas cheias) ----
        var inicioOcupacao = inicioSemanaAtual.AddDays(-7 * SemanasOcupacao);
        var blocosPassados = await db.Database.SqlQueryRaw<BlocoExpandido>(
            Formatar(SqlBlocosExpandidos), nomes, inicioOcupacao, inicioSemanaAtual, codigos, (object?)prefixo ?? DBNull.Value)
            .ToListAsync(ct);
        var agendados = await db.Database.SqlQueryRaw<int>(
            Formatar(SqlAgendadosNoPeriodo), nomes, inicioOcupacao, codigos, (object?)prefixo ?? DBNull.Value, inicioSemanaAtual)
            .FirstOrDefaultAsync(ct);
        var ofertadas = blocosPassados.Sum(b => b.V1 + b.Vr + b.Vres);
        var ocupacao = new OcupacaoCenarioDto(
            SemanasOcupacao, ofertadas, agendados,
            ofertadas > 0 ? Math.Min(1, agendados / (double)ofertadas) : null);

        // ---- procedimento ----
        CanonicoRef? canonico = null;
        if (codigo is not null && (await CanonicosPorCodigoAsync(ct)).TryGetValue(codigo, out var c)) canonico = c;
        var procedimento = new ProcedimentoCenarioDto(
            codigo, nome, canonico?.Nome, canonico?.Id,
            codigo is not null && FamiliaProcedimentoSisreg.EhCodigoDeGrupo(codigo),
            [.. familia.Order(StringComparer.Ordinal)]);

        var cobertura = await demanda.CoberturaAsync(ct);

        var parametros = ParametrosIniciais(oferta, ocupacao, entrada);

        return new CenarioFilaDto(
            procedimento, fila, entrada, vazao, semAgendar, oferta, ocupacao, cobertura, parametros, DateTime.UtcNow);
    }

    // ------------------------------------------------------------------ montagem

    private static FilaCenarioDto MontarFila(List<(int? Risco, DateOnly? Data)> linhas, DateOnly hoje)
    {
        var dias = linhas.Where(l => l.Data is not null)
            .Select(l => hoje.DayNumber - l.Data!.Value.DayNumber)
            .Where(d => d >= 0).Order().ToList();

        var porRisco = linhas.GroupBy(l => l.Risco?.ToString() ?? "sem")
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var faixas = Faixas.Select(f => new DemandaFaixaEsperaDto(
            f.Ordem, f.Rotulo, dias.Count(d => d >= f.Lo && d <= f.Hi))).ToList();

        return new FilaCenarioDto(
            linhas.Count,
            porRisco,
            dias.Count > 0 ? dias[dias.Count / 2] : null,
            dias.Count > 0 ? dias[Math.Min(dias.Count - 1, (int)Math.Floor(dias.Count * 0.9))] : null,
            dias.Count > 0 ? dias[^1] : null,
            linhas.Where(l => l.Data is not null).Select(l => l.Data!.Value).DefaultIfEmpty().Min() is { } d && d != default ? d : null,
            faixas);
    }

    /// <summary>Preenche as semanas sem movimento com zero — semana ausente é zero, não buraco.</summary>
    private static RitmoDto MontarRitmo(List<SemanaDto> serie, DateOnly inicio, DateOnly fimExclusivo)
    {
        var mapa = serie.ToDictionary(s => s.Semana, s => s.Quantidade);
        var cheia = new List<SemanaDto>();
        for (var s = inicio; s < fimExclusivo; s = s.AddDays(7))
            cheia.Add(new SemanaDto(s, mapa.GetValueOrDefault(s)));

        double Media(int ultimas) => cheia.Count == 0
            ? 0
            : Math.Round(cheia.TakeLast(Math.Min(ultimas, cheia.Count)).Average(x => x.Quantidade), 2);

        var m12 = Media(12);
        var m4 = Media(4);
        return new RitmoDto(m12, Media(26), m12 > 0 ? Math.Round(m4 / m12, 2) : null, cheia);
    }

    private static OfertaCenarioDto MontarOferta(List<BlocoExpandido> blocos)
    {
        var regulados = blocos.Where(b => !b.AgendaLocal).ToList();
        var locais = blocos.Where(b => b.AgendaLocal).ToList();

        static int PorSemana(int total) => (int)Math.Round(total / (double)SemanasOferta);

        var unidades = blocos
            .GroupBy(b => (b.UnidadeId, b.AgendaLocal))
            .Select(g => new UnidadeOfertaDto(
                g.Key.UnidadeId, g.First().UnidadeNome, g.First().Cnes, g.Key.AgendaLocal,
                g.Select(b => b.Cpf).Distinct().Count(),
                PorSemana(g.Sum(b => b.V1 + b.Vres)),
                PorSemana(g.Sum(b => b.V1 + b.Vr + b.Vres))))
            .OrderByDescending(u => u.VagasRegulacaoSemana).ThenBy(u => u.Nome)
            .ToList();

        var profissionais = regulados
            .GroupBy(b => b.Cpf)
            .Select(g => new ProfissionalOfertaDto(
                g.First().ProfissionalNome, g.First().Cbo,
                g.GroupBy(b => b.UnidadeNome).OrderByDescending(x => x.Count()).First().Key,
                [.. g.Select(b => (int)b.Dia.DayOfWeek).Distinct().Order()],
                PorSemana(g.Sum(b => b.V1 + b.Vres))))
            .OrderByDescending(p => p.VagasRegulacaoSemana).ThenBy(p => p.Nome)
            .ToList();

        var nProf = regulados.Select(b => b.Cpf).Distinct().Count();
        // Turno = profissional num dia com bloco. É o que a escala diz de verdade: a hora de
        // início/fim NÃO é tempo de trabalho (a ultrassonografia tem blocos de 5 min com 125 vagas).
        var turnos = regulados.Select(b => (b.Cpf, b.Dia)).Distinct().Count();
        var horas = regulados.Sum(b => (b.HoraFim - b.HoraInicio).TotalHours);
        var vagasReg = regulados.Sum(b => b.V1 + b.Vres);

        var turnosSemana = Math.Round(turnos / (double)SemanasOferta, 2);
        var turnosPorProf = nProf == 0 ? 0 : Math.Round(turnosSemana / nProf, 2);
        var atendPorTurno = turnos == 0 ? 0 : Math.Round(vagasReg / (double)turnos, 2);

        return new OfertaCenarioDto(
            unidades, profissionais,
            [.. regulados.Select(b => (int)b.Dia.DayOfWeek).Distinct().Order()],
            regulados.Count > 0 ? regulados.Min(b => b.HoraInicio) : null,
            regulados.Count > 0 ? regulados.Max(b => b.HoraFim) : null,
            PorSemana(regulados.Count),
            PorSemana(regulados.Sum(b => b.V1)),
            PorSemana(regulados.Sum(b => b.Vr)),
            PorSemana(regulados.Sum(b => b.Vres)),
            PorSemana(vagasReg),
            PorSemana(regulados.Sum(b => b.V1 + b.Vr + b.Vres)),
            turnosSemana, turnosPorProf, atendPorTurno,
            Math.Round(horas / SemanasOferta, 1),
            PorSemana(locais.Sum(b => b.V1 + b.Vr + b.Vres)));
    }

    /// <summary>
    /// Os parâmetros que reproduzem a oferta de hoje. Sem escala nenhuma, entram valores de
    /// partida razoáveis (2 turnos/semana, 10 por turno) para o agente ter de onde propor — e
    /// ficam livres.
    /// </summary>
    private static ParametrosEstrategia ParametrosIniciais(OfertaCenarioDto oferta, OcupacaoCenarioDto ocupacao, RitmoDto entrada)
    {
        var temOferta = oferta.Profissionais.Count > 0 && oferta.AtendimentosPorTurno > 0;
        var unidadesReguladas = oferta.Unidades.Where(u => !u.AgendaLocal).Select(u => u.UnidadeId).Distinct().Count();

        return new ParametrosEstrategia(
            Objetivo: ObjetivoEstrategia.ZerarEmSemanas,
            PrazoAlvoSemanas: null,
            Unidades: new ParametroNumero(unidadesReguladas, false, 0, null),
            Profissionais: new ParametroNumero(oferta.Profissionais.Count, false, 0, null),
            TurnosPorProfissionalSemana: new ParametroNumero(temOferta ? oferta.TurnosPorProfissionalSemana : 2, false, 0, 14),
            AtendimentosPorTurno: new ParametroNumero(temOferta ? oferta.AtendimentosPorTurno : 10, false, 0, null),
            Aproveitamento: new ParametroNumero(Math.Round(ocupacao.Aproveitamento ?? AproveitamentoPadrao, 2), false, 0, 1),
            EntradaSemanal: new ParametroNumero(entrada.MediaSemanal12, true, 0, null),
            Mutiroes: [],
            MutiroesTravados: false,
            HorizonteSemanas: ParametrosEstrategia.HorizontePadrao);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record CanonicoRef(Guid Id, string Nome);

    private async Task<Dictionary<string, CanonicoRef>> CanonicosPorCodigoAsync(CancellationToken ct)
    {
        var linhas = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.Ativo && o.Sistema == SistemaRegulacao.Sisreg)
            .Select(o => new { o.ChaveExterna, o.ProcedimentoId, Nome = o.Procedimento!.NomeCanonico })
            .ToListAsync(ct);
        var mapa = new Dictionary<string, CanonicoRef>(StringComparer.Ordinal);
        foreach (var l in linhas) mapa.TryAdd(l.ChaveExterna, new CanonicoRef(l.ProcedimentoId, l.Nome));
        return mapa;
    }

    /// <summary>Códigos exatos e/ou prefixo <c>LIKE</c> que recortam escala e marcações da família.</summary>
    private static (string[] Codigos, string? Prefixo) RecorteDeCodigos(string? codigo)
    {
        if (codigo is null || FamiliaProcedimentoSisreg.GrupoDoCodigo(codigo) is not { } grupo) return ([], null);
        return FamiliaProcedimentoSisreg.EhCodigoDeGrupo(codigo)
            ? ([], codigo[..4] + "%")
            : ([codigo, grupo], null);
    }

    private static DateOnly Hoje() => DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));

    /// <summary>Segunda-feira da semana de <paramref name="d"/> — a régua do <c>date_trunc('week')</c>.</summary>
    private static DateOnly InicioDaSemana(DateOnly d) =>
        d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private static string Formatar(string sql)
    {
        for (var i = 0; i < 10; i++) sql = sql.Replace($"{{{i}}}", $"@p{i}");
        return sql;
    }

    // ------------------------------------------------------------------ SQL

    /// <summary>Recorte de família em <c>solicitacao</c>: nome exato OU código exato OU prefixo de grupo.</summary>
    private const string FiltroFamiliaSolicitacao = """
        (s.procedimento_texto = any({0}::text[])
         or (cardinality({2}::text[]) > 0 and s.procedimento_codigo_sisreg = any({2}::text[]))
         or ({3}::text is not null and s.procedimento_codigo_sisreg like {3}::text))
        """;

    private const string FiltroFamiliaEscala = """
        (e.procedimento_nome = any({0}::text[])
         or (cardinality({3}::text[]) > 0 and e.procedimento_codigo = any({3}::text[]))
         or ({4}::text is not null and e.procedimento_codigo like {4}::text))
        """;

    /// <summary>{0} nomes, {1} início, {2} códigos, {3} prefixo, {4} fim (exclusivo).</summary>
    private const string SqlEntradaSemanal = """
        with pedidos as (
            select f.codigo_solicitacao codigo, f.data_solicitacao data
            from smsmarica.sisreg_fila_pendente f
            where f.procedimento_nome = any({0}::text[])
              and f.data_solicitacao >= {1}::date and f.data_solicitacao < {4}::date
            union
            select s.codigo_solicitacao, s.data_solicitacao
            from smsmarica.solicitacao s
            where s.excluido_em is null
              and s.codigo_solicitacao <> '0000'
              and s.data_solicitacao >= {1}::date and s.data_solicitacao < {4}::date
              and
        """ + FiltroFamiliaSolicitacao + """
        )
        select date_trunc('week', data)::date "Semana", count(distinct codigo)::int "Quantidade"
        from pedidos
        group by 1
        order by 1
        """;

    /// <summary>{0} nomes, {1} início, {2} códigos, {3} prefixo, {4} fim (exclusivo).</summary>
    private const string SqlVazaoSemanal = """
        select date_trunc('week', (s.data_agendada at time zone 'America/Sao_Paulo')::date)::date "Semana",
               count(*)::int "Quantidade"
        from smsmarica.solicitacao s
        where s.excluido_em is null and s.cancelado_em is null and s.data_agendada is not null
          and (s.data_agendada at time zone 'America/Sao_Paulo')::date >= {1}::date
          and (s.data_agendada at time zone 'America/Sao_Paulo')::date < {4}::date
          and
        """ + FiltroFamiliaSolicitacao + """
        group by 1
        order by 1
        """;

    /// <summary>{0} nomes, {1} início, {2} códigos, {3} prefixo, {4} fim (exclusivo). Um só número.</summary>
    private const string SqlAgendadosNoPeriodo = """
        select count(*)::int "Value"
        from smsmarica.solicitacao s
        where s.excluido_em is null and s.cancelado_em is null and s.data_agendada is not null
          and (s.data_agendada at time zone 'America/Sao_Paulo')::date >= {1}::date
          and (s.data_agendada at time zone 'America/Sao_Paulo')::date < {4}::date
          and
        """ + FiltroFamiliaSolicitacao;

    /// <summary>
    /// A escala expandida em ocorrências: {0} nomes, {1} início, {2} fim (exclusivo), {3} códigos,
    /// {4} prefixo. Cada linha é um bloco num dia concreto.
    /// </summary>
    private const string SqlBlocosExpandidos = """
        with dias as (
            select generate_series({1}::date, ({2}::date - 1), '1 day')::date d
        )
        select e.unidade_id "UnidadeId", u.nome "UnidadeNome", e.cnes "Cnes", e.agenda_local "AgendaLocal",
               e.profissional_cpf "Cpf", e.profissional_nome "ProfissionalNome", e.cbo_descricao "Cbo",
               d.d "Dia", e.hora_inicio "HoraInicio", e.hora_fim "HoraFim",
               e.vagas_primeira_vez "V1", e.vagas_retorno "Vr", e.vagas_reserva "Vres"
        from smsmarica.sisreg_escala e
        join smsmarica.unidade u on u.id = e.unidade_id
        join dias d on extract(dow from d.d) = e.dia_semana
                   and d.d between e.vigencia_inicio and e.vigencia_fim
        where e.status = 1 and not e.ausente
          and
        """ + FiltroFamiliaEscala;

    /// <summary>Oferta por procedimento da escala nas próximas semanas: {0} início, {1} fim (exclusivo).</summary>
    private const string SqlOfertaPorProcedimento = """
        with dias as (
            select generate_series({0}::date, ({1}::date - 1), '1 day')::date d
        )
        select e.procedimento_codigo "Codigo", min(e.procedimento_nome) "Nome",
               count(distinct e.unidade_id)::int "Unidades",
               count(distinct e.profissional_cpf)::int "Profissionais",
               coalesce(sum(case when e.agenda_local then 0 else e.vagas_primeira_vez + e.vagas_reserva end), 0)::int "VagasRegulacao"
        from smsmarica.sisreg_escala e
        join dias d on extract(dow from d.d) = e.dia_semana
                   and d.d between e.vigencia_inicio and e.vigencia_fim
        where e.status = 1 and not e.ausente
        group by e.procedimento_codigo
        """;

    // ------------------------------------------------------------------ linhas SQL

    private sealed record OfertaPorProcedimentoLinha(string Codigo, string Nome, int Unidades, int Profissionais, int VagasRegulacao);

    private sealed record BlocoExpandido(
        Guid UnidadeId, string UnidadeNome, string? Cnes, bool AgendaLocal,
        string Cpf, string ProfissionalNome, string? Cbo,
        DateOnly Dia, TimeOnly HoraInicio, TimeOnly HoraFim, int V1, int Vr, int Vres);
}
