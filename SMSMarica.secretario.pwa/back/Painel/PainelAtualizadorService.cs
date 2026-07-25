using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using SMSMarica.Secretario.Api.Oracle;

namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Atualiza o snapshot do painel em dois ticks — rápido (o "agora") e lento (os
/// consolidados) — contra DUAS bases: o Salux/Oracle do Conde Modesto Leal e o HIS em
/// SQL Server da UPA 24h. SEMPRE sequencial, uma consulta por vez: as duas são produção
/// viva de unidade de saúde.
///
/// <para>
/// As bases são isoladas entre si. A UPA fora do ar não impede o Conde de atualizar (nem
/// o contrário): cada uma tem seu próprio estado de erro, o painel segue servindo o
/// último número bom de cada lado, e <c>fontes[]</c> no contrato diz quem está atrasado.
/// A aba "geral" é remontada a partir do que houver em memória das duas.
/// </para>
///
/// O primeiro ciclo (rápido + lento) roda já no startup.
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

    /// <summary>Código do HMCML dentro do Salux (a base atende mais de um hospital).</summary>
    private const int HospitalHmcml = 1;

    private readonly SnapshotStore _store;
    private readonly ProxySqlOpcoes _proxy;
    private readonly PainelOpcoes _painel;
    private readonly ILogger<PainelAtualizadorService> _logger;
    private readonly ProxySqlFonte? _fonte;

    // Seções vivas de cada unidade REAL. A aba "geral" não tem estado próprio: é
    // recalculada a cada republicação a partir daqui.
    private readonly EstadoUnidade _conde = new();
    private readonly EstadoUnidade _upa = new();
    private readonly EstadoUnidade _santaRita = new();

    public PainelAtualizadorService(
        SnapshotStore store,
        IHttpClientFactory httpFactory,
        IOptions<ProxySqlOpcoes> proxy,
        IOptions<PainelOpcoes> painel,
        ILogger<PainelAtualizadorService> logger)
    {
        _store = store;
        _proxy = proxy.Value;
        _painel = painel.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_proxy.Token))
        {
            _fonte = null;
            return;
        }

        var http = httpFactory.CreateClient("proxy-sql");
        http.BaseAddress = new Uri(_proxy.BaseUrl);
        // Timeout do HTTP acima do da consulta: quem deve estourar primeiro é o banco,
        // com mensagem de erro útil, não o cliente.
        http.Timeout = TimeSpan.FromSeconds(_painel.TimeoutConsultaSegundos + 30);
        _fonte = new ProxySqlFonte(http, _proxy.Token, _proxy.MaxLinhas);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Restart não recomeça do zero: as seções do snapshot persistido voltam a ser o
        // estado vivo de cada unidade até o primeiro ciclo bom substituí-las.
        SemearDoSnapshotPersistido();

        if (_fonte is null)
        {
            // Sem token do proxy: sobe mesmo assim e serve o snapshot persistido (ou 503).
            _logger.LogWarning(
                "Token do proxy SQL ausente (ProxySql:Token) — atualização desativada; servindo snapshot persistido, se houver.");
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

    private void SemearDoSnapshotPersistido()
    {
        var snapshot = _store.Atual;
        if (snapshot is null)
        {
            return;
        }

        foreach (var unidade in snapshot.Unidades)
        {
            var estado = EstadoDe(unidade.Id);

            if (estado is null)
            {
                continue;
            }

            // Só adota seção COMPLETA. O arquivo pode ter sido gravado por uma versão
            // anterior do contrato, e uma seção meio preenchida é pior que seção ausente:
            // ausente vira skeleton por um minuto, meio preenchida vira exceção.
            estado.Agora = unidade.Agora;
            estado.Atendimentos = unidade.Atendimentos;
            estado.Internacoes = unidade.Internacoes;
            estado.EsperaPorCor = EstaCompleta(unidade.EsperaPorCor) ? unidade.EsperaPorCor : null;
            estado.Maternidade = unidade.Maternidade;
            estado.Leitos = unidade.Leitos;
            estado.Diagnosticos = EstaCompleto(unidade.Diagnosticos) ? unidade.Diagnosticos : null;
        }

        foreach (var fonte in snapshot.Fontes ?? [])
        {
            var estado = EstadoDe(fonte.Id);
            if (estado is not null)
            {
                estado.UltimaAtualizacaoOk = fonte.Status.UltimaAtualizacaoOk;
            }
        }
    }

    private static bool EstaCompleta(EsperaPorCorSecao? secao) =>
        secao?.Periodos is { } p
        && p.Hoje is not null && p.Ontem is not null
        && p.MesAtual is not null && p.MesAnterior is not null;

    private static bool EstaCompleto(DiagnosticosSecao? secao) =>
        secao?.Periodos is { } p
        && p.Hoje is not null && p.Ontem is not null
        && p.MesAtual is not null && p.MesAnterior is not null;

    private EstadoUnidade? EstadoDe(string id) => id switch
    {
        Unidades.IdConde => _conde,
        Unidades.IdUpa => _upa,
        Unidades.IdSantaRita => _santaRita,
        _ => null,
    };

    // ── Ciclo rápido ───────────────────────────────────────────────────────────

    private async Task ExecutarCicloRapidoAsync(CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();

        await AtualizarAsync(_conde, "rápido", ct, async token =>
        {
            var q1a = await ConsultarAsync(Unidades.BaseConde, ConsultasPainel.Q1AguardandoPorCor(HospitalHmcml), token);
            var q1b = await ConsultarAsync(Unidades.BaseConde, ConsultasPainel.Q1EmAtendimento(HospitalHmcml), token);
            var q1c = await ConsultarAsync(Unidades.BaseConde, ConsultasPainel.Q1InternadosEHoje(HospitalHmcml), token);
            _conde.Agora = MontarAgoraConde(q1a, q1b, q1c);
        });

        foreach (var (estado, baseSlug, unidade) in Upas())
        {
            await AtualizarAsync(estado, "rápido", ct, async token =>
            {
                var u1a = await ConsultarAsync(baseSlug, ConsultasUpa.U1AguardandoPorCor(unidade), token);
                var u1b = await ConsultarAsync(baseSlug, ConsultasUpa.U1EmAtendimentoEHoje(unidade), token);
                estado.Agora = MontarAgoraUpa(u1a, u1b);
            });
        }

        Republicar();
        _logger.LogInformation("Ciclo rápido em {Ms} ms (conde: {Conde}, upa: {Upa}, santa rita: {SantaRita}).",
            cronometro.ElapsedMilliseconds, Situacao(_conde), Situacao(_upa), Situacao(_santaRita));
    }

    /// <summary>As UPAs, que compartilham consulta e diferem só em base e código de unidade.</summary>
    private (EstadoUnidade Estado, string Base, string Unidade)[] Upas() =>
    [
        (_upa, Unidades.BaseUpa, ConsultasUpa.UnidadeUpaMarica),
        (_santaRita, Unidades.BaseSantaRita, ConsultasUpa.UnidadeSantaRita),
    ];

    // ── Ciclo lento ────────────────────────────────────────────────────────────

    private async Task ExecutarCicloLentoAsync(CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();

        await AtualizarAsync(_conde, "lento", ct, token => ExecutarLentoCondeAsync(token));
        foreach (var (estado, baseSlug, unidade) in Upas())
        {
            await AtualizarAsync(estado, "lento", ct, token => ExecutarLentoUpaAsync(estado, baseSlug, unidade, token));
        }

        Republicar();

        // Persiste após o ciclo lento — restart volta com o painel completo. Grava mesmo
        // se uma das bases falhou: o que estiver bom vale mais que nada.
        await _store.PersistirAsync(ct);

        _logger.LogInformation("Ciclo lento em {Ms} ms (conde: {Conde}, upa: {Upa}, santa rita: {SantaRita}).",
            cronometro.ElapsedMilliseconds, Situacao(_conde), Situacao(_upa), Situacao(_santaRita));
    }

    private async Task ExecutarLentoCondeAsync(CancellationToken ct)
    {
        const int hosp = HospitalHmcml;
        var hoje = FusoBrasilia.Agora();
        var b = Unidades.BaseConde;

        // Q2 — atendimentos por período (+ total só de dias completos do mês atual).
        var totalMesAnterior = await ConsultarEscalarAsync(b,
            ConsultasPainel.Q2AtendimentosPeriodo(hosp, ConsultasPainel.IniMesAnterior, ConsultasPainel.FimMesAnterior), ct);
        var totalMesAtual = await ConsultarEscalarAsync(b,
            ConsultasPainel.Q2AtendimentosPeriodo(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual), ct);
        var totalHoje = await ConsultarEscalarAsync(b,
            ConsultasPainel.Q2AtendimentosPeriodo(hosp, ConsultasPainel.IniHoje, ConsultasPainel.FimHoje), ct);
        var totalDiasCompletos = await ConsultarEscalarAsync(b,
            ConsultasPainel.Q2AtendimentosPeriodo(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimDiasCompletos), ct);

        // Q3/Q4 — séries de atendimento.
        var serieAtendimentos = await ConsultarAsync(b, ConsultasPainel.Q3SerieDiariaAtendimentos(hosp), ct);
        var porHora = await ConsultarAsync(b, ConsultasPainel.Q4PorHoraHoje(hosp), ct);

        // Q5 — internações por período + série (+ dias completos p/ média do mês atual).
        var intMesAnterior = await ConsultarAsync(b,
            ConsultasPainel.Q5InternacoesPeriodo(hosp, ConsultasPainel.IniMesAnterior, ConsultasPainel.FimMesAnterior), ct);
        var intMesAtual = await ConsultarAsync(b,
            ConsultasPainel.Q5InternacoesPeriodo(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual), ct);
        var intHoje = await ConsultarAsync(b,
            ConsultasPainel.Q5InternacoesPeriodo(hosp, ConsultasPainel.IniHoje, ConsultasPainel.FimHoje), ct);
        var intDiasCompletos = await ConsultarAsync(b,
            ConsultasPainel.Q5InternacoesPeriodo(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimDiasCompletos), ct);
        var serieInternacoes = await ConsultarAsync(b, ConsultasPainel.Q5SerieDiariaInternacoes(hosp), ct);

        // Q7 — maternidade (NASCIMENTO não tem cd_hospital próprio: o livro de partos
        // é do HMCML, única maternidade da rede na base).
        var matMesAnterior = await ConsultarAsync(b,
            ConsultasPainel.Q7Maternidade(ConsultasPainel.IniMesAnterior, ConsultasPainel.FimMesAnterior), ct);
        var matMesAtual = await ConsultarAsync(b,
            ConsultasPainel.Q7Maternidade(ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual), ct);
        var matHoje = await ConsultarAsync(b,
            ConsultasPainel.Q7Maternidade(ConsultasPainel.IniHoje, ConsultasPainel.FimHoje), ct);
        var matDiasCompletos = await ConsultarAsync(b,
            ConsultasPainel.Q7Maternidade(ConsultasPainel.IniMesAtual, ConsultasPainel.FimDiasCompletos), ct);
        var seriePartos = await ConsultarAsync(b, ConsultasPainel.Q7SerieDiariaPartos(), ct);

        // Q6 — espera por cor, 1× por período (a PESADA fica por último).
        var esperaHoje = await ConsultarAsync(b,
            ConsultasPainel.Q6EsperaPorCor(hosp, ConsultasPainel.IniHoje, ConsultasPainel.FimHoje, "SYSDATE + 3"), ct);
        var esperaOntem = await ConsultarAsync(b,
            ConsultasPainel.Q6EsperaPorCor(hosp, ConsultasPainel.IniOntem, ConsultasPainel.FimOntem, "TRUNC(SYSDATE) + 3"), ct);
        var esperaMesAtual = await ConsultarAsync(b,
            ConsultasPainel.Q6EsperaPorCor(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual, "SYSDATE + 3"), ct);
        var esperaMesAnterior = await ConsultarAsync(b,
            ConsultasPainel.Q6EsperaPorCor(
                hosp, ConsultasPainel.IniMesAnterior, ConsultasPainel.FimMesAnterior, "TRUNC(SYSDATE,'MM') + 3"), ct);

        // L1..L3 — leitos, perfil dos internados e permanência das altas do mês.
        var setores = await ConsultarAsync(b, ConsultasPainel.L1OcupacaoPorSetor(hosp), ct);
        var perfil = await ConsultarAsync(b, ConsultasPainel.L2PerfilInternados(hosp), ct);
        var permanencia = await ConsultarAsync(b,
            ConsultasPainel.L3Permanencia(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual), ct);

        // Q8 — diagnósticos nos quatro períodos do seletor.
        var cidsHoje = await ConsultarAsync(b,
            ConsultasPainel.Q8CidPorCor(hosp, ConsultasPainel.IniHoje, ConsultasPainel.FimHoje, TopCids), ct);
        var cidsOntem = await ConsultarAsync(b,
            ConsultasPainel.Q8CidPorCor(hosp, ConsultasPainel.IniOntem, ConsultasPainel.FimOntem, TopCids), ct);
        var cidsMesAtual = await ConsultarAsync(b,
            ConsultasPainel.Q8CidPorCor(hosp, ConsultasPainel.IniMesAtual, ConsultasPainel.FimMesAtual, TopCids), ct);
        var cidsMesAnterior = await ConsultarAsync(b,
            ConsultasPainel.Q8CidPorCor(hosp, ConsultasPainel.IniMesAnterior, ConsultasPainel.FimMesAnterior, TopCids), ct);

        var carimbo = FusoBrasilia.Agora();
        _conde.Leitos = MontarLeitosConde(hoje, carimbo, setores, perfil, permanencia);
        _conde.Diagnosticos = new DiagnosticosSecao(carimbo, new DiagnosticosPeriodos(
            LerDiagnosticos(cidsHoje), LerDiagnosticos(cidsOntem),
            LerDiagnosticos(cidsMesAtual), LerDiagnosticos(cidsMesAnterior)), null);
        _conde.Atendimentos = MontarAtendimentos(
            hoje, carimbo, totalMesAnterior, totalMesAtual, totalHoje, totalDiasCompletos, serieAtendimentos, porHora);
        _conde.Internacoes = MontarInternacoes(
            hoje, carimbo, intMesAnterior, intMesAtual, intHoje, intDiasCompletos, serieInternacoes);
        _conde.Maternidade = MontarMaternidade(
            hoje, carimbo, matMesAnterior, matMesAtual, matHoje, matDiasCompletos, seriePartos);
        _conde.EsperaPorCor = new EsperaPorCorSecao(carimbo, new EsperaPeriodos(
            MontarEsperaPeriodo(esperaHoje, Unidades.IdConde),
            MontarEsperaPeriodo(esperaOntem, Unidades.IdConde),
            MontarEsperaPeriodo(esperaMesAtual, Unidades.IdConde),
            MontarEsperaPeriodo(esperaMesAnterior, Unidades.IdConde)));
    }

    /// <summary>
    /// Ciclo lento de UMA das UPAs. As duas rodam o mesmo HIS em instâncias separadas, então
    /// só mudam o slug da base e o <c>unid_codigo</c>.
    /// </summary>
    private async Task ExecutarLentoUpaAsync(
        EstadoUnidade estado, string b, string unidade, CancellationToken ct)
    {
        var hoje = FusoBrasilia.Agora();

        var totalMesAnterior = await ConsultarEscalarAsync(b,
            ConsultasUpa.U2AtendimentosPeriodo(unidade, ConsultasUpa.IniMesAnterior, ConsultasUpa.FimMesAnterior), ct);
        var totalMesAtual = await ConsultarEscalarAsync(b,
            ConsultasUpa.U2AtendimentosPeriodo(unidade, ConsultasUpa.IniMesAtual, ConsultasUpa.FimMesAtual), ct);
        var totalHoje = await ConsultarEscalarAsync(b,
            ConsultasUpa.U2AtendimentosPeriodo(unidade, ConsultasUpa.IniHoje, ConsultasUpa.FimHoje), ct);
        var totalDiasCompletos = await ConsultarEscalarAsync(b,
            ConsultasUpa.U2AtendimentosPeriodo(unidade, ConsultasUpa.IniMesAtual, ConsultasUpa.FimDiasCompletos), ct);

        var serieAtendimentos = await ConsultarAsync(b, ConsultasUpa.U3SerieDiariaAtendimentos(unidade), ct);
        var porHora = await ConsultarAsync(b, ConsultasUpa.U4PorHoraHoje(unidade), ct);

        var esperaHoje = await ConsultarAsync(b,
            ConsultasUpa.U6EsperaPorCor(unidade, ConsultasUpa.IniHoje, ConsultasUpa.FimHoje, "DATEADD(day,3,GETDATE())"), ct);
        var esperaOntem = await ConsultarAsync(b,
            ConsultasUpa.U6EsperaPorCor(unidade, ConsultasUpa.IniOntem, ConsultasUpa.FimOntem, "DATEADD(day,3,GETDATE())"), ct);
        var esperaMesAtual = await ConsultarAsync(b,
            ConsultasUpa.U6EsperaPorCor(unidade, ConsultasUpa.IniMesAtual, ConsultasUpa.FimMesAtual, "DATEADD(day,3,GETDATE())"), ct);
        var esperaMesAnterior = await ConsultarAsync(b,
            ConsultasUpa.U6EsperaPorCor(
                unidade, ConsultasUpa.IniMesAnterior, ConsultasUpa.FimMesAnterior,
                $"DATEADD(day,3,{ConsultasUpa.FimMesAnterior})"), ct);

        // L4/L5 — leitos de observação e quantos foram encaminhados a eles no mês.
        var leitos = await ConsultarAsync(b, ConsultasUpa.L4LeitosObservacao(), ct);
        var fluxo = await ConsultarAsync(b,
            ConsultasUpa.L5FluxoObservacao(unidade, ConsultasUpa.IniMesAtual, ConsultasUpa.FimMesAtual), ct);

        var carimbo = FusoBrasilia.Agora();
        estado.Leitos = MontarLeitosUpa(hoje, carimbo, leitos, fluxo);
        estado.Atendimentos = MontarAtendimentos(
            hoje, carimbo, totalMesAnterior, totalMesAtual, totalHoje, totalDiasCompletos, serieAtendimentos, porHora);
        estado.EsperaPorCor = new EsperaPorCorSecao(carimbo, new EsperaPeriodos(
            MontarEsperaPeriodo(esperaHoje, Unidades.IdUpa),
            MontarEsperaPeriodo(esperaOntem, Unidades.IdUpa),
            MontarEsperaPeriodo(esperaMesAtual, Unidades.IdUpa),
            MontarEsperaPeriodo(esperaMesAnterior, Unidades.IdUpa)));

        // Internações e maternidade continuam nulas de propósito — ver ConsultasUpa.
    }

    /// <summary>
    /// Roda um trecho do ciclo para UMA unidade, isolando a falha: erro aqui marca só o
    /// estado daquela base e deixa o resto do painel seguir com o último número bom.
    /// </summary>
    private async Task AtualizarAsync(
        EstadoUnidade estado, string tipoCiclo, CancellationToken ct, Func<CancellationToken, Task> corpo)
    {
        try
        {
            await corpo(ct);
            estado.LimparErro(tipoCiclo);
            estado.UltimaAtualizacaoOk = FusoBrasilia.Agora();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var erro = ErroCurto(ex);
            estado.RegistrarErro(tipoCiclo, erro);
            _logger.LogError("Ciclo {Tipo} FALHOU: {Erro}", tipoCiclo, erro);
        }
    }

    private static string Situacao(EstadoUnidade estado) => estado.ErroVigente is { } erro ? $"ERRO — {erro}" : "OK";

    // ── Publicação do snapshot ─────────────────────────────────────────────────

    /// <summary>
    /// Remonta o snapshot inteiro (geral + unidades) a partir do estado vivo.
    ///
    /// <para>
    /// <b>Falha aqui não pode derrubar o serviço.</b> O host roda com
    /// <c>BackgroundServiceExceptionBehavior.StopHost</c>, então uma exceção nesta
    /// montagem mata o painel inteiro — foi o que aconteceu em 25/07/2026, quando o
    /// snapshot persistido de um contrato anterior trouxe uma lista de período nula e a
    /// soma da rede estourou no PRIMEIRO ciclo rápido, antes de o ciclo lento ter chance
    /// de substituí-la. Servir o snapshot anterior por mais um minuto é sempre melhor
    /// que devolver 502.
    /// </para>
    /// </summary>
    private void Republicar()
    {
        try
        {
            RepublicarInterno();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao montar o snapshot — mantendo o anterior.");
        }
    }

    private void RepublicarInterno()
    {
        var carimbo = FusoBrasilia.Agora();

        var conde = new UnidadePainel(
            Unidades.IdConde, "Conde", Unidades.NomeConde, Unidades.FonteConde,
            Unidades.CoresDe(Unidades.IdConde),
            _conde.Agora, _conde.Atendimentos, _conde.Internacoes, _conde.EsperaPorCor, _conde.Maternidade,
            _conde.Leitos, _conde.Diagnosticos);

        // As UPAs não internam nem têm maternidade: passam null de propósito.
        var upa = new UnidadePainel(
            Unidades.IdUpa, "UPA", Unidades.NomeUpa, Unidades.FonteUpa,
            Unidades.CoresDe(Unidades.IdUpa),
            _upa.Agora, _upa.Atendimentos, null, _upa.EsperaPorCor, null, _upa.Leitos, null);

        var santaRita = new UnidadePainel(
            Unidades.IdSantaRita, "Sta. Rita", Unidades.NomeSantaRita, Unidades.FonteSantaRita,
            Unidades.CoresDe(Unidades.IdSantaRita),
            _santaRita.Agora, _santaRita.Atendimentos, null, _santaRita.EsperaPorCor, null, _santaRita.Leitos, null);

        UnidadePainel[] reais = [conde, upa, santaRita];
        var geral = MontarGeral(reais);

        var fontes = new[]
        {
            new FonteInfo(Unidades.IdConde, Unidades.NomeConde, _conde.Status()),
            new FonteInfo(Unidades.IdUpa, Unidades.NomeUpa, _upa.Status()),
            new FonteInfo(Unidades.IdSantaRita, Unidades.NomeSantaRita, _santaRita.Status()),
        };

        EstadoUnidade[] estados = [_conde, _upa, _santaRita];

        // Consolidado: OK só com TODAS as bases OK; a "última atualização boa" da rede é a
        // MAIS ANTIGA delas — é ela que diz quão velho é o número mais velho da tela.
        var status = new StatusFonte(
            Ok: estados.All(e => e.ErroVigente is null),
            UltimoErro: estados.Select(e => e.ErroVigente).FirstOrDefault(e => e is not null),
            UltimaAtualizacaoOk: estados
                .Select(e => e.UltimaAtualizacaoOk)
                .Aggregate((DateTimeOffset?)null, MenorData));

        _store.Definir(new PainelSnapshot(carimbo, status, fontes, [geral, .. reais]));
    }

    private static DateTimeOffset? MenorData(DateTimeOffset? a, DateTimeOffset? b) =>
        a is null ? b : b is null ? a : a < b ? a : b;

    /// <summary>
    /// A aba "geral": soma o que é somável (fila, atendimentos) e repassa com etiqueta de
    /// escopo o que só existe no Conde (internações, maternidade). Um número de rede que
    /// na verdade é de uma unidade só precisa dizer isso na cara do usuário.
    /// </summary>
    private static UnidadePainel MontarGeral(IReadOnlyList<UnidadePainel> reais)
    {
        var conde = reais.First(u => u.Id == Unidades.IdConde);

        return new UnidadePainel(
            Unidades.IdGeral, "Geral", "Rede municipal de urgência", Unidades.FonteGeral,
            Unidades.CoresDe(Unidades.IdGeral),
            Agora: SomarAgora([.. reais.Select(u => u.Agora).OfType<AgoraSecao>()]),
            Atendimentos: SomarAtendimentos([.. reais.Select(u => u.Atendimentos).OfType<AtendimentosSecao>()]),
            Internacoes: conde.Internacoes is { } i ? i with { Escopo = Unidades.SiglaConde } : null,
            EsperaPorCor: SomarEspera([.. reais.Select(u => u.EsperaPorCor).OfType<EsperaPorCorSecao>()]),
            Maternidade: conde.Maternidade is { } m ? m with { Escopo = Unidades.SiglaConde } : null,
            Leitos: SomarLeitos(reais),
            // Diagnóstico por cor só existe no Conde: na rede ele vem etiquetado, não somado.
            Diagnosticos: conde.Diagnosticos is { } d ? d with { Escopo = Unidades.SiglaConde } : null);
    }

    /// <summary>
    /// O frescor da rede é o do dado mais VELHO — arredondar para o mais novo esconderia
    /// uma base parada atrás de outra que está atualizando.
    /// </summary>
    private static DateTimeOffset MaisAntigo<T>(IReadOnlyList<T> secoes, Func<T, DateTimeOffset> carimbo) =>
        secoes.Min(carimbo);

    private static AgoraSecao? SomarAgora(IReadOnlyList<AgoraSecao> secoes)
    {
        if (secoes.Count == 0)
        {
            return null;
        }

        var porCor = new Dictionary<string, (int Qtd, double SomaMinutos, int PesoMinutos)>();
        foreach (var item in secoes.SelectMany(s => s.AguardandoPorCor))
        {
            var atual = porCor.GetValueOrDefault(item.Cor);
            atual.Qtd += item.Qtd;
            if (item.MinMedioEspera is { } minutos && item.Qtd > 0)
            {
                atual.SomaMinutos += (double)minutos * item.Qtd;
                atual.PesoMinutos += item.Qtd;
            }

            porCor[item.Cor] = atual;
        }

        var aguardando = Unidades.Cores
            .Select(cor =>
            {
                var (qtd, soma, peso) = porCor.GetValueOrDefault(cor);
                return new CorAguardando(cor, qtd, peso > 0 ? (int)Math.Round(soma / peso) : null);
            })
            .ToList();

        // Só o Conde interna, então o bloco é único — mas vem etiquetado, para o número
        // não ser lido como se fosse da rede toda.
        var internados = secoes.Select(s => s.Internados).OfType<InternadosAgora>().FirstOrDefault();

        return new AgoraSecao(
            AtualizadoEm: MaisAntigo(secoes, s => s.AtualizadoEm),
            AguardandoMedico: secoes.Sum(s => s.AguardandoMedico),
            AguardandoPorCor: aguardando,
            EmAtendimento: secoes.Sum(s => s.EmAtendimento),
            AtendimentosHoje: secoes.Sum(s => s.AtendimentosHoje),
            Internados: internados is null ? null : internados with { Escopo = Unidades.SiglaConde });
    }

    private static AtendimentosSecao? SomarAtendimentos(IReadOnlyList<AtendimentosSecao> secoes)
    {
        if (secoes.Count == 0)
        {
            return null;
        }

        // Rótulo de mês, dias do mês e dias completos são os mesmos em todas as unidades
        // (o calendário não muda de prédio); a primeira serve de referência.
        var referencia = secoes[0];
        var totalMesAnterior = secoes.Sum(s => s.MesAnterior.Total);
        var totalDiasCompletos = secoes.Sum(s => s.MesAtual.TotalDiasCompletos);

        return new AtendimentosSecao(
            AtualizadoEm: MaisAntigo(secoes, s => s.AtualizadoEm),
            MesAnterior: new AtendimentosMesAnterior(
                referencia.MesAnterior.Rotulo,
                totalMesAnterior,
                referencia.MesAnterior.Dias,
                MediaDiaria(totalMesAnterior, referencia.MesAnterior.Dias)),
            MesAtual: new AtendimentosMesAtual(
                referencia.MesAtual.Rotulo,
                secoes.Sum(s => s.MesAtual.Total),
                referencia.MesAtual.DiasCompletos,
                totalDiasCompletos,
                MediaDiaria(totalDiasCompletos, referencia.MesAtual.DiasCompletos)),
            Hoje: new TotalSimples(secoes.Sum(s => s.Hoje.Total)),
            SerieDiaria: SomarSerie(secoes.SelectMany(s => s.SerieDiaria)),
            PorHoraHoje: SomarPorHora(secoes.SelectMany(s => s.PorHoraHoje)));
    }

    private static List<DiaQtd> SomarSerie(IEnumerable<DiaQtd> pontos)
    {
        var porDia = new Dictionary<string, int>();
        foreach (var ponto in pontos)
        {
            porDia[ponto.Dia] = porDia.GetValueOrDefault(ponto.Dia) + ponto.Qtd;
        }

        return [.. porDia.OrderBy(par => par.Key, StringComparer.Ordinal).Select(par => new DiaQtd(par.Key, par.Value))];
    }

    private static List<HoraQtd> SomarPorHora(IEnumerable<HoraQtd> pontos)
    {
        var porHora = new Dictionary<int, int>();
        foreach (var ponto in pontos)
        {
            porHora[ponto.Hora] = porHora.GetValueOrDefault(ponto.Hora) + ponto.Qtd;
        }

        return [.. porHora.OrderBy(par => par.Key).Select(par => new HoraQtd(par.Key, par.Value))];
    }

    private static EsperaPorCorSecao? SomarEspera(IReadOnlyList<EsperaPorCorSecao> secoes)
    {
        if (secoes.Count == 0)
        {
            return null;
        }

        return new EsperaPorCorSecao(
            MaisAntigo(secoes, s => s.AtualizadoEm),
            // `?? []` porque um período pode chegar nulo de um snapshot gravado antes de
            // ele existir no contrato; sem isso a soma da rede estoura (ver Republicar).
            new EsperaPeriodos(
                SomarEsperaPeriodo(secoes.Select(s => s.Periodos?.Hoje ?? [])),
                SomarEsperaPeriodo(secoes.Select(s => s.Periodos?.Ontem ?? [])),
                SomarEsperaPeriodo(secoes.Select(s => s.Periodos?.MesAtual ?? [])),
                SomarEsperaPeriodo(secoes.Select(s => s.Periodos?.MesAnterior ?? []))));
    }

    /// <summary>
    /// Mescla as pulseiras das unidades por média ponderada, para a aba "geral".
    ///
    /// <para>
    /// <b>Meta é assunto da unidade, e só aparece no contexto dela.</b> O Manchester do
    /// Salux e os cadastros das UPAs usam alvos diferentes para a mesma cor — Amarelo é
    /// 30 min no Conde, 60 na UPA Maricá e 30 em Santa Rita; Verde é 60, 120 e 60. Não
    /// existe meta da rede, e um "% na meta" consolidado seria a média de cumprimentos de
    /// réguas diferentes, um número sem significado clínico. Por isso <c>MetaMin</c> e
    /// <c>PctNaMeta</c> vêm nulos aqui: o consolidado mostra volume e tempo (média,
    /// mediana, p90), e quem quiser tempo-contra-meta abre a aba da unidade.
    /// </para>
    /// </summary>
    private static List<EsperaCor> SomarEsperaPeriodo(IEnumerable<IReadOnlyList<EsperaCor>> porUnidade)
    {
        var buckets = new Dictionary<string, EsperaAcumulador>();
        foreach (var item in porUnidade.SelectMany(lista => lista))
        {
            var bucket = buckets.TryGetValue(item.Cor, out var existente) ? existente : new EsperaAcumulador();
            bucket.Somar(item);
            buckets[item.Cor] = bucket;
        }

        return
        [
            .. Unidades.Cores.Select(cor => (buckets.TryGetValue(cor, out var bucket)
                ? bucket.Materializar(cor)
                : EsperaCorVazia(cor)) with { MetaMin = null, PctNaMeta = null }),
        ];
    }

    // ── Montagem das seções (Conde) ────────────────────────────────────────────

    private static AgoraSecao MontarAgoraConde(ResultadoConsulta q1a, ResultadoConsulta q1b, ResultadoConsulta q1c)
    {
        var aguardandoPorCor = MontarAguardandoPorCor(q1a, Unidades.IdConde);

        var emAtendimento = ComoInt(q1b.Linhas[0][0]);

        // Q1c: total, maternidade, ate17, adultos, mediaDias, atendHoje, internHoje
        var linhaC = q1c.Linhas[0];

        return new AgoraSecao(
            AtualizadoEm: FusoBrasilia.Agora(),
            AguardandoMedico: aguardandoPorCor.Sum(c => c.Qtd),
            AguardandoPorCor: aguardandoPorCor,
            EmAtendimento: emAtendimento,
            AtendimentosHoje: ComoInt(linhaC[5]),
            Internados: new InternadosAgora(
                Total: ComoInt(linhaC[0]),
                Maternidade: ComoInt(linhaC[1]),
                Ate17: ComoInt(linhaC[2]),
                Adultos: ComoInt(linhaC[3]),
                MediaDiasInternacao: ComoDoubleOuNulo(linhaC[4]),
                InternacoesHoje: ComoInt(linhaC[6]),
                Escopo: null));
    }

    // ── Montagem das seções (UPA) ──────────────────────────────────────────────

    private static AgoraSecao MontarAgoraUpa(ResultadoConsulta u1a, ResultadoConsulta u1b)
    {
        var aguardandoPorCor = MontarAguardandoPorCor(u1a, Unidades.IdUpa);
        var linha = u1b.Linhas[0];

        return new AgoraSecao(
            AtualizadoEm: FusoBrasilia.Agora(),
            AguardandoMedico: aguardandoPorCor.Sum(c => c.Qtd),
            AguardandoPorCor: aguardandoPorCor,
            EmAtendimento: ComoInt(linha[0]),
            AtendimentosHoje: ComoInt(linha[1]),
            // A UPA não interna — ver a armadilha 3 em ConsultasUpa. Nulo, não zero.
            Internados: null);
    }

    /// <summary>
    /// Fila por cor, normalizada e SEMPRE completa (uma entrada por cor do contrato, na
    /// ordem clínica), venha de qual base vier.
    /// </summary>
    private static List<CorAguardando> MontarAguardandoPorCor(ResultadoConsulta resultado, string unidadeId)
    {
        var qtdPorCor = new Dictionary<string, int>();
        var somaMinutos = new Dictionary<string, double>(); // ponderada por qtd, p/ média mesclada
        foreach (var linha in resultado.Linhas)
        {
            var cor = NormalizarCor(linha[0] as string, unidadeId);
            var qtd = ComoInt(linha[1]);
            var minMedio = ComoDoubleOuNulo(linha[2]);
            qtdPorCor[cor] = qtdPorCor.GetValueOrDefault(cor) + qtd;
            if (minMedio is not null)
            {
                somaMinutos[cor] = somaMinutos.GetValueOrDefault(cor) + minMedio.Value * qtd;
            }
        }

        var lista = new List<CorAguardando>(Unidades.Cores.Length);
        foreach (var cor in Unidades.Cores)
        {
            var qtd = qtdPorCor.GetValueOrDefault(cor);
            int? minMedio = qtd > 0 && somaMinutos.TryGetValue(cor, out var soma)
                ? (int)Math.Round(soma / qtd)
                : null;
            lista.Add(new CorAguardando(cor, qtd, minMedio));
        }

        return lista;
    }

    /// <summary>Quantos CIDs por cor — cinco é o que cabe numa olhada sem virar tabela.</summary>
    private const int TopCids = 5;

    /// <summary>
    /// Uma rodada da Q8 vira a lista por cor, na ordem clínica. Cores sem CID no período
    /// simplesmente não aparecem — lista vazia é melhor que cor vazia na tela.
    /// </summary>
    private static List<DiagnosticosDaCor> LerDiagnosticos(ResultadoConsulta resultado)
    {
        var porCor = new Dictionary<string, (int Total, List<CidRanking> Cids)>();
        // O total vem repetido em toda linha da mesma cor CRUA, então só pode ser somado
        // uma vez por cor crua — e a normalização funde cores (SALUX → SEM_CLASSIFICACAO),
        // caso em que os dois totais precisam somar.
        var totaisContados = new HashSet<string>();

        foreach (var linha in resultado.Linhas)
        {
            var corCrua = (linha[0] as string)?.Trim() ?? "";
            var cor = NormalizarCor(corCrua, Unidades.IdConde);
            var qtd = ComoInt(linha[3]);
            var totalCor = ComoInt(linha[4]);

            if (!porCor.TryGetValue(cor, out var atual))
            {
                atual = (0, []);
            }

            if (totaisContados.Add(corCrua))
            {
                atual.Total += totalCor;
            }

            atual.Cids.Add(new CidRanking(
                Codigo: (linha[1] as string)?.Trim() ?? "",
                Descricao: NomeCid(linha[2] as string),
                Qtd: qtd,
                Pct: totalCor > 0 ? Math.Round(100.0 * qtd / totalCor, 1) : null));
            porCor[cor] = atual;
        }

        return
        [
            .. Unidades.Cores
                .Where(porCor.ContainsKey)
                .Select(cor => new DiagnosticosDaCor(
                    cor,
                    porCor[cor].Total,
                    // A normalização pode fundir cores (SALUX → SEM_CLASSIFICACAO); reordena
                    // e corta de novo para o topo continuar sendo o topo de verdade.
                    [.. porCor[cor].Cids.OrderByDescending(c => c.Qtd).Take(TopCids)])),
        ];
    }

    /// <summary>Descrição do CID em Caixa de Título — o cadastro grava em CAIXA ALTA.</summary>
    private static string NomeCid(string? bruto)
    {
        var texto = bruto?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            return "Sem descrição";
        }

        var cultura = PtBr ?? CultureInfo.InvariantCulture;
        return cultura.TextInfo.ToTitleCase(texto.ToLower(cultura));
    }

    // ── Leitos (L1..L5) ────────────────────────────────────────────────────────

    private static LeitosSecao MontarLeitosConde(
        DateTimeOffset hoje, DateTimeOffset carimbo,
        ResultadoConsulta setoresBrutos, ResultadoConsulta perfilBruto, ResultadoConsulta permanenciaBruta)
    {
        var setores = new List<SetorOcupacao>(setoresBrutos.Linhas.Count);
        foreach (var linha in setoresBrutos.Linhas)
        {
            var capacidade = ComoInt(linha[1]);
            var ocupados = ComoInt(linha[3]);
            setores.Add(new SetorOcupacao(
                Setor: NomeSetor(linha[0] as string),
                Leitos: capacidade,
                Ocupados: ocupados,
                Bloqueados: ComoInt(linha[2]),
                EmLeitoExtra: ComoInt(linha[4]),
                Extras: ComoInt(linha[5]),
                Virtuais: ComoInt(linha[6]),
                Desativados: ComoInt(linha[7]),
                // Denominador já é a capacidade (a L1 tira o bloqueado de dentro dela),
                // e o numerador são TODOS os internados do setor — por isso passa de 100%
                // num setor lotado, que é exatamente o que se quer enxergar.
                Taxa: TaxaOcupacao(ocupados, capacidade)));
        }

        var p = perfilBruto.Linhas[0];
        var perfil = new PerfilInternados(
            Total: ComoInt(p[0]), Homens: ComoInt(p[1]), Mulheres: ComoInt(p[2]), SemSexo: ComoInt(p[3]),
            Ate17: ComoInt(p[4]), Adultos: ComoInt(p[5]), Idosos: ComoInt(p[6]),
            IdadeMedia: ComoDoubleOuNulo(p[7]), DiasMedios: ComoDoubleOuNulo(p[8]));

        return new LeitosSecao(
            AtualizadoEm: carimbo,
            // A soma leva TODOS os setores — inclusive os que só têm leito extra ou
            // desativado, senão o rodapé "98 extras" mentiria por baixo. A lista exibida
            // leva só quem tem capacidade ou paciente: setor sem os dois não é linha de
            // ocupação, é ruído de cadastro.
            Ocupacao: SomarSetores(setores),
            Setores: setores.Where(s => s.Leitos > 0 || s.Ocupados > 0).ToList(),
            Perfil: perfil,
            Permanencia: MontarPermanencia(permanenciaBruta, RotuloMes(hoje)),
            Observacao: null,
            Escopo: null,
            Indisponivel: null);
    }

    private static LeitosSecao MontarLeitosUpa(
        DateTimeOffset hoje, DateTimeOffset carimbo, ResultadoConsulta leitosBrutos, ResultadoConsulta fluxoBruto)
    {
        var setores = new List<SetorOcupacao>(leitosBrutos.Linhas.Count);
        foreach (var linha in leitosBrutos.Linhas)
        {
            var leitos = ComoInt(linha[1]);
            var ocupados = ComoInt(linha[2]);
            // Não existe status de bloqueio nestas bases — só livre e ocupado. Nem a
            // qualificação de tipo de leito (extra/virtual/desativado) que o Salux tem:
            // aqui todo leito cadastrado é leito de observação em operação.
            setores.Add(new SetorOcupacao(
                NomeSetor(linha[0] as string), leitos, ocupados,
                Bloqueados: 0, EmLeitoExtra: 0, Extras: 0, Virtuais: 0, Desativados: 0,
                Taxa: TaxaOcupacao(ocupados, leitos)));
        }

        var f = fluxoBruto.Linhas[0];
        var observacao = new ObservacaoFluxo(RotuloMes(hoje), ComoInt(f[1]), ComoInt(f[0]));

        var ocupacao = SomarSetores(setores);
        var cadastroRaso = ocupacao is null || ocupacao.Leitos < ConsultasUpa.MinimoLeitosCadastrados;

        return new LeitosSecao(
            AtualizadoEm: carimbo,
            // Cadastro raso não vira "0 de 2": vira ausência declarada de dado.
            Ocupacao: cadastroRaso ? null : ocupacao,
            Setores: cadastroRaso ? [] : setores,
            Perfil: null,
            Permanencia: null,
            Observacao: observacao,
            Escopo: null,
            Indisponivel: cadastroRaso
                ? "O cadastro de leitos desta unidade está vazio no sistema dela, então não há taxa de ocupação confiável para publicar."
                : null);
    }

    private static Permanencia? MontarPermanencia(ResultadoConsulta resultado, string rotulo)
    {
        // A L3 devolve uma linha por segmento, com TOTAL primeiro.
        var porSegmento = new Dictionary<string, (int Altas, double? Media, double? Mediana, double? P90)>();
        foreach (var linha in resultado.Linhas)
        {
            var nome = (linha[0] as string)?.Trim().ToUpperInvariant() ?? "";
            porSegmento[nome] = (ComoInt(linha[1]), ComoDoubleOuNulo(linha[2]),
                ComoDoubleOuNulo(linha[3]), ComoDoubleOuNulo(linha[4]));
        }

        if (!porSegmento.TryGetValue("TOTAL", out var total))
        {
            return null;
        }

        string[] ordem = ["HOMENS", "MULHERES", "ATE17", "ADULTOS", "IDOSOS"];
        var segmentos = ordem
            .Where(porSegmento.ContainsKey)
            .Select(nome => new PermanenciaSegmento(nome, porSegmento[nome].Altas, porSegmento[nome].Media))
            .ToList();

        return new Permanencia(rotulo, total.Altas, total.Media, total.Mediana, total.P90, segmentos);
    }

    /// <summary>
    /// Consolida setores num total. Capacidade já vem sem bloqueado nem extra/virtual/
    /// desativado — o que a L1 qualificou por setor só é somado aqui, nunca recalculado.
    /// </summary>
    private static Ocupacao? SomarSetores(IReadOnlyList<SetorOcupacao> setores)
    {
        if (setores.Count == 0)
        {
            return null;
        }

        var capacidade = setores.Sum(s => s.Leitos);
        var ocupados = setores.Sum(s => s.Ocupados);

        return new Ocupacao(
            Leitos: capacidade,
            Ocupados: ocupados,
            // Folga e estouro se compensariam se medidos no total (149 contra 211 daria
            // zero estouro, escondendo a Saúde Mental a 150%). Some-se o que cada setor
            // tem de folga e o que cada um tem de estouro, separadamente — vaga na
            // Pediatria não alivia a Maternidade.
            Livres: setores.Sum(s => s.Livres),
            Bloqueados: setores.Sum(s => s.Bloqueados),
            Excedente: setores.Sum(s => s.Excedente),
            EmLeitoExtra: setores.Sum(s => s.EmLeitoExtra),
            Extras: setores.Sum(s => s.Extras),
            Virtuais: setores.Sum(s => s.Virtuais),
            Desativados: setores.Sum(s => s.Desativados),
            Taxa: TaxaOcupacao(ocupados, capacidade));
    }

    /// <summary>
    /// Consolida a rede a partir das ocupações já fechadas por unidade. Só a taxa é
    /// recalculada — somar percentual de unidades de tamanhos diferentes daria média
    /// aritmética onde o certo é ponderada pela capacidade.
    /// </summary>
    private static Ocupacao? SomarOcupacoes(IReadOnlyList<Ocupacao> ocupacoes)
    {
        if (ocupacoes.Count == 0)
        {
            return null;
        }

        var capacidade = ocupacoes.Sum(o => o.Leitos);
        return new Ocupacao(
            Leitos: capacidade,
            Ocupados: ocupacoes.Sum(o => o.Ocupados),
            Livres: ocupacoes.Sum(o => o.Livres),
            Bloqueados: ocupacoes.Sum(o => o.Bloqueados),
            Excedente: ocupacoes.Sum(o => o.Excedente),
            EmLeitoExtra: ocupacoes.Sum(o => o.EmLeitoExtra),
            Extras: ocupacoes.Sum(o => o.Extras),
            Virtuais: ocupacoes.Sum(o => o.Virtuais),
            Desativados: ocupacoes.Sum(o => o.Desativados),
            Taxa: TaxaOcupacao(ocupacoes.Sum(o => o.Ocupados), capacidade));
    }

    /// <summary>Pode passar de 100%: leito extra em uso é lotação, não capacidade nova.</summary>
    private static double? TaxaOcupacao(int ocupados, int capacidade) =>
        capacidade > 0 ? Math.Round(100.0 * ocupados / capacidade, 1) : null;

    /// <summary>Nome do setor em Caixa de Título — o cadastro grava tudo em CAIXA ALTA.</summary>
    private static string NomeSetor(string? bruto)
    {
        var texto = bruto?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            return "Sem setor";
        }

        var cultura = PtBr ?? CultureInfo.InvariantCulture;
        return cultura.TextInfo.ToTitleCase(texto.ToLower(cultura));
    }

    /// <summary>
    /// A rede soma só os leitos de quem TEM cadastro utilizável, e o escopo diz quem
    /// entrou. Perfil e permanência vêm do Conde, única unidade que interna.
    /// </summary>
    private static LeitosSecao? SomarLeitos(IReadOnlyList<UnidadePainel> reais)
    {
        var comLeitos = reais
            .Where(u => u.Leitos?.Ocupacao is not null)
            .ToList();
        var todas = reais.Select(u => u.Leitos).OfType<LeitosSecao>().ToList();
        if (todas.Count == 0)
        {
            return null;
        }

        var setores = comLeitos.SelectMany(u => u.Leitos!.Setores).ToList();
        var doConde = reais.First(u => u.Id == Unidades.IdConde).Leitos;

        return new LeitosSecao(
            AtualizadoEm: MaisAntigo(todas, s => s.AtualizadoEm),
            // Soma as OCUPAÇÕES já fechadas de cada unidade, não os setores exibidos: a
            // lista de setores é filtrada (setor só com leito extra não é linha de tela),
            // e re-somá-la fazia a aba Geral publicar 88 extras onde o Conde sozinho tem 98.
            Ocupacao: SomarOcupacoes(comLeitos.Select(u => u.Leitos!.Ocupacao!).ToList()),
            Setores: setores.OrderByDescending(s => s.Ocupados).ThenByDescending(s => s.Leitos).ToList(),
            Perfil: doConde?.Perfil,
            Permanencia: doConde?.Permanencia,
            Observacao: SomarObservacao(todas),
            Escopo: comLeitos.Count == 0
                ? null
                : string.Join(" + ", comLeitos.Select(u => u.Id == Unidades.IdConde ? Unidades.SiglaConde : u.Nome)),
            Indisponivel: null);
    }

    private static ObservacaoFluxo? SomarObservacao(IReadOnlyList<LeitosSecao> secoes)
    {
        var fluxos = secoes.Select(s => s.Observacao).OfType<ObservacaoFluxo>().ToList();
        return fluxos.Count == 0
            ? null
            : new ObservacaoFluxo(fluxos[0].Rotulo, fluxos.Sum(f => f.Encaminhados), fluxos.Sum(f => f.Classificados));
    }

    // ── Montagem das seções comuns ─────────────────────────────────────────────

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

        var ant = LerPeriodoInternacao(mesAnterior);
        var atu = LerPeriodoInternacao(mesAtual);
        var hoj = LerPeriodoInternacao(diaAtual);
        var comp = LerPeriodoInternacao(diasCompletosPeriodo);

        return new InternacoesSecao(
            AtualizadoEm: carimbo,
            MesAnterior: new InternacoesMes(
                RotuloMes(mesAnteriorData), ant.Total, ant.Maternidade, ant.Ate17, ant.Adultos,
                MediaDiaria(ant.Total, diasMesAnterior)),
            MesAtual: new InternacoesMes(
                // Média diária do mês atual SEGUE a regra dos dias completos (a mesma dos
                // atendimentos) — decisão confirmada; o contrato foi alinhado a essa regra.
                RotuloMes(hoje), atu.Total, atu.Maternidade, atu.Ate17, atu.Adultos,
                MediaDiaria(comp.Total, diasCompletos)),
            Hoje: new InternacoesHoje(hoj.Total, hoj.Maternidade, hoj.Ate17, hoj.Adultos),
            SerieDiaria: MontarSerieDiariaInternacoes(serieDiaria, hoje),
            Escopo: null);
    }

    private static (int Total, int Maternidade, int Ate17, int Adultos) LerPeriodoInternacao(
        ResultadoConsulta resultado)
    {
        var linha = resultado.Linhas[0];
        return (ComoInt(linha[0]), ComoInt(linha[1]), ComoInt(linha[2]), ComoInt(linha[3]));
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
    /// Série de internações (35 dias contínuos) com as três faixas por dia — SÓ esta série
    /// carrega o split (contrato); dias sem linha viram 0/0/0/0.
    /// </summary>
    private static List<DiaQtdInternacao> MontarSerieDiariaInternacoes(ResultadoConsulta resultado, DateTimeOffset hoje)
    {
        var porDia = new Dictionary<DateOnly, (int Qtd, int? Maternidade, int? Ate17, int? Adultos)>();
        foreach (var linha in resultado.Linhas)
        {
            if (linha[0] is DateTime dia)
            {
                porDia[DateOnly.FromDateTime(dia)] =
                    (ComoInt(linha[1]), ComoIntOuNulo(linha[2]), ComoIntOuNulo(linha[3]), ComoIntOuNulo(linha[4]));
            }
        }

        var fim = DateOnly.FromDateTime(hoje.Date);
        var serie = new List<DiaQtdInternacao>(35);
        for (var dia = fim.AddDays(-34); dia <= fim; dia = dia.AddDays(1))
        {
            var (qtd, maternidade, ate17, adultos) = porDia.GetValueOrDefault(dia, (0, 0, 0, 0));
            serie.Add(new DiaQtdInternacao(
                dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), qtd, maternidade, ate17, adultos));
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
            SerieDiaria: MontarSerieDiariaPartos(serieDiaria, hoje),
            Escopo: null);
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
            PctCesarea: partos > 0 ? Math.Round(100.0 * cesareas / partos, 1) : null,
            Natimortos: ComoInt(l[9]),
            ComMalformacao: ComoInt(l[10]),
            MalformacaoSemInfo: ComoInt(l[11]),
            ATermo: ComoInt(l[12]),
            PrematuroTardio: ComoInt(l[13]),
            PosTermo: ComoInt(l[14]),
            GestacaoSemInfo: ComoInt(l[15]),
            GravidezUnica: ComoInt(l[16]),
            GravidezMultipla: ComoInt(l[17]),
            Apgar1Abaixo7: ComoInt(l[18]),
            EstaturaMedia: ComoDoubleOuNulo(l[19]),
            PerimetroCefalicoMedio: ComoDoubleOuNulo(l[20]),
            IdadeMediaMae: ComoDoubleOuNulo(l[21]),
            MaeAte17: ComoInt(l[22]),
            MaeMenor20: ComoInt(l[23]),
            Mae35Mais: ComoInt(l[24]));
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

    private static List<EsperaCor> MontarEsperaPeriodo(ResultadoConsulta resultado, string unidadeId)
    {
        // Acumula por cor normalizada. Cores fora do contrato (SALUX no Conde, variantes
        // de subdescrição na UPA) somam no bucket certo — médias mescladas por média
        // ponderada (aproximação aceitável: são caudas de meia dúzia de casos por mês).
        var buckets = new Dictionary<string, EsperaAcumulador>();
        foreach (var linha in resultado.Linhas)
        {
            var cor = NormalizarCor(linha[0] as string, unidadeId);
            var bucket = buckets.TryGetValue(cor, out var existente) ? existente : new EsperaAcumulador();
            bucket.Somar(new EsperaCor(
                Cor: cor,
                Pacientes: ComoInt(linha[1]),
                ComAtendimento: ComoInt(linha[2]),
                MediaAteTriagem: ComoDoubleOuNulo(linha[3]),
                MediaEspera: ComoDoubleOuNulo(linha[4]),
                MedianaEspera: ComoDoubleOuNulo(linha[5]),
                P90Espera: ComoDoubleOuNulo(linha[6]),
                MetaMin: ComoIntOuNulo(linha[7]),
                PctNaMeta: ComoDoubleOuNulo(linha[8])));
            buckets[cor] = bucket;
        }

        // Sempre todas as cores do contrato, ordem fixa, mesmo sem linhas no período.
        return [.. Unidades.Cores.Select(cor => buckets.TryGetValue(cor, out var bucket)
            ? bucket.Materializar(cor)
            : EsperaCorVazia(cor))];
    }

    private static EsperaCor EsperaCorVazia(string cor) => new(cor, 0, 0, null, null, null, null, null, null);

    /// <summary>Acumulador p/ mesclar linhas que caem na mesma cor do contrato.</summary>
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
        private int _pesoPctNaMeta;
        private int? _metaMin;

        public void Somar(EsperaCor item)
        {
            _pacientes += item.Pacientes;
            _comAtendimento += item.ComAtendimento;

            if (item.MediaAteTriagem is not null)
            {
                _somaAteTriagem += item.MediaAteTriagem.Value * item.Pacientes;
                _pesoAteTriagem += item.Pacientes;
            }

            if (item.MediaEspera is not null && item.ComAtendimento > 0)
            {
                _somaEspera += item.MediaEspera.Value * item.ComAtendimento;
                _somaMediana += (item.MedianaEspera ?? 0) * item.ComAtendimento;
                _somaP90 += (item.P90Espera ?? 0) * item.ComAtendimento;
                _pesoEspera += item.ComAtendimento;
            }

            // O "% na meta" só é ponderado por quem tinha meta — quem não tem alvo não
            // entra no denominador em vez de contar como fora dele.
            if (item.PctNaMeta is not null && item.ComAtendimento > 0)
            {
                _somaPctNaMeta += item.PctNaMeta.Value * item.ComAtendimento;
                _pesoPctNaMeta += item.ComAtendimento;
            }

            // Dentro de uma unidade a meta é a mesma para a cor (vem de um cadastro só);
            // entre unidades ela nem é reportada — ver SomarEsperaPeriodo.
            _metaMin ??= item.MetaMin;
        }

        public EsperaCor Materializar(string cor)
        {
            // SEM_CLASSIFICACAO: contrato reporta média até triagem, meta e % na meta nulos
            // (não há cor ⇒ não há meta).
            var semClassificacao = cor == Unidades.SemClassificacao;
            return new EsperaCor(
                Cor: cor,
                Pacientes: _pacientes,
                ComAtendimento: _comAtendimento,
                MediaAteTriagem: semClassificacao ? null : Ponderada(_somaAteTriagem, _pesoAteTriagem),
                MediaEspera: Ponderada(_somaEspera, _pesoEspera),
                MedianaEspera: Ponderada(_somaMediana, _pesoEspera),
                P90Espera: Ponderada(_somaP90, _pesoEspera),
                MetaMin: semClassificacao ? null : _metaMin,
                PctNaMeta: semClassificacao ? null : Ponderada(_somaPctNaMeta, _pesoPctNaMeta));
        }

        private static double? Ponderada(double soma, int peso) =>
            peso > 0 ? Math.Round(soma / peso, 1) : null;
    }

    // ── Infra do ciclo ─────────────────────────────────────────────────────────

    /// <summary>Estado vivo de uma base: as seções mais recentes e o erro por tipo de ciclo.</summary>
    private sealed class EstadoUnidade
    {
        public AgoraSecao? Agora;
        public AtendimentosSecao? Atendimentos;
        public InternacoesSecao? Internacoes;
        public EsperaPorCorSecao? EsperaPorCor;
        public MaternidadeSecao? Maternidade;
        public LeitosSecao? Leitos;
        public DiagnosticosSecao? Diagnosticos;
        public DateTimeOffset? UltimaAtualizacaoOk;

        // Erro POR CICLO: um tick rápido OK não pode apagar o erro do ciclo lento (e
        // vice-versa) — cada erro só é limpo por um ciclo bem-sucedido do MESMO tipo.
        private string? _erroRapido;
        private string? _erroLento;

        /// <summary>O erro mais relevante ainda vigente — o do ciclo lento tem precedência.</summary>
        public string? ErroVigente => _erroLento ?? _erroRapido;

        public void RegistrarErro(string tipoCiclo, string erro)
        {
            if (tipoCiclo == "lento")
            {
                _erroLento = erro;
            }
            else
            {
                _erroRapido = erro;
            }
        }

        public void LimparErro(string tipoCiclo)
        {
            if (tipoCiclo == "lento")
            {
                _erroLento = null;
            }
            else
            {
                _erroRapido = null;
            }
        }

        public StatusFonte Status() => new(ErroVigente is null, ErroVigente, UltimaAtualizacaoOk);
    }

    private async Task<ResultadoConsulta> ConsultarAsync(string baseSlug, string sql, CancellationToken ct)
    {
        var resultado = await _fonte!.ExecutarUmaAsync(baseSlug, sql, ct);
        return resultado.Ok
            ? resultado
            : throw new InvalidOperationException(resultado.Erro ?? "Consulta falhou no proxy SQL.");
    }

    private async Task<int> ConsultarEscalarAsync(string baseSlug, string sql, CancellationToken ct)
    {
        var resultado = await ConsultarAsync(baseSlug, sql, ct);
        return ComoInt(resultado.Linhas[0][0]);
    }

    private static string RotuloMes(DateTimeOffset data) =>
        PtBr is not null
            ? data.ToString("MMMM/yyyy", PtBr).ToLower(PtBr) // pt-BR já é minúsculo; ToLower garante
            : $"{MesesPtBr[data.Month - 1]}/{data.Year}";     // fallback sem libicu

    private static double? MediaDiaria(int total, int dias) =>
        dias > 0 ? Math.Round((double)total / dias, 1) : null;

    /// <summary>
    /// Normaliza o nome da cor vindo do banco para os valores do contrato.
    ///
    /// <para>
    /// No Conde, <c>DS_CLASSIFICACAO_RISCO</c> traz "SALUX" e afins, que somam em
    /// SEM_CLASSIFICACAO. Na UPA, o nome vem de <c>risaco_descricao</c> ("Amarelo",
    /// "Laranja"…) e as variantes de subdescrição (Consultório/Observação) já caem na
    /// mesma cor porque a subdescrição não entra aqui. LARANJA só é aceito onde a
    /// unidade usa: se aparecesse no Conde seria dado sujo, não uma cor nova.
    /// </para>
    /// </summary>
    private static string NormalizarCor(string? ds, string unidadeId)
    {
        if (string.IsNullOrWhiteSpace(ds))
        {
            return Unidades.SemClassificacao;
        }

        var normalizada = RemoverAcentos(ds.Trim()).ToUpperInvariant();
        return Unidades.CoresDe(unidadeId).Contains(normalizada) && normalizada != Unidades.SemClassificacao
            ? normalizada
            : Unidades.SemClassificacao;
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

    /// <summary>Mensagem curta (1ª linha, sem stack) p/ log e p/ o status da fonte.</summary>
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
