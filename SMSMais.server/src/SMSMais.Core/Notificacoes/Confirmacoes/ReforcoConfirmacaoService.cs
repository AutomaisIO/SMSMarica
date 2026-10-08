using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SMSMais.Core.Conversas;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.PendenciasCadastro;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

/// <summary>
/// Régua de reforço da confirmação: para quem recebeu a PRIMEIRA mensagem e não se identificou,
/// um reforço três dias depois (toque 2) e, perto da data, a orientação de procurar o posto
/// (toque 3, terminal). Ver <see cref="ReguaReforcoConfirmacao"/> para as regras.
///
/// <para>Por que existe: a primeira mensagem não pede nada ("seu exame foi agendado") e muita
/// gente não responde. Sem régua, esse paciente ficava sem as informações do agendamento até ir
/// ao posto por conta própria — e sem nenhum aviso de que era lá que a guia estava.</para>
///
/// <para><b>Tetos por NÚMERO</b>, não por paciente — do outro lado há uma pessoa, e é ela que
/// bloqueia a conta: silêncio mínimo desde o último automático, um reforço por semana, uma
/// orientação a cada 20 dias, um toque por passagem. O número que atende vários pacientes recebe
/// uma mensagem só; as outras primeiras mensagens do mesmo número ganham o carimbo de cobertas.</para>
///
/// <para>Só ENFILEIRA (molde do <see cref="LembreteAgendamentoService"/>). Quem envia é o
/// <see cref="EnviadorComunicacaoService"/>, com a mesma janela, a mesma vazão e as mesmas regras
/// de LGPD — e o envio CONFERE TUDO DE NOVO, porque entre a fila e a saída a pessoa pode ter
/// respondido.</para>
/// </summary>
public interface IReforcoConfirmacaoService
{
    /// <summary>Enfileira os toques devidos agora. Devolve quantos entraram na fila.</summary>
    Task<int> EnfileirarDevidosAsync(CancellationToken ct = default);
}

public sealed class ReforcoConfirmacaoService(
    SmsMaisDbContext db,
    IConfirmacaoConfiguracaoService regras,
    IComunicacaoPacienteService comunicacoes,
    IPacienteResolver pacientes,
    IContatoNegadoService contatosNegados,
    IOptions<ComunicacaoPacienteOptions> options,
    ILogger<ReforcoConfirmacaoService> logger) : IReforcoConfirmacaoService
{
    /// <summary>Teto por passagem: a régua anda devagar por natureza — rajada aqui é sinal de erro.</summary>
    private const int MaximoPorPassagem = 500;

    /// <summary>Reforço só para agendamento a pelo menos tantos dias: mais perto que isso, quem
    /// fala é a orientação ao posto.</summary>
    private const int ReforcoAntecedenciaMinimaDias = 3;

    /// <summary>Orientação ao posto só com pelo menos tantas horas até o agendamento — mandar "vá
    /// ao posto buscar a guia" na véspera à noite não dá tempo de ir.</summary>
    private const int OrientacaoAntecedenciaMinimaHoras = 24;

    public Task<int> EnfileirarDevidosAsync(CancellationToken ct = default)
        => EnfileirarDevidosAsync(DateTime.UtcNow, ct);

    /// <summary>Com o relógio explícito — é o que deixa a régua (domingo, prazos) ser testada.</summary>
    internal async Task<int> EnfileirarDevidosAsync(DateTime agora, CancellationToken ct)
    {
        var cfg = await regras.ObterAsync(ct);
        var o = options.Value;
        var reforcoLigado = cfg.ReforcoConfirmacaoHabilitado && o.EnviarReforcoConfirmacao;
        var orientacaoLigada = cfg.OrientacaoPostoHabilitada && o.EnviarOrientacaoPosto;
        if (!reforcoLigado && !orientacaoLigada) return 0;

        // Domingo não. O envio também segura (o que entrou no sábado à noite espera a segunda),
        // mas nem enfileirar evita a fila parada com cara de problema.
        if (ReguaReforcoConfirmacao.EhDomingo(agora)) return 0;

        var principalAte = agora.AddHours(-o.ReforcoAposHoras);
        var dataMinimaReforco = agora.AddDays(ReforcoAntecedenciaMinimaDias);
        var dataMinimaOrientacao = agora.AddHours(OrientacaoAntecedenciaMinimaHoras);
        // A orientação sai o que vier primeiro: X dias antes da data (os mesmos do lembrete) ou
        // tantas horas depois do toque 2 (reforço ou lembrete).
        var orientacaoPelaData = agora.AddDays(cfg.LembreteDiasAntes);
        var toque2Ate = agora.AddHours(-o.OrientacaoAposReforcoHoras);

        // O universo, filtrado no banco até onde dá — o que sobra é quase só o que está devido de
        // fato, e o teto da passagem não deixa ninguém esperando atrás de quem não sai.
        var brutos = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(p => p.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                && p.Status == StatusComunicacao.AguardandoVerificacaoCadastral
                && p.EnviadoEm != null && p.EntregueEm != null
                && p.EnviadoEm <= principalAte
                && p.Telefone != null
                && p.SolicitacaoId != null
                && p.Solicitacao!.ExcluidoEm == null
                && p.Solicitacao.Status != StatusSolicitacao.Cancelada
                && p.Solicitacao.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
                && p.Solicitacao.DataAgendada >= dataMinimaOrientacao
                // "Vou ao posto": a resposta prometeu não insistir mais.
                && (p.MotivoFalha == null || !p.MotivoFalha.StartsWith(ReguaReforcoConfirmacao.CarimboVaiAoPosto))
                // Uma pessoa está com a solicitação (menu Confirmações): o automático não entra.
                && !db.AtendimentosConfirmacao.Any(a => a.SolicitacaoId == p.SolicitacaoId && a.EncerradoEm == null)
                // O toque 3 já saiu (ou está na fila): depois dele, nada. Linha DISPENSADA antes da
                // primeira mensagem atual é de um ciclo anterior — o telefone do cadastro mudou e a
                // primeira mensagem voltou a sair para o número novo — e não conta: a régua recomeça.
                && !db.ComunicacoesPaciente.Any(c => c.SolicitacaoId == p.SolicitacaoId
                    && c.Finalidade == FinalidadeComunicacao.OrientacaoPosto
                    && !(c.Status == StatusComunicacao.Dispensada && (c.AtualizadoEm ?? c.CriadoEm) < p.EnviadoEm))
                && (
                    // Candidata ao reforço: nenhum toque 2 neste ciclo — nem reforço, nem lembrete
                    // (que, saindo ou na fila, ocupa o mesmo lugar).
                    (reforcoLigado
                     && p.Solicitacao.DataAgendada >= dataMinimaReforco
                     && !db.ComunicacoesPaciente.Any(c => c.SolicitacaoId == p.SolicitacaoId
                         && ((c.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao
                              && !(c.Status == StatusComunicacao.Dispensada
                                   && (c.AtualizadoEm ?? c.CriadoEm) < p.EnviadoEm))
                             || (c.Finalidade == FinalidadeComunicacao.LembreteAgendamento
                                 && (c.EnviadoEm != null || c.Status == StatusComunicacao.Pendente)))))
                    // Candidata à orientação: a data chegou perto ou o toque 2 já tem dias.
                    || (orientacaoLigada
                        && (p.Solicitacao.DataAgendada <= orientacaoPelaData
                            || db.ComunicacoesPaciente.Any(c => c.SolicitacaoId == p.SolicitacaoId
                                && (c.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao
                                    || c.Finalidade == FinalidadeComunicacao.LembreteAgendamento)
                                && c.EnviadoEm != null && c.EnviadoEm <= toque2Ate)))))
            .OrderBy(p => p.Solicitacao!.DataAgendada)
            .Take(MaximoPorPassagem)
            .Select(p => new
            {
                p.Id,
                SolicitacaoId = p.SolicitacaoId!.Value,
                p.PacienteId,
                Telefone = p.Telefone!,
                EnviadoEm = p.EnviadoEm!.Value,
                p.MotivoFalha,
                DataAgendada = p.Solicitacao!.DataAgendada!.Value,
                p.Solicitacao.FonteCriacao,
                p.Solicitacao.TipoVaga,
                TemRawSisreg = p.Solicitacao.RawSisreg != null,
                TemReforco = db.ComunicacoesPaciente.Any(c => c.SolicitacaoId == p.SolicitacaoId
                    && c.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao
                    && !(c.Status == StatusComunicacao.Dispensada && (c.AtualizadoEm ?? c.CriadoEm) < p.EnviadoEm)),
                // A linha do ciclo anterior (se houver) é REARMADA: o índice único (solicitação ×
                // finalidade) não deixa criar outra, e a trilha continua numa linha só.
                ReforcoAntigoId = db.ComunicacoesPaciente
                    .Where(c => c.SolicitacaoId == p.SolicitacaoId
                        && c.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao
                        && c.Status == StatusComunicacao.Dispensada && (c.AtualizadoEm ?? c.CriadoEm) < p.EnviadoEm)
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefault(),
                OrientacaoAntigaId = db.ComunicacoesPaciente
                    .Where(c => c.SolicitacaoId == p.SolicitacaoId
                        && c.Finalidade == FinalidadeComunicacao.OrientacaoPosto
                        && c.Status == StatusComunicacao.Dispensada && (c.AtualizadoEm ?? c.CriadoEm) < p.EnviadoEm)
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefault(),
                LembreteOcupaToque2 = db.ComunicacoesPaciente.Any(c => c.SolicitacaoId == p.SolicitacaoId
                    && c.Finalidade == FinalidadeComunicacao.LembreteAgendamento
                    && (c.EnviadoEm != null || c.Status == StatusComunicacao.Pendente)),
                UltimoToque2Em = db.ComunicacoesPaciente
                    .Where(c => c.SolicitacaoId == p.SolicitacaoId
                        && (c.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao
                            || c.Finalidade == FinalidadeComunicacao.LembreteAgendamento))
                    .Max(c => c.EnviadoEm),
            })
            .ToListAsync(ct);

        if (brutos.Count == 0) return 0;

        var principais = cfg.SomenteSisreg
            ? brutos.Where(b => OrigemAgendamento.EhDoSisreg(b.FonteCriacao, b.TemRawSisreg)).ToList()
            : brutos;
        if (principais.Count == 0) return 0;

        // ---- o que se sabe de cada NÚMERO (em lote) ----
        // Cada forma gravada do número (13 dígitos, wa_id antigo de 12) aponta para a chave do
        // número da PRINCIPAL. Mapear de volta pela forma — e não recalculando a chave de cada
        // linha lida — é o que garante que a resposta chegada no formato antigo caia no mesmo
        // número que a primeira mensagem, qualquer que seja o dígito depois do DDD.
        var chaveDaForma = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in principais)
        {
            var chave = ReguaReforcoConfirmacao.ChaveDoNumero(p.Telefone);
            foreach (var forma in ReguaReforcoConfirmacao.FormasDoNumero(p.Telefone))
                chaveDaForma.TryAdd(forma, chave);
        }
        string Chave(string telefone) => chaveDaForma.TryGetValue(telefone, out var c)
            ? c
            : ReguaReforcoConfirmacao.ChaveDoNumero(telefone);
        var formas = chaveDaForma.Keys.ToArray();
        var desde = principais.Min(p => p.EnviadoEm);

        var ultimaEntrada = (await db.MensagensWhatsApp.AsNoTracking()
                .Where(m => m.Direcao == DirecaoMensagem.Entrada && m.OcorridoEm > desde
                    && formas.Contains(m.Telefone))
                .GroupBy(m => m.Telefone)
                .Select(g => new { Telefone = g.Key, Ultima = g.Max(m => m.OcorridoEm) })
                .ToListAsync(ct))
            .GroupBy(x => Chave(x.Telefone))
            .ToDictionary(g => g.Key, g => g.Max(x => x.Ultima), StringComparer.Ordinal);

        var estados = (await db.VerificacoesCadastraisEstado.AsNoTracking()
                .Where(e => formas.Contains(e.TelefoneCanonical))
                .ToListAsync(ct))
            .GroupBy(e => Chave(e.TelefoneCanonical))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.AtualizadoEm ?? e.CriadoEm).First(),
                StringComparer.Ordinal);

        // Silêncio mínimo: recebeu qualquer automático de agendamento há pouco? Então ninguém fala.
        var limiteSilencio = agora.AddHours(-o.SilencioMinimoPorNumeroHoras);
        var silenciados = (await db.ComunicacoesPaciente.AsNoTracking()
                .Where(c => ReguaReforcoConfirmacao.FinalidadesQueContamNoSilencio.Contains(c.Finalidade)
                    && c.EnviadoEm != null && c.EnviadoEm > limiteSilencio
                    && c.Telefone != null && formas.Contains(c.Telefone))
                .Select(c => c.Telefone!)
                .ToListAsync(ct))
            .Select(Chave)
            .ToHashSet(StringComparer.Ordinal);

        // Reforço/orientação que este número já recebeu dentro do teto — ou que está na fila para
        // receber, seja de quando for (o que espera na fila vai sair; mandar outro por cima dobraria).
        // A linha da régua só ganha telefone quando sai; na fila, o número é o da sua principal.
        var limiteReforco = agora.AddDays(-o.ReforcoPorNumeroDias);
        var limiteOrientacao = agora.AddDays(-o.OrientacaoPorNumeroDias);
        var toquesRecentes = (await db.ComunicacoesPaciente.AsNoTracking()
                .Where(c => (c.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao
                             || c.Finalidade == FinalidadeComunicacao.OrientacaoPosto)
                    && (c.Status == StatusComunicacao.Pendente
                        || (c.EnviadoEm != null
                            && c.EnviadoEm >= (c.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao
                                ? limiteReforco
                                : limiteOrientacao))))
                .Select(c => new
                {
                    c.Finalidade,
                    Quando = c.EnviadoEm ?? c.CriadoEm,
                    Telefone = c.Telefone ?? db.ComunicacoesPaciente
                        .Where(p => p.SolicitacaoId == c.SolicitacaoId
                            && p.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
                        .Select(p => p.Telefone)
                        .FirstOrDefault(),
                })
                .ToListAsync(ct))
            .Where(x => x.Telefone is not null)
            .GroupBy(x => (x.Finalidade, Chave: Chave(x.Telefone!)))
            .ToDictionary(g => g.Key, g => g.Max(x => x.Quando));

        // ---- quem, de fato, está devido ----
        var devidos = new List<(Guid PrincipalId, Guid SolicitacaoId, Guid PacienteId, string Chave,
            string Telefone, FinalidadeComunicacao Finalidade, Guid? LinhaAntigaId)>();
        foreach (var p in principais)
        {
            var chave = Chave(p.Telefone);
            var houveEntrada = ultimaEntrada.TryGetValue(chave, out var ultima) && ultima > p.EnviadoEm;
            var estado = estados.GetValueOrDefault(chave);

            // A orientação primeiro: se ela está devida, o reforço já não faz sentido (ela encerra).
            // Retorno não recebe a orientação ao posto: ela manda retirar a guia, que quem volta já tem.
            var orientacaoDevida = orientacaoLigada
                && p.TipoVaga != TipoVaga.Retorno
                && p.DataAgendada >= dataMinimaOrientacao
                && (p.DataAgendada <= orientacaoPelaData || p.UltimoToque2Em is { } t2 && t2 <= toque2Ate)
                && ReguaReforcoConfirmacao.OrientacaoAlcanca(houveEntrada, estado, p.Id, p.PacienteId, agora);
            var reforcoDevido = !orientacaoDevida
                && reforcoLigado
                && !p.TemReforco && !p.LembreteOcupaToque2
                && p.DataAgendada >= dataMinimaReforco
                && ReguaReforcoConfirmacao.ReforcoAlcanca(houveEntrada, estado);

            if (orientacaoDevida)
                devidos.Add((p.Id, p.SolicitacaoId, p.PacienteId, chave, p.Telefone,
                    FinalidadeComunicacao.OrientacaoPosto, p.OrientacaoAntigaId));
            else if (reforcoDevido)
                devidos.Add((p.Id, p.SolicitacaoId, p.PacienteId, chave, p.Telefone,
                    FinalidadeComunicacao.ReforcoConfirmacao, p.ReforcoAntigoId));
        }
        if (devidos.Count == 0) return 0;

        // Contato verificado resolve a situação por outro caminho (a confirmação de verdade é
        // liberada — não é a régua que insiste). Paciente que o hub não devolveu fica para a
        // próxima passada: na dúvida, não se manda.
        var resumos = await pacientes.ResolverManyAsync(devidos.Select(d => d.PacienteId).Distinct(), ct);

        var numerosNestaPassagem = new Dictionary<string, FinalidadeComunicacao>(StringComparer.Ordinal);
        var escolhidos = new List<(Guid SolicitacaoId, FinalidadeComunicacao Finalidade, Guid? LinhaAntigaId)>();
        var cobertas = new Dictionary<Guid, string>();
        foreach (var d in devidos)
        {
            if (silenciados.Contains(d.Chave)) continue; // volta na próxima passada

            // UM por número por passagem — e o outro paciente do mesmo número fica coberto.
            if (numerosNestaPassagem.TryGetValue(d.Chave, out var jaVai))
            {
                if (jaVai == d.Finalidade) cobertas[d.PrincipalId] = Coberta(d.Finalidade, agora);
                continue;
            }

            // Teto por número: o número já recebeu este toque por outro paciente dentro do prazo.
            if (toquesRecentes.TryGetValue((d.Finalidade, d.Chave), out var quando))
            {
                cobertas[d.PrincipalId] = Coberta(d.Finalidade, quando);
                continue;
            }

            if (!resumos.TryGetValue(d.PacienteId, out var resumo)) continue;
            if (TelefoneWhatsApp.EhCelularBr(resumo.TelefoneVerificado)) continue;

            // Par número × paciente negado (pendência aberta ou carimbo no cadastro): nada automático.
            if (await contatosNegados.BloqueadoAsync(d.Telefone, d.PacienteId, ct)) continue;

            numerosNestaPassagem[d.Chave] = d.Finalidade;
            escolhidos.Add((d.SolicitacaoId, d.Finalidade, d.LinhaAntigaId));
        }

        if (escolhidos.Count > 0)
        {
            var ids = escolhidos.Select(e => e.SolicitacaoId).ToList();
            var solicitacoes = await db.Solicitacoes.AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);
            var antigasIds = escolhidos.Where(e => e.LinhaAntigaId is not null).Select(e => e.LinhaAntigaId!.Value).ToList();
            var antigas = antigasIds.Count == 0
                ? []
                : await db.ComunicacoesPaciente.Where(c => antigasIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
            foreach (var (solicitacaoId, finalidade, linhaAntigaId) in escolhidos)
            {
                if (linhaAntigaId is { } idAntiga && antigas.TryGetValue(idAntiga, out var antiga))
                    ComunicacaoPacienteService.RearmarParaNovoEnvio(antiga);
                else if (solicitacoes.TryGetValue(solicitacaoId, out var s))
                    await comunicacoes.EnfileirarAsync(s, finalidade, ct);
            }
        }

        // Carimbo nas principais cobertas por outro paciente do mesmo número — só quando muda, para
        // não reescrever a linha a cada meia hora.
        if (cobertas.Count > 0)
        {
            var ids = cobertas.Keys.ToList();
            var linhas = await db.ComunicacoesPaciente.Where(c => ids.Contains(c.Id)).ToListAsync(ct);
            foreach (var c in linhas)
            {
                var motivo = cobertas[c.Id];
                if (c.MotivoFalha == motivo) continue;
                c.MotivoFalha = motivo;
                c.AtualizadoEm = agora;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Outra passada (outro processo) enfileirou o mesmo toque entre a leitura e a escrita.
            // O índice único (solicitação × finalidade) segurou a duplicata; o resto volta na
            // próxima passada.
            db.ChangeTracker.Clear();
            logger.LogWarning(
                "Régua de reforço: colisão com outra passada ao enfileirar — nada gravado agora, tenta de novo.");
            return 0;
        }

        var reforcos = escolhidos.Count(e => e.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao);
        var orientacoes = escolhidos.Count - reforcos;
        if (escolhidos.Count > 0 || cobertas.Count > 0)
            logger.LogInformation(
                "Régua de reforço da confirmação: {Reforcos} reforço(s) e {Orientacoes} orientação(ões) ao "
                + "posto enfileirados; {Cobertas} primeira(s) mensagem(ns) coberta(s) por outro paciente do "
                + "mesmo número; {Devidos} devido(s) no total.",
                reforcos, orientacoes, cobertas.Count, devidos.Count);
        return escolhidos.Count;
    }

    /// <summary>
    /// Carimbo da principal que não ganhou o toque porque o NÚMERO já ganhou por outro paciente. A
    /// data é a do toque que cobriu — estável entre passadas, então o carimbo não é reescrito.
    /// </summary>
    internal static string Coberta(FinalidadeComunicacao finalidade, DateTime quandoUtc)
    {
        var dia = Common.Tempo.FusoBrasilia.ParaExibicao(quandoUtc)
            .ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture);
        return finalidade == FinalidadeComunicacao.OrientacaoPosto
            ? $"Orientação ao posto coberta pela que este número recebeu para outro paciente ({dia})."
            : $"Reforço coberto pelo que este número recebeu para outro paciente ({dia}).";
    }
}

/// <summary>
/// Varre as primeiras mensagens sem resposta de tempos em tempos e enfileira os toques devidos.
/// Separado do enviador de propósito (molde do <see cref="LembreteAgendamentoWorker"/>):
/// enfileirar é barato e pode rodar a qualquer hora; ENVIAR é que respeita a janela. As chaves
/// são relidas a cada tick — quem desliga às 14h quer que pare às 14h.
/// </summary>
public sealed class ReforcoConfirmacaoWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ReforcoConfirmacaoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IReforcoConfirmacaoService>()
                    .EnfileirarDevidosAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Nunca deixa subir: BackgroundServiceExceptionBehavior=StopHost derrubaria a API.
                logger.LogError(ex, "Falha ao enfileirar a régua de reforço da confirmação — tenta de novo.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
