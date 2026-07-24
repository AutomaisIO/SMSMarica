using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using SMSMarica.Secretario.Api.Oracle;

namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Atualiza o snapshot do painel em dois ticks: rápido (Q1a/Q1b/Q1c) e lento (Q2..Q6).
/// SEMPRE sequencial — uma consulta por vez, o Oracle é produção viva de hospital.
/// O primeiro ciclo (rápido + lento) roda já no startup. Falha de consulta não derruba
/// o serviço: loga, marca <c>oracle.ok=false</c> e mantém o último snapshot bom.
/// </summary>
public sealed class PainelAtualizadorService : BackgroundService
{
    // Fallback defensivo: host sem libicu (invariant globalization + PredefinedCulturesOnly)
    // lança CultureNotFoundException — degrada para nomes de mês pt-BR hardcoded em vez de
    // derrubar o serviço no startup.
    private static readonly CultureInfo? PtBr = ObterPtBr();
    private static readonly string[] MesesPtBr =
        ["janeiro", "fevereiro", "março", "abril", "maio", "junho",
         "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    private static readonly string[] CoresContrato =
        ["VERMELHO", "AMARELO", "VERDE", "AZUL", "SEM_CLASSIFICACAO"];

    private static CultureInfo? ObterPtBr()
    {
        try
        {
            return CultureInfo.GetCultureInfo("pt-BR");
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private readonly SnapshotStore _store;
    private readonly SaluxOpcoes _salux;
    private readonly PainelOpcoes _painel;
    private readonly ILogger<PainelAtualizadorService> _logger;
    private readonly SaluxOracleFonte? _fonte;

    // Estado de erro POR CICLO: um tick rápido OK não pode apagar o erro do ciclo lento
    // (e vice-versa) — cada erro só é limpo por um ciclo bem-sucedido do MESMO tipo.
    // Só o loop do BackgroundService toca nesses campos (sequencial), sem concorrência.
    private string? _ultimoErroRapido;
    private string? _ultimoErroLento;
    private DateTimeOffset? _ultimaAtualizacaoOk;

    public PainelAtualizadorService(
        SnapshotStore store,
        IOptions<SaluxOpcoes> salux,
        IOptions<PainelOpcoes> painel,
        ILogger<PainelAtualizadorService> logger)
    {
        _store = store;
        _salux = salux.Value;
        _painel = painel.Value;
        _logger = logger;

        _fonte = string.IsNullOrWhiteSpace(_salux.Usuario) || string.IsNullOrWhiteSpace(_salux.Senha)
            ? null
            : new SaluxOracleFonte(
                _salux.Host, _salux.Porta, _salux.Servico, _salux.Usuario, _salux.Senha,
                _painel.TimeoutConsultaSegundos, maxLinhas: 500);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_fonte is null)
        {
            // Sem credencial: sobe mesmo assim e serve o snapshot persistido (ou 503) sem tocar o Oracle.
            _logger.LogWarning(
                "Credencial do Salux ausente (Salux:Usuario/Salux:Senha) — atualização desativada; servindo snapshot persistido, se houver.");
            return;
        }

        var intervaloRapido = TimeSpan.FromSeconds(Math.Max(5, _painel.IntervaloRapidoSegundos));
        var ticksPorCicloLento = Math.Max(1,
            (int)Math.Round((double)_painel.IntervaloLentoSegundos / Math.Max(5, _painel.IntervaloRapidoSegundos)));

        var tick = 0L;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Sequencial por construção: rápido termina antes do lento começar.
                await ExecutarCicloRapidoAsync(stoppingToken);

                if (tick % ticksPorCicloLento == 0)
                {
                    await ExecutarCicloLentoAsync(stoppingToken);
                }

                tick++;
                await Task.Delay(intervaloRapido, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown normal do host.
        }
    }

    // ── Ciclo rápido (Q1) ──────────────────────────────────────────────────────

    private async Task ExecutarCicloRapidoAsync(CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            var q1a = await ConsultarAsync(ConsultasPainel.Q1AguardandoPorCor(_salux.Hospital), ct);
            var q1b = await ConsultarAsync(ConsultasPainel.Q1EmAtendimento(_salux.Hospital), ct);
            var q1c = await ConsultarAsync(ConsultasPainel.Q1InternadosEHoje(_salux.Hospital), ct);

            var agora = MontarAgora(q1a, q1b, q1c);
            var carimbo = FusoBrasilia.Agora();

            _ultimoErroRapido = null;
            _ultimaAtualizacaoOk = carimbo;
            _store.Atualizar(atual => (atual ?? SnapshotVazio(carimbo)) with
            {
                GeradoEm = carimbo,
                Agora = agora,
                Oracle = StatusOracleAtual(),
            });

            _logger.LogInformation("Ciclo rápido OK em {Ms} ms.", cronometro.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var erro = ErroCurto(ex);
            _ultimoErroRapido = erro;
            _store.MarcarFalha(UltimoErroVigente() ?? erro);
            _logger.LogError("Ciclo rápido FALHOU em {Ms} ms: {Erro}", cronometro.ElapsedMilliseconds, erro);
        }
    }

    private AgoraSecao MontarAgora(ResultadoConsulta q1a, ResultadoConsulta q1b, ResultadoConsulta q1c)
    {
        // Q1a: acumula por cor normalizada (SALUX e afins somam em SEM_CLASSIFICACAO).
        var qtdPorCor = new Dictionary<string, int>();
        var somaMinutos = new Dictionary<string, double>(); // ponderada por qtd, p/ média mesclada
        foreach (var linha in q1a.Linhas)
        {
            var cor = NormalizarCor(linha[0] as string);
            var qtd = ComoInt(linha[1]);
            var minMedio = ComoDoubleOuNulo(linha[2]);
            qtdPorCor[cor] = qtdPorCor.GetValueOrDefault(cor) + qtd;
            if (minMedio is not null)
            {
                somaMinutos[cor] = somaMinutos.GetValueOrDefault(cor) + minMedio.Value * qtd;
            }
        }

        // Sempre as 5 entradas do contrato, na ordem fixa, mesmo com qtd 0.
        var aguardandoPorCor = new List<CorAguardando>(CoresContrato.Length);
        foreach (var cor in CoresContrato)
        {
            var qtd = qtdPorCor.GetValueOrDefault(cor);
            int? minMedio = qtd > 0 && somaMinutos.TryGetValue(cor, out var soma)
                ? (int)Math.Round(soma / qtd)
                : null;
            aguardandoPorCor.Add(new CorAguardando(cor, qtd, minMedio));
        }

        var emAtendimento = ComoInt(q1b.Linhas[0][0]);

        var linhaC = q1c.Linhas[0];
        var internadosAgora = ComoInt(linhaC[0]);
        var internadosMaternidade = ComoInt(linhaC[1]);

        return new AgoraSecao(
            AtualizadoEm: FusoBrasilia.Agora(),
            AguardandoMedico: aguardandoPorCor.Sum(c => c.Qtd),
            AguardandoPorCor: aguardandoPorCor,
            EmAtendimento: emAtendimento,
            InternadosAgora: internadosAgora,
            InternadosMaternidade: internadosMaternidade,
            InternadosDemais: internadosAgora - internadosMaternidade,
            MediaDiasInternacao: ComoDoubleOuNulo(linhaC[2]),
            AtendimentosHoje: ComoInt(linhaC[3]),
            InternacoesHoje: ComoInt(linhaC[4]));
    }

    // ── Ciclo lento (Q2..Q6) ───────────────────────────────────────────────────

    private async Task ExecutarCicloLentoAsync(CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            var hosp = _salux.Hospital;
            var hoje = FusoBrasilia.Agora();

            // Q2 — atendimentos por período (+ total só de dias completos do mês atual).
            var totalMesAnterior = await ConsultarEscalarAsync(
                ConsultasPainel.Q2AtendimentosPeriodo(hosp, ConsultasPainel.IniMesAnterior, ConsultasPainel.FimMesAnterior), ct);
            var totalMesAtual = await ConsultarEscalarAsync(
                ConsultasPainel.Q2AtendimentosPeriodo(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual), ct);
            var totalHoje = await ConsultarEscalarAsync(
                ConsultasPainel.Q2AtendimentosPeriodo(hosp, ConsultasPainel.IniHoje, ConsultasPainel.FimHoje), ct);
            var totalDiasCompletos = await ConsultarEscalarAsync(
                ConsultasPainel.Q2AtendimentosPeriodo(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimDiasCompletos), ct);

            // Q3/Q4 — séries de atendimento.
            var serieAtendimentos = await ConsultarAsync(ConsultasPainel.Q3SerieDiariaAtendimentos(hosp), ct);
            var porHora = await ConsultarAsync(ConsultasPainel.Q4PorHoraHoje(hosp), ct);

            // Q5 — internações por período + série (+ dias completos p/ média do mês atual).
            var intMesAnterior = await ConsultarAsync(
                ConsultasPainel.Q5InternacoesPeriodo(hosp, ConsultasPainel.IniMesAnterior, ConsultasPainel.FimMesAnterior), ct);
            var intMesAtual = await ConsultarAsync(
                ConsultasPainel.Q5InternacoesPeriodo(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual), ct);
            var intHoje = await ConsultarAsync(
                ConsultasPainel.Q5InternacoesPeriodo(hosp, ConsultasPainel.IniHoje, ConsultasPainel.FimHoje), ct);
            var intDiasCompletos = await ConsultarAsync(
                ConsultasPainel.Q5InternacoesPeriodo(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimDiasCompletos), ct);
            var serieInternacoes = await ConsultarAsync(ConsultasPainel.Q5SerieDiariaInternacoes(hosp), ct);

            // Q7 — maternidade (NASCIMENTO não tem cd_hospital próprio: o livro de partos
            // é do HMCML, única maternidade da rede na base).
            var matMesAnterior = await ConsultarAsync(
                ConsultasPainel.Q7Maternidade(ConsultasPainel.IniMesAnterior, ConsultasPainel.FimMesAnterior), ct);
            var matMesAtual = await ConsultarAsync(
                ConsultasPainel.Q7Maternidade(ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual), ct);
            var matHoje = await ConsultarAsync(
                ConsultasPainel.Q7Maternidade(ConsultasPainel.IniHoje, ConsultasPainel.FimHoje), ct);
            var matDiasCompletos = await ConsultarAsync(
                ConsultasPainel.Q7Maternidade(ConsultasPainel.IniMesAtual, ConsultasPainel.FimDiasCompletos), ct);
            var seriePartos = await ConsultarAsync(ConsultasPainel.Q7SerieDiariaPartos(), ct);

            // Q6 — espera por cor, 1× por período (a PESADA fica por último).
            var esperaHoje = await ConsultarAsync(
                ConsultasPainel.Q6EsperaPorCor(hosp, ConsultasPainel.IniHoje, ConsultasPainel.FimHoje, "SYSDATE + 3"), ct);
            var esperaMesAtual = await ConsultarAsync(
                ConsultasPainel.Q6EsperaPorCor(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual, "SYSDATE + 3"), ct);
            var esperaMesAnterior = await ConsultarAsync(
                ConsultasPainel.Q6EsperaPorCor(
                    hosp, ConsultasPainel.IniMesAnterior, ConsultasPainel.FimMesAnterior, "TRUNC(SYSDATE,'MM') + 3"), ct);

            var carimbo = FusoBrasilia.Agora();
            var atendimentos = MontarAtendimentos(
                hoje, carimbo, totalMesAnterior, totalMesAtual, totalHoje, totalDiasCompletos, serieAtendimentos, porHora);
            var internacoes = MontarInternacoes(
                hoje, carimbo, intMesAnterior, intMesAtual, intHoje, intDiasCompletos, serieInternacoes);
            var maternidade = MontarMaternidade(
                hoje, carimbo, matMesAnterior, matMesAtual, matHoje, matDiasCompletos, seriePartos);
            var espera = new EsperaPorCorSecao(carimbo, new EsperaPeriodos(
                MontarEsperaPeriodo(esperaHoje),
                MontarEsperaPeriodo(esperaMesAtual),
                MontarEsperaPeriodo(esperaMesAnterior)));

            _ultimoErroLento = null;
            _ultimaAtualizacaoOk = carimbo;
            _store.Atualizar(atual => (atual ?? SnapshotVazio(carimbo)) with
            {
                GeradoEm = carimbo,
                Atendimentos = atendimentos,
                Internacoes = internacoes,
                EsperaPorCor = espera,
                Maternidade = maternidade,
                Oracle = StatusOracleAtual(),
            });

            // Persiste só após ciclo lento OK — restart volta com o painel completo.
            await _store.PersistirAsync(ct);

            _logger.LogInformation("Ciclo lento OK em {Ms} ms.", cronometro.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var erro = ErroCurto(ex);
            _ultimoErroLento = erro;
            _store.MarcarFalha(UltimoErroVigente() ?? erro);
            _logger.LogError("Ciclo lento FALHOU em {Ms} ms: {Erro}", cronometro.ElapsedMilliseconds, erro);
        }
    }

    /// <summary>
    /// Status do Oracle derivado do estado por ciclo: <c>ok</c> exige rápido OK <b>e</b>
    /// lento OK; <c>ultimoErro</c> é o erro mais relevante ainda vigente (o do lento tem
    /// precedência e só é limpo por um ciclo LENTO bem-sucedido); <c>ultimaAtualizacaoOk</c>
    /// é o instante do último ciclo (de qualquer tipo) 100% OK que atualizou seções.
    /// </summary>
    private OracleStatus StatusOracleAtual() => new(
        Ok: _ultimoErroRapido is null && _ultimoErroLento is null,
        UltimoErro: UltimoErroVigente(),
        UltimaAtualizacaoOk: _ultimaAtualizacaoOk);

    private string? UltimoErroVigente() => _ultimoErroLento ?? _ultimoErroRapido;

    private static AtendimentosSecao MontarAtendimentos(
        DateTimeOffset hoje,
        DateTimeOffset carimbo,
        int totalMesAnterior,
        int totalMesAtual,
        int totalHoje,
        int totalDiasCompletos,
        ResultadoConsulta serieDiaria,
        ResultadoConsulta porHora)
    {
        var mesAnterior = hoje.AddMonths(-1);
        var diasMesAnterior = DateTime.DaysInMonth(mesAnterior.Year, mesAnterior.Month);

        // Média do mês atual = só dias completos ÷ (dias corridos − 1). No dia 1º não há
        // dia completo ainda — média fica nula (nunca dividir por zero).
        var diasCompletos = hoje.Day - 1;

        return new AtendimentosSecao(
            AtualizadoEm: carimbo,
            MesAnterior: new AtendimentosMesAnterior(
                Rotulo: RotuloMes(mesAnterior),
                Total: totalMesAnterior,
                Dias: diasMesAnterior,
                MediaDiaria: MediaDiaria(totalMesAnterior, diasMesAnterior)),
            MesAtual: new AtendimentosMesAtual(
                Rotulo: RotuloMes(hoje),
                Total: totalMesAtual,
                DiasCompletos: diasCompletos,
                TotalDiasCompletos: totalDiasCompletos,
                MediaDiaria: MediaDiaria(totalDiasCompletos, diasCompletos)),
            Hoje: new TotalSimples(totalHoje),
            SerieDiaria: MontarSerieDiaria(serieDiaria, hoje),
            PorHoraHoje: MontarPorHora(porHora, hoje));
    }

    private static InternacoesSecao MontarInternacoes(
        DateTimeOffset hoje,
        DateTimeOffset carimbo,
        ResultadoConsulta mesAnterior,
        ResultadoConsulta mesAtual,
        ResultadoConsulta diaAtual,
        ResultadoConsulta diasCompletosPeriodo,
        ResultadoConsulta serieDiaria)
    {
        var mesAnteriorData = hoje.AddMonths(-1);
        var diasMesAnterior = DateTime.DaysInMonth(mesAnteriorData.Year, mesAnteriorData.Month);
        var diasCompletos = hoje.Day - 1;

        var (antTotal, antMat, antDem) = LerPeriodoInternacao(mesAnterior);
        var (atuTotal, atuMat, atuDem) = LerPeriodoInternacao(mesAtual);
        var (hojTotal, hojMat, hojDem) = LerPeriodoInternacao(diaAtual);
        var (compTotal, _, _) = LerPeriodoInternacao(diasCompletosPeriodo);

        return new InternacoesSecao(
            AtualizadoEm: carimbo,
            MesAnterior: new InternacoesMes(
                RotuloMes(mesAnteriorData), antTotal, antMat, antDem, MediaDiaria(antTotal, diasMesAnterior)),
            MesAtual: new InternacoesMes(
                // Média diária do mês atual SEGUE a regra dos dias completos (a mesma dos
                // atendimentos) — decisão confirmada; o contrato foi alinhado a essa regra.
                RotuloMes(hoje), atuTotal, atuMat, atuDem, MediaDiaria(compTotal, diasCompletos)),
            Hoje: new InternacoesHoje(hojTotal, hojMat, hojDem),
            SerieDiaria: MontarSerieDiariaInternacoes(serieDiaria, hoje));
    }

    private static (int Total, int Maternidade, int Demais) LerPeriodoInternacao(ResultadoConsulta resultado)
    {
        var linha = resultado.Linhas[0];
        return (ComoInt(linha[0]), ComoInt(linha[1]), ComoInt(linha[2]));
    }

    /// <summary>Série de 35 dias contínuos terminando hoje — dias sem linha viram qtd 0 (gráfico sem buraco).</summary>
    private static List<DiaQtd> MontarSerieDiaria(ResultadoConsulta resultado, DateTimeOffset hoje)
    {
        var porDia = new Dictionary<DateOnly, int>();
        foreach (var linha in resultado.Linhas)
        {
            if (linha[0] is DateTime dia)
            {
                porDia[DateOnly.FromDateTime(dia)] = ComoInt(linha[1]);
            }
        }

        var fim = DateOnly.FromDateTime(hoje.Date);
        var serie = new List<DiaQtd>(35);
        for (var dia = fim.AddDays(-34); dia <= fim; dia = dia.AddDays(1))
        {
            serie.Add(new DiaQtd(dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), porDia.GetValueOrDefault(dia)));
        }

        return serie;
    }

    /// <summary>
    /// Série de internações (35 dias contínuos) com o split maternidade/demais por dia —
    /// SÓ esta série carrega o split (contrato); dias sem linha viram 0/0/0.
    /// </summary>
    private static List<DiaQtdInternacao> MontarSerieDiariaInternacoes(ResultadoConsulta resultado, DateTimeOffset hoje)
    {
        var porDia = new Dictionary<DateOnly, (int Qtd, int? Maternidade, int? Demais)>();
        foreach (var linha in resultado.Linhas)
        {
            if (linha[0] is DateTime dia)
            {
                porDia[DateOnly.FromDateTime(dia)] =
                    (ComoInt(linha[1]), ComoIntOuNulo(linha[2]), ComoIntOuNulo(linha[3]));
            }
        }

        var fim = DateOnly.FromDateTime(hoje.Date);
        var serie = new List<DiaQtdInternacao>(35);
        for (var dia = fim.AddDays(-34); dia <= fim; dia = dia.AddDays(1))
        {
            var (qtd, maternidade, demais) = porDia.GetValueOrDefault(dia, (0, 0, 0));
            serie.Add(new DiaQtdInternacao(
                dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), qtd, maternidade, demais));
        }

        return serie;
    }

    // ── Maternidade (Q7) ───────────────────────────────────────────────────────

    private static MaternidadeSecao MontarMaternidade(
        DateTimeOffset hoje,
        DateTimeOffset carimbo,
        ResultadoConsulta mesAnterior,
        ResultadoConsulta mesAtual,
        ResultadoConsulta diaAtual,
        ResultadoConsulta diasCompletosPeriodo,
        ResultadoConsulta serieDiaria)
    {
        var mesAnteriorData = hoje.AddMonths(-1);
        var diasMesAnterior = DateTime.DaysInMonth(mesAnteriorData.Year, mesAnteriorData.Month);
        var diasCompletos = hoje.Day - 1;

        // Média diária: mesma régua do resto do painel (mês atual = só dias completos).
        var partosDiasCompletos = ComoInt(diasCompletosPeriodo.Linhas[0][0]);

        return new MaternidadeSecao(
            AtualizadoEm: carimbo,
            MesAnterior: LerMaternidade(mesAnterior, RotuloMes(mesAnteriorData), diasMesAnterior, null),
            MesAtual: LerMaternidade(mesAtual, RotuloMes(hoje), diasCompletos, partosDiasCompletos),
            Hoje: LerMaternidade(diaAtual, "hoje", 0, null),
            SerieDiaria: MontarSerieDiariaPartos(serieDiaria, hoje));
    }

    /// <summary>
    /// Uma linha da Q7 vira o período do contrato. <paramref name="totalParaMedia"/> permite
    /// que o mês atual use o total dos dias completos (e não o parcial) no denominador.
    /// </summary>
    private static MaternidadePeriodo LerMaternidade(
        ResultadoConsulta resultado, string rotulo, int dias, int? totalParaMedia)
    {
        var l = resultado.Linhas[0];
        var partos = ComoInt(l[0]);
        var cesareas = ComoInt(l[1]);

        return new MaternidadePeriodo(
            Rotulo: rotulo,
            Partos: partos,
            Cesareas: cesareas,
            Vaginais: ComoInt(l[2]),
            Prematuros: ComoInt(l[3]),
            BaixoPeso: ComoInt(l[4]),
            PesoMedioKg: ComoDoubleOuNulo(l[5]),
            Apgar5Abaixo7: ComoInt(l[6]),
            Meninas: ComoInt(l[7]),
            Meninos: ComoInt(l[8]),
            MediaDiaria: MediaDiaria(totalParaMedia ?? partos, dias),
            PctCesarea: partos > 0 ? Math.Round(100.0 * cesareas / partos, 1) : null);
    }

    private static List<DiaPartos> MontarSerieDiariaPartos(ResultadoConsulta resultado, DateTimeOffset hoje)
    {
        var porDia = new Dictionary<DateOnly, (int Qtd, int? Cesareas)>();
        foreach (var linha in resultado.Linhas)
        {
            if (linha[0] is DateTime dia)
            {
                porDia[DateOnly.FromDateTime(dia)] = (ComoInt(linha[1]), ComoIntOuNulo(linha[2]));
            }
        }

        var fim = DateOnly.FromDateTime(hoje.Date);
        var serie = new List<DiaPartos>(35);
        for (var dia = fim.AddDays(-34); dia <= fim; dia = dia.AddDays(1))
        {
            var (qtd, cesareas) = porDia.GetValueOrDefault(dia, (0, 0));
            serie.Add(new DiaPartos(dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), qtd, cesareas));
        }

        return serie;
    }

    /// <summary>Horas 0..hora atual (Brasília) — horas sem linha viram qtd 0.</summary>
    private static List<HoraQtd> MontarPorHora(ResultadoConsulta resultado, DateTimeOffset hoje)
    {
        var porHora = new Dictionary<int, int>();
        foreach (var linha in resultado.Linhas)
        {
            porHora[ComoInt(linha[0])] = ComoInt(linha[1]);
        }

        var lista = new List<HoraQtd>(hoje.Hour + 1);
        for (var hora = 0; hora <= hoje.Hour; hora++)
        {
            lista.Add(new HoraQtd(hora, porHora.GetValueOrDefault(hora)));
        }

        return lista;
    }

    private static List<EsperaCor> MontarEsperaPeriodo(ResultadoConsulta resultado)
    {
        // Acumula por cor normalizada. SALUX/desconhecidas somam em SEM_CLASSIFICACAO —
        // médias/mediana/p90 mescladas por média ponderada (aproximação aceitável: a cor
        // SALUX é meia dúzia de casos/mês).
        var buckets = new Dictionary<string, EsperaAcumulador>();
        foreach (var linha in resultado.Linhas)
        {
            var cor = NormalizarCor(linha[0] as string);
            var bucket = buckets.TryGetValue(cor, out var existente) ? existente : new EsperaAcumulador();
            bucket.Somar(
                pacientes: ComoInt(linha[1]),
                comAtendimento: ComoInt(linha[2]),
                mediaAteTriagem: ComoDoubleOuNulo(linha[3]),
                mediaEspera: ComoDoubleOuNulo(linha[4]),
                medianaEspera: ComoDoubleOuNulo(linha[5]),
                p90Espera: ComoDoubleOuNulo(linha[6]),
                metaMin: ComoIntOuNulo(linha[7]),
                pctNaMeta: ComoDoubleOuNulo(linha[8]));
            buckets[cor] = bucket;
        }

        // Sempre as 5 cores do contrato, ordem fixa, mesmo sem linhas no período.
        var lista = new List<EsperaCor>(CoresContrato.Length);
        foreach (var cor in CoresContrato)
        {
            lista.Add(buckets.TryGetValue(cor, out var bucket)
                ? bucket.Materializar(cor)
                : new EsperaCor(cor, 0, 0, null, null, null, null, null, null));
        }

        return lista;
    }

    /// <summary>Acumulador p/ mesclar linhas da Q6 que caem na mesma cor do contrato.</summary>
    private sealed class EsperaAcumulador
    {
        private int _pacientes;
        private int _comAtendimento;
        private double _somaAteTriagem;
        private int _pesoAteTriagem;
        private double _somaEspera;
        private double _somaMediana;
        private double _somaP90;
        private double _somaPctNaMeta;
        private int _pesoEspera;
        private int? _metaMin;

        public void Somar(
            int pacientes, int comAtendimento, double? mediaAteTriagem, double? mediaEspera,
            double? medianaEspera, double? p90Espera, int? metaMin, double? pctNaMeta)
        {
            _pacientes += pacientes;
            _comAtendimento += comAtendimento;

            if (mediaAteTriagem is not null)
            {
                _somaAteTriagem += mediaAteTriagem.Value * pacientes;
                _pesoAteTriagem += pacientes;
            }

            if (mediaEspera is not null && comAtendimento > 0)
            {
                _somaEspera += mediaEspera.Value * comAtendimento;
                _somaMediana += (medianaEspera ?? 0) * comAtendimento;
                _somaP90 += (p90Espera ?? 0) * comAtendimento;
                _somaPctNaMeta += (pctNaMeta ?? 0) * comAtendimento;
                _pesoEspera += comAtendimento;
            }

            _metaMin ??= metaMin;
        }

        public EsperaCor Materializar(string cor)
        {
            // SEM_CLASSIFICACAO: contrato reporta média até triagem, meta e % na meta nulos
            // (não há cor ⇒ não há meta; SALUX mesclada aqui é ruído).
            var semClassificacao = cor == "SEM_CLASSIFICACAO";
            return new EsperaCor(
                Cor: cor,
                Pacientes: _pacientes,
                ComAtendimento: _comAtendimento,
                MediaAteTriagem: semClassificacao ? null : Ponderada(_somaAteTriagem, _pesoAteTriagem),
                MediaEspera: Ponderada(_somaEspera, _pesoEspera),
                MedianaEspera: Ponderada(_somaMediana, _pesoEspera),
                P90Espera: Ponderada(_somaP90, _pesoEspera),
                MetaMin: semClassificacao ? null : _metaMin,
                PctNaMeta: semClassificacao ? null : Ponderada(_somaPctNaMeta, _pesoEspera));
        }

        private static double? Ponderada(double soma, int peso) =>
            peso > 0 ? Math.Round(soma / peso, 1) : null;
    }

    // ── Infra do ciclo ─────────────────────────────────────────────────────────

    private async Task<ResultadoConsulta> ConsultarAsync(string sql, CancellationToken ct)
    {
        var resultado = await _fonte!.ExecutarAsync(sql, ct);
        return resultado.Ok
            ? resultado
            : throw new InvalidOperationException(resultado.Erro ?? "Consulta Oracle falhou.");
    }

    private async Task<int> ConsultarEscalarAsync(string sql, CancellationToken ct)
    {
        var resultado = await ConsultarAsync(sql, ct);
        return ComoInt(resultado.Linhas[0][0]);
    }

    private static PainelSnapshot SnapshotVazio(DateTimeOffset carimbo) => new(
        GeradoEm: carimbo,
        Fonte: SnapshotStore.FontePadrao,
        Oracle: new OracleStatus(true, null, carimbo),
        Agora: null,
        Atendimentos: null,
        Internacoes: null,
        EsperaPorCor: null,
        Maternidade: null);

    private static string RotuloMes(DateTimeOffset data) =>
        PtBr is not null
            ? data.ToString("MMMM/yyyy", PtBr).ToLower(PtBr) // pt-BR já é minúsculo; ToLower garante
            : $"{MesesPtBr[data.Month - 1]}/{data.Year}";     // fallback sem libicu

    private static double? MediaDiaria(int total, int dias) =>
        dias > 0 ? Math.Round((double)total / dias, 1) : null;

    /// <summary>Normaliza DS_CLASSIFICACAO_RISCO p/ os valores do contrato ("SALUX" e afins somam em SEM_CLASSIFICACAO).</summary>
    private static string NormalizarCor(string? ds)
    {
        if (string.IsNullOrWhiteSpace(ds))
        {
            return "SEM_CLASSIFICACAO";
        }

        var normalizada = RemoverAcentos(ds.Trim()).ToUpperInvariant();
        return normalizada is "VERMELHO" or "AMARELO" or "VERDE" or "AZUL"
            ? normalizada
            : "SEM_CLASSIFICACAO";
    }

    private static string RemoverAcentos(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Mensagem curta (1ª linha, sem stack) p/ log e p/ oracle.ultimoErro.</summary>
    private static string ErroCurto(Exception ex)
    {
        var mensagem = ex.Message;
        var quebra = mensagem.IndexOfAny(['\r', '\n']);
        var linha = quebra >= 0 ? mensagem[..quebra] : mensagem;
        return linha.Length > 200 ? linha[..200] : linha;
    }

    // ── Conversões (NUMBER do Oracle chega como decimal) ───────────────────────

    private static int ComoInt(object? valor) =>
        valor is null ? 0 : Convert.ToInt32(valor, CultureInfo.InvariantCulture);

    private static int? ComoIntOuNulo(object? valor) =>
        valor is null ? null : Convert.ToInt32(valor, CultureInfo.InvariantCulture);

    private static double? ComoDoubleOuNulo(object? valor) =>
        valor is null ? null : Math.Round(Convert.ToDouble(valor, CultureInfo.InvariantCulture), 1);
}
