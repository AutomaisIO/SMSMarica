using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Tempo;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Indicadores;

/// <summary>Situação de um coletor (faltas, canceladas, desfechos, PPI) para a tela.</summary>
public sealed record ResumoColetorDto(
    ColetorIndicadorSisreg Coletor, int Pendentes, int EmAndamento, int Concluidas, int Falhas,
    DateTime? UltimaLeituraEm);

public sealed record FalhaColetaDto(
    ColetorIndicadorSisreg Coletor, DateOnly Inicio, DateOnly Fim, string Escopo, int Tentativas, string? Erro,
    DateTime? TentadaEm);

public sealed record ColetaIndicadoresStatusDto(
    bool Ativa,
    DateTime? PausadaAte,
    bool ChaveMestraLigada,
    string? Espera,
    string? TrabalhoAtual,
    int RequisicoesNaUltimaHora,
    int TetoPorHora,
    DateTime? UltimoPassoEm,
    IReadOnlyList<ResumoColetorDto> Coletores,
    IReadOnlyList<FalhaColetaDto> UltimasFalhas,
    // Leituras que falharam e voltaram para a fila (pelo botão, pela rodada do dia ou depois de um
    // tempo esgotado): o erro de antes continua gravado até a próxima tentativa começar.
    IReadOnlyList<FalhaColetaDto> DeVoltaNaFila);

public interface IColetaIndicadoresSisregService
{
    /// <summary>Itens "em andamento" que ninguém está executando (restart, trabalho abandonado) voltam a pendente.</summary>
    Task<int> ResetarEmAndamentoAsync(Guid? exceto, CancellationToken ct);

    /// <summary>Cria as janelas que faltam e re-arma as que precisam ser relidas.</summary>
    Task PlanejarAsync(DateOnly hoje, CancellationToken ct);

    /// <summary>Próximo item pendente, já marcado em andamento e com a tentativa CONTADA (antes da chamada).</summary>
    Task<ItemColeta?> ReservarProximoAsync(CancellationToken ct);

    Task ConcluirAsync(Guid id, int linhas, CancellationToken ct);
    Task FalharAsync(Guid id, string erro, CancellationToken ct);

    /// <summary>Volta a pendente sem culpa (CAPTCHA, coletor desligado no meio).</summary>
    Task DevolverAsync(Guid id, string? motivo, CancellationToken ct);

    /// <summary>Semana de faltas que estourou o tempo: vira uma janela por dia.</summary>
    Task DividirEmDiasAsync(Guid id, string motivo, CancellationToken ct);

    /// <summary>Um item de desfechos por unidade para o mês.</summary>
    Task CriarItensDeUnidadesAsync(DateOnly mes, IReadOnlyList<UnidadeSolicitanteSisreg> unidades, CancellationToken ct);

    /// <summary>Botão da tela: todas as falhas voltam a pendente, com as tentativas zeradas.</summary>
    Task<int> RearmarFalhasAsync(CancellationToken ct);

    Task<(IReadOnlyList<ResumoColetorDto> Coletores, IReadOnlyList<FalhaColetaDto> Falhas, IReadOnlyList<FalhaColetaDto> DeVoltaNaFila)> ResumoAsync(CancellationToken ct);
}

/// <summary>
/// O cursor do coletor dos Indicadores de Regulação (<c>sisreg_indicador_coleta</c>): quais janelas
/// existem, em que estado, e quando uma volta a ser lida. Não fala com o SISREG — quem fala é o
/// agendador, um passo por tick (ver <see cref="Background.ColetaIndicadoresScheduler"/>).
///
/// <para><b>Só o recente.</b> O passado (jan/2025–ago/2026) foi carregado pelo laboratório em
/// 30/09/2026 e fica como está; o plano olha só os últimos <see cref="ColetaIndicadoresOpcoes.MesesRecentes"/>
/// meses fechados.</para>
/// </summary>
public sealed class ColetaIndicadoresSisregService(
    SmsMaisDbContext db,
    IOptions<ColetaIndicadoresOpcoes> opcoes) : IColetaIndicadoresSisregService
{
    private readonly ColetaIndicadoresOpcoes _opcoes = opcoes.Value;

    /// <summary>Erro gravado na semana dividida — ela não é re-armada (os dias a substituem).</summary>
    public const string MarcaDividida = "DIVIDIDA EM DIAS";

    public async Task<int> ResetarEmAndamentoAsync(Guid? exceto, CancellationToken ct) =>
        await db.SisregIndicadorColetas
            .Where(c => c.Status == StatusColetaIndicador.EmAndamento && (exceto == null || c.Id != exceto))
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Status, StatusColetaIndicador.Pendente), ct);

    public async Task PlanejarAsync(DateOnly hoje, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var inicioDoDia = FusoBrasilia.DeBrasiliaParaUtc(hoje.ToDateTime(TimeOnly.MinValue));

        // Falha de ontem (ou antes) tenta de novo hoje — até o teto de tentativas.
        await db.SisregIndicadorColetas
            .Where(c => c.Status == StatusColetaIndicador.Falha
                        && c.Tentativas < _opcoes.MaximoTentativas
                        && (c.IniciadoEm == null || c.IniciadoEm < inicioDoDia)
                        && (c.Erro == null || !c.Erro.StartsWith(MarcaDividida)))
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Status, StatusColetaIndicador.Pendente), ct);

        var meses = PlanoColetaIndicadores.MesesFechados(hoje, _opcoes.MesesRecentes);
        var desde = meses.Count > 0 ? meses[0] : hoje;
        var existentes = await db.SisregIndicadorColetas
            .Where(c => c.JanelaFim >= desde.AddMonths(-24))
            .ToListAsync(ct);
        bool Existe(ColetorIndicadorSisreg col, DateOnly ini, string escopo) =>
            existentes.Any(c => c.Coletor == col && c.JanelaInicio == ini && c.Escopo == escopo);
        void Criar(ColetorIndicadorSisreg col, DateOnly ini, DateOnly fim, string escopo)
        {
            var novo = new SisregIndicadorColeta
            {
                Id = Guid.NewGuid(), Coletor = col, JanelaInicio = ini, JanelaFim = fim, Escopo = escopo,
                Status = StatusColetaIndicador.Pendente, CriadoEm = agora,
            };
            db.SisregIndicadorColetas.Add(novo);
            existentes.Add(novo);
        }

        // --- faltas: semanas já velhas o bastante ---------------------------------------------------
        foreach (var s in PlanoColetaIndicadores.SemanasDeFaltas(hoje, _opcoes.MesesRecentes, _opcoes.DiasParaFaltas))
        {
            var coberta = existentes.Any(c => c.Coletor == ColetorIndicadorSisreg.Faltas
                                              && c.Status == StatusColetaIndicador.Concluida
                                              && c.JanelaInicio <= s.Inicio && c.JanelaFim >= s.Fim);
            if (!coberta && !Existe(ColetorIndicadorSisreg.Faltas, s.Inicio, "")) Criar(ColetorIndicadorSisreg.Faltas, s.Inicio, s.Fim, "");
        }

        // --- faltas recentes: a mesma lista, das semanas novas demais para o oficial ------------------
        // Rotina, não carga: cada janela volta a pendente quando a última tentativa envelhece, e a
        // contagem de tentativas recomeça — o teto existe para janela que nunca fecha, e esta fecha e
        // reabre de propósito. A semana corrente cresce um dia por dia; o fim acompanha.
        if (_opcoes.MinutosParaRelerFaltasRecentes > 0)
        {
            var vencida = agora.AddMinutes(-_opcoes.MinutosParaRelerFaltasRecentes);
            foreach (var s in PlanoColetaIndicadores.SemanasRecentesDeFaltas(hoje, _opcoes.DiasParaFaltas))
            {
                var item = existentes.FirstOrDefault(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes
                                                          && c.JanelaInicio == s.Inicio && c.Escopo == "");
                if (item is null)
                {
                    Criar(ColetorIndicadorSisreg.FaltasRecentes, s.Inicio, s.Fim, "");
                    continue;
                }
                if (item.Status == StatusColetaIndicador.EmAndamento) continue;

                // Semana que estourou o tempo do SISREG foi dividida em dias: ela não volta a ser
                // tentada inteira (estouraria de novo, hora após hora — 09–16/09 e 17–23/09 de 2026
                // nunca leram assim), quem reabre são os dias. A semana corrente ganha o dia novo aqui.
                if (item.Erro?.StartsWith(MarcaDividida, StringComparison.Ordinal) == true)
                {
                    item.JanelaFim = s.Fim;
                    foreach (var d in PlanoColetaIndicadores.Dias(s))
                    {
                        var dia = existentes.FirstOrDefault(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes
                                                                 && c.Escopo == PlanoColetaIndicadores.EscopoDia
                                                                 && c.JanelaInicio == d.Inicio);
                        if (dia is null)
                        {
                            Criar(ColetorIndicadorSisreg.FaltasRecentes, d.Inicio, d.Fim, PlanoColetaIndicadores.EscopoDia);
                            continue;
                        }
                        if (dia.Status != StatusColetaIndicador.EmAndamento
                            && (dia.IniciadoEm is null || dia.IniciadoEm < vencida))
                        {
                            dia.Status = StatusColetaIndicador.Pendente;
                            dia.Tentativas = 0;
                        }
                    }
                    continue;
                }

                if (item.JanelaFim != s.Fim || item.IniciadoEm is null || item.IniciadoEm < vencida)
                {
                    item.JanelaFim = s.Fim;
                    item.Status = StatusColetaIndicador.Pendente;
                    item.Tentativas = 0;
                }
            }
        }

        // Semana que já ganhou a leitura oficial não precisa mais da recente: a oficial passa a ser
        // quem diz que aquele período foi lido.
        foreach (var provisoria in existentes
                     .Where(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes
                                 && existentes.Any(o => o.Coletor == ColetorIndicadorSisreg.Faltas
                                                        && o.Status == StatusColetaIndicador.Concluida
                                                        && o.JanelaInicio <= c.JanelaInicio && o.JanelaFim >= c.JanelaFim))
                     .ToList())
        {
            db.SisregIndicadorColetas.Remove(provisoria);
            existentes.Remove(provisoria);
        }

        // --- PPI: competência fechada sem cotas gravadas --------------------------------------------
        foreach (var comp in PlanoColetaIndicadores.CompetenciasDePpi(hoje, _opcoes.MesesRecentes, _opcoes.DiaDaPpi))
        {
            if (Existe(ColetorIndicadorSisreg.Ppi, comp, "")) continue;
            if (await db.SisregPpiCotas.AnyAsync(c => c.Competencia == comp, ct)) continue;
            Criar(ColetorIndicadorSisreg.Ppi, comp, comp.AddMonths(1).AddDays(-1), "");
        }

        // --- canceladas -----------------------------------------------------------------------------
        // Mês recente SEM total conhecido (nem janela mensal, nem todos os dias fechados pela conciliação):
        // lê o mês inteiro — é o que dá o total oficial E todos os motivos.
        foreach (var mes in meses)
        {
            var fim = mes.AddMonths(1).AddDays(-1);
            if (TotalDeclaradoDeCanceladas(existentes, mes) is not null) continue;
            var mensal = existentes.FirstOrDefault(c => c.Coletor == ColetorIndicadorSisreg.Canceladas
                                                        && c.JanelaInicio == mes && c.Escopo == "");
            if (mensal is null) Criar(ColetorIndicadorSisreg.Canceladas, mes, fim, "");
        }

        // Mês com total conhecido mas sem as linhas (a carga de 30/09 trouxe só o total): AMOSTRA de
        // páginas espalhadas, para os motivos. Uma vez por mês — o cálculo estima a partir dela. O total
        // declarado fica como está.
        var mesesComTotal = existentes
            .Where(c => c.Coletor == ColetorIndicadorSisreg.Canceladas && c.JanelaInicio.Day == 1)
            .Select(c => c.JanelaInicio)
            .Distinct()
            .Where(m => m < new DateOnly(hoje.Year, hoje.Month, 1))
            .ToList();
        foreach (var mes in mesesComTotal)
        {
            if (TotalDeclaradoDeCanceladas(existentes, mes) is not { } total || total == 0) continue;
            if (Existe(ColetorIndicadorSisreg.Canceladas, mes, PlanoColetaIndicadores.EscopoAmostra)) continue;
            var ini = FusoBrasilia.DeBrasiliaParaUtc(mes.ToDateTime(TimeOnly.MinValue));
            var fimUtc = FusoBrasilia.DeBrasiliaParaUtc(mes.AddMonths(1).ToDateTime(TimeOnly.MinValue));
            var gravadas = await db.SisregMarcacoesCanceladas.CountAsync(c => c.CanceladoEm >= ini && c.CanceladoEm < fimUtc, ct);
            if (gravadas >= total * 0.9) continue;
            Criar(ColetorIndicadorSisreg.Canceladas, mes, mes.AddMonths(1).AddDays(-1), PlanoColetaIndicadores.EscopoAmostra);
        }

        // --- desfechos: lista de unidades por mês; relê as unidades enquanto o mês é recente --------
        foreach (var mes in meses)
        {
            var fim = mes.AddMonths(1).AddDays(-1);
            // A carga do laboratório é uma janela LONGA por unidade (jan/2025–ago/2026): cobre o mês.
            var cobertoPelaCarga = existentes.Any(c => c.Coletor == ColetorIndicadorSisreg.Desfechos
                                                       && c.Status == StatusColetaIndicador.Concluida
                                                       && c.JanelaInicio < mes && c.JanelaFim >= fim);
            if (cobertoPelaCarga) continue;
            if (!Existe(ColetorIndicadorSisreg.Desfechos, mes, TrabalhoUnidades.Escopo))
                Criar(ColetorIndicadorSisreg.Desfechos, mes, fim, TrabalhoUnidades.Escopo);
        }
        var releitura = agora.AddDays(-_opcoes.DiasParaReler);
        foreach (var item in existentes.Where(c => c.Coletor == ColetorIndicadorSisreg.Desfechos
                                                   && c.Escopo != TrabalhoUnidades.Escopo
                                                   && c.Status == StatusColetaIndicador.Concluida
                                                   && c.JanelaInicio >= desde
                                                   && c.JanelaInicio.Day == 1
                                                   && c.JanelaFim == c.JanelaInicio.AddMonths(1).AddDays(-1)
                                                   && c.LidoEm < releitura))
        {
            item.Status = StatusColetaIndicador.Pendente;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Total OFICIAL de canceladas do mês já conhecido: a janela mensal concluída (coletor ou carga) ou, sem
    /// ela, a soma dos dias — só se a conciliação fechou TODOS os dias do mês. Mesma regra do cálculo.
    /// </summary>
    private static int? TotalDeclaradoDeCanceladas(IEnumerable<SisregIndicadorColeta> existentes, DateOnly mes)
    {
        var fim = mes.AddMonths(1).AddDays(-1);
        var doMes = existentes.Where(c => c.Coletor == ColetorIndicadorSisreg.Canceladas
                                          && c.Status == StatusColetaIndicador.Concluida
                                          && c.JanelaInicio >= mes && c.JanelaInicio <= fim).ToList();
        if (doMes.FirstOrDefault(c => c.Escopo == "" && c.JanelaInicio == mes && c.JanelaFim == fim) is { Linhas: { } n })
            return n;
        var dias = doMes.Where(c => c.Escopo == PlanoColetaIndicadores.EscopoDia).ToList();
        return dias.Select(d => d.JanelaInicio).Distinct().Count() == fim.Day ? dias.Sum(d => d.Linhas ?? 0) : null;
    }

    public async Task<ItemColeta?> ReservarProximoAsync(CancellationToken ct)
    {
        // Barato primeiro: PPI (1 requisição), lista de unidades, faltas (2), desfechos (3), amostra de
        // motivos (6), e por último as canceladas de um mês inteiro (~100 páginas).
        var item = await db.SisregIndicadorColetas
            .Where(c => c.Status == StatusColetaIndicador.Pendente && c.Tentativas < _opcoes.MaximoTentativas)
            .OrderBy(c => c.Coletor == ColetorIndicadorSisreg.Ppi ? 0
                : c.Escopo == TrabalhoUnidades.Escopo ? 1
                : c.Coletor == ColetorIndicadorSisreg.Faltas || c.Coletor == ColetorIndicadorSisreg.FaltasRecentes ? 2
                : c.Coletor == ColetorIndicadorSisreg.Desfechos ? 3
                : c.Escopo == PlanoColetaIndicadores.EscopoAmostra ? 4 : 5)
            .ThenBy(c => c.JanelaInicio)
            .ThenBy(c => c.Escopo)
            .FirstOrDefaultAsync(ct);
        if (item is null) return null;

        // A tentativa conta ANTES da chamada: se o processo morrer no meio, o teto ainda vale.
        item.Status = StatusColetaIndicador.EmAndamento;
        item.Tentativas++;
        item.IniciadoEm = DateTime.UtcNow;
        item.Erro = null;
        await db.SaveChangesAsync(ct);
        return new ItemColeta(item.Id, item.Coletor, item.JanelaInicio, item.JanelaFim, item.Escopo);
    }

    public Task ConcluirAsync(Guid id, int linhas, CancellationToken ct) =>
        db.SisregIndicadorColetas.Where(c => c.Id == id).ExecuteUpdateAsync(u => u
            .SetProperty(c => c.Status, StatusColetaIndicador.Concluida)
            .SetProperty(c => c.Linhas, linhas)
            .SetProperty(c => c.LidoEm, DateTime.UtcNow)
            .SetProperty(c => c.Erro, (string?)null), ct);

    public Task FalharAsync(Guid id, string erro, CancellationToken ct) =>
        db.SisregIndicadorColetas.Where(c => c.Id == id).ExecuteUpdateAsync(u => u
            .SetProperty(c => c.Status, StatusColetaIndicador.Falha)
            .SetProperty(c => c.Erro, Cortar(erro)), ct);

    public Task DevolverAsync(Guid id, string? motivo, CancellationToken ct) =>
        db.SisregIndicadorColetas.Where(c => c.Id == id).ExecuteUpdateAsync(u => u
            .SetProperty(c => c.Status, StatusColetaIndicador.Pendente)
            .SetProperty(c => c.Erro, Cortar(motivo)), ct);

    public async Task DividirEmDiasAsync(Guid id, string motivo, CancellationToken ct)
    {
        var semana = await db.SisregIndicadorColetas.SingleAsync(c => c.Id == id, ct);
        semana.Status = StatusColetaIndicador.Falha;
        semana.Erro = Cortar($"{MarcaDividida}: {motivo}");

        var existentes = await db.SisregIndicadorColetas
            .Where(c => c.Coletor == semana.Coletor && c.Escopo == PlanoColetaIndicadores.EscopoDia
                        && c.JanelaInicio >= semana.JanelaInicio && c.JanelaInicio <= semana.JanelaFim)
            .Select(c => c.JanelaInicio)
            .ToListAsync(ct);
        foreach (var dia in PlanoColetaIndicadores.Dias(new JanelaColeta(semana.JanelaInicio, semana.JanelaFim)))
        {
            if (existentes.Contains(dia.Inicio)) continue;
            db.SisregIndicadorColetas.Add(new SisregIndicadorColeta
            {
                Id = Guid.NewGuid(), Coletor = semana.Coletor, JanelaInicio = dia.Inicio, JanelaFim = dia.Fim,
                Escopo = PlanoColetaIndicadores.EscopoDia, Status = StatusColetaIndicador.Pendente,
                CriadoEm = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task CriarItensDeUnidadesAsync(
        DateOnly mes, IReadOnlyList<UnidadeSolicitanteSisreg> unidades, CancellationToken ct)
    {
        var fim = mes.AddMonths(1).AddDays(-1);
        var ja = await db.SisregIndicadorColetas
            .Where(c => c.Coletor == ColetorIndicadorSisreg.Desfechos && c.JanelaInicio == mes)
            .Select(c => c.Escopo)
            .ToListAsync(ct);
        foreach (var u in unidades.DistinctBy(u => u.Cnes).Where(u => !ja.Contains(u.Cnes)))
        {
            db.SisregIndicadorColetas.Add(new SisregIndicadorColeta
            {
                Id = Guid.NewGuid(), Coletor = ColetorIndicadorSisreg.Desfechos, JanelaInicio = mes, JanelaFim = fim,
                Escopo = u.Cnes, Status = StatusColetaIndicador.Pendente, CriadoEm = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> RearmarFalhasAsync(CancellationToken ct) =>
        await db.SisregIndicadorColetas
            .Where(c => c.Status == StatusColetaIndicador.Falha
                        && (c.Erro == null || !c.Erro.StartsWith(MarcaDividida)))
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.Status, StatusColetaIndicador.Pendente)
                .SetProperty(c => c.Tentativas, 0), ct);

    public async Task<(IReadOnlyList<ResumoColetorDto> Coletores, IReadOnlyList<FalhaColetaDto> Falhas, IReadOnlyList<FalhaColetaDto> DeVoltaNaFila)> ResumoAsync(
        CancellationToken ct)
    {
        var porColetor = await db.SisregIndicadorColetas
            .GroupBy(c => c.Coletor)
            .Select(g => new ResumoColetorDto(
                g.Key,
                g.Count(c => c.Status == StatusColetaIndicador.Pendente),
                g.Count(c => c.Status == StatusColetaIndicador.EmAndamento),
                g.Count(c => c.Status == StatusColetaIndicador.Concluida),
                g.Count(c => c.Status == StatusColetaIndicador.Falha
                             && (c.Erro == null || !c.Erro.StartsWith(MarcaDividida))),
                g.Max(c => c.LidoEm)))
            .ToListAsync(ct);
        var coletores = Enum.GetValues<ColetorIndicadorSisreg>()
            .Select(col => porColetor.FirstOrDefault(r => r.Coletor == col) ?? new ResumoColetorDto(col, 0, 0, 0, 0, null))
            .ToList();

        var falhas = await db.SisregIndicadorColetas
            .Where(c => c.Status == StatusColetaIndicador.Falha
                        && (c.Erro == null || !c.Erro.StartsWith(MarcaDividida)))
            .OrderByDescending(c => c.IniciadoEm)
            .Take(8)
            .Select(c => new FalhaColetaDto(c.Coletor, c.JanelaInicio, c.JanelaFim, c.Escopo, c.Tentativas, c.Erro, c.IniciadoEm))
            .ToListAsync(ct);

        var deVolta = await db.SisregIndicadorColetas
            .Where(c => c.Status == StatusColetaIndicador.Pendente && c.Erro != null
                        && !c.Erro.StartsWith(MarcaDividida))
            .OrderBy(c => c.Coletor).ThenBy(c => c.JanelaInicio)
            .Take(12)
            .Select(c => new FalhaColetaDto(c.Coletor, c.JanelaInicio, c.JanelaFim, c.Escopo, c.Tentativas, c.Erro, c.IniciadoEm))
            .ToListAsync(ct);
        return (coletores, falhas, deVolta);
    }

    private static string? Cortar(string? s) => s is null ? null : s.Length <= 1000 ? s : s[..1000];
}
