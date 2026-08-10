using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Ser.Pacientes;

public sealed record BackfillPacientesSerDto(
    int Pacientes, int Criados, int Enriquecidos, int Inalterados, int SemChave, int Falhas,
    int DuracaoSegundos);

/// <summary>
/// Alinhamento ÚNICO entre a base espelhada do SER e o hub FHIR.
///
/// <para><b>Por que não tem tela.</b> É um acerto de uma vez só: passa nos 19.069 pacientes
/// distintos que o SER já nos deu e leva ao hub o que faltava. Depois disso quem mantém em dia é
/// a varredura, conciliando só solicitação nova ou com dado de paciente alterado. Um botão
/// permanente convidaria a re-rodar os 19 mil à toa — cada paciente custa pelo menos uma leitura
/// no hub.</para>
///
/// <para><b>Idempotente e retomável por natureza</b>, sem ponteiro: o upsert canônico tem guarda
/// de no-op, então paciente já alinhado não gera escrita nenhuma. Cair no meio e rodar de novo
/// custa releitura, nunca duplicata.</para>
///
/// <para><b>Uma solicitação por paciente, a mais recente.</b> O mesmo paciente aparece em várias
/// solicitações ao longo dos anos, e o cadastro do SER muda entre elas — telefone novo, endereço
/// novo. Conciliar todas em ordem faria o hub receber o dado antigo por último.</para>
///
/// <para><b>Gate operacional: o hub não tem undo.</b> Rodar isto escreve identidade em milhares
/// de pacientes. Só com backup do <c>fhir.patient</c> — a mesma regra do backfill de promoção
/// blob→nativo.</para>
/// </summary>
public interface ISerBackfillPacientesService
{
    Task<BackfillPacientesSerDto> ExecutarAsync(int throttleMs, CancellationToken ct);

    /// <summary>
    /// Só a FILA: solicitações que a varredura carimbou como "o cadastro do paciente mudou".
    /// É o regime permanente depois do alinhamento único — no dia a dia são dezenas, não 19 mil.
    /// </summary>
    Task<BackfillPacientesSerDto> ExecutarPendentesAsync(int limite, CancellationToken ct);
}

public sealed class SerBackfillPacientesService(
    SmsMaricaDbContext db,
    ISerConciliacaoPacienteService conciliacao,
    ILogger<SerBackfillPacientesService> logger) : ISerBackfillPacientesService
{
    /// <summary>Quantas solicitações carregar por vez — mantém a memória plana em 19 mil.</summary>
    private const int Lote = 200;

    public async Task<BackfillPacientesSerDto> ExecutarAsync(int throttleMs, CancellationToken ct)
    {
        var inicio = DateTime.UtcNow;
        int criados = 0, enriquecidos = 0, inalterados = 0, semChave = 0, falhas = 0, vistos = 0;

        var ids = await IdsMaisRecentesPorPacienteAsync(ct);
        logger.LogInformation(
            "SER/backfill de pacientes: {Qtd} pacientes distintos a conciliar com o hub.", ids.Count);

        foreach (var pagina in ids.Chunk(Lote))
        {
            ct.ThrowIfCancellationRequested();

            // Rastreadas: o id do paciente resolvido é gravado na própria solicitação.
            var solicitacoes = await db.SerSolicitacoes
                .Where(s => pagina.Contains(s.Id))
                .ToListAsync(ct);

            foreach (var s in solicitacoes)
            {
                ct.ThrowIfCancellationRequested();
                vistos++;
                try
                {
                    var r = await conciliacao.ConciliarAsync(s, ct);
                    if (r.PacienteId is { } pid)
                    {
                        s.PacienteId = pid;
                        await PropagarAsIrmasAsync(s, pid, ct);
                    }
                    switch (r.Resultado)
                    {
                        case ResultadoConciliacaoSer.Criado: criados++; break;
                        case ResultadoConciliacaoSer.Enriquecido: enriquecidos++; break;
                        case ResultadoConciliacaoSer.Inalterado: inalterados++; break;
                        case ResultadoConciliacaoSer.SemChave: semChave++; break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Um paciente que falha não pode derrubar os 19 mil. O upsert é idempotente:
                    // a próxima rodada tenta de novo sem efeito colateral.
                    falhas++;
                    logger.LogWarning(
                        ex, "SER/backfill: falhou no paciente da solicitação {IdSer}.", s.IdSer);
                }

                if (throttleMs > 0) await Task.Delay(throttleMs, ct);
            }

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "SER/backfill: {Vistos}/{Total} — {Criados} criados, {Enriq} enriquecidos, "
                + "{Inalt} inalterados, {SemChave} sem chave, {Falhas} falhas.",
                vistos, ids.Count, criados, enriquecidos, inalterados, semChave, falhas);
        }

        // O alinhamento passou por TODOS os pacientes: qualquer marca pendente (inclusive em
        // outras solicitações da mesma pessoa) já está contemplada. Limpar evita o worker
        // reprocessar em seguida o que o backfill acabou de fazer.
        if (falhas == 0)
        {
            await db.SerSolicitacoes
                .Where(s => s.PacienteConciliarEm != null)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.PacienteConciliarEm, (DateTime?)null), ct);
        }

        var duracao = (int)(DateTime.UtcNow - inicio).TotalSeconds;
        logger.LogInformation("SER/backfill de pacientes: terminado em {Seg}s.", duracao);

        return new BackfillPacientesSerDto(
            ids.Count, criados, enriquecidos, inalterados, semChave, falhas, duracao);
    }

    public async Task<BackfillPacientesSerDto> ExecutarPendentesAsync(
        int limite, CancellationToken ct)
    {
        var inicio = DateTime.UtcNow;
        int criados = 0, enriquecidos = 0, inalterados = 0, semChave = 0, falhas = 0;

        // Rastreadas (sem AsNoTracking): a marca é limpa aqui mesmo, e só depois do sucesso.
        var pendentes = await db.SerSolicitacoes
            .Where(s => s.ExcluidoEm == null && s.PacienteConciliarEm != null)
            .OrderBy(s => s.PacienteConciliarEm)
            .Take(limite)
            .ToListAsync(ct);

        if (pendentes.Count == 0) return new BackfillPacientesSerDto(0, 0, 0, 0, 0, 0, 0);

        foreach (var s in pendentes)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var r = await conciliacao.ConciliarAsync(s, ct);
                switch (r.Resultado)
                {
                    case ResultadoConciliacaoSer.Criado: criados++; break;
                    case ResultadoConciliacaoSer.Enriquecido: enriquecidos++; break;
                    case ResultadoConciliacaoSer.Inalterado: inalterados++; break;
                    case ResultadoConciliacaoSer.SemChave: semChave++; break;
                }

                // Guarda quem é a pessoa: é isto que liga a linha do SER ao resumo do
                // paciente e ao WhatsApp na tela, sem uma consulta ao hub por linha listada.
                if (r.PacienteId is { } pid)
                {
                    s.PacienteId = pid;
                    await PropagarAsIrmasAsync(s, pid, ct);
                }

                // Limpa a marca. Vale inclusive para "sem chave": mantê-la faria o worker
                // reprocessar a cada ciclo um caso sem saída. Se o SER trouxer CPF ou CNS
                // depois, o retrato do paciente muda e a varredura remarca sozinha.
                // Exceção NÃO chega aqui — quem falha fica na fila para a próxima passagem.
                s.PacienteConciliarEm = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                logger.LogWarning(
                    ex, "SER/conciliação: falhou no paciente da solicitação {IdSer}. "
                    + "Fica na fila para a próxima passagem.", s.IdSer);
            }
        }

        await db.SaveChangesAsync(ct);

        var duracao = (int)(DateTime.UtcNow - inicio).TotalSeconds;
        logger.LogInformation(
            "SER/conciliação: {Qtd} pendente(s) — {Criados} criados, {Enriq} enriquecidos, "
            + "{Inalt} inalterados, {SemChave} sem chave, {Falhas} falhas em {Seg}s.",
            pendentes.Count, criados, enriquecidos, inalterados, semChave, falhas, duracao);

        return new BackfillPacientesSerDto(
            pendentes.Count, criados, enriquecidos, inalterados, semChave, falhas, duracao);
    }

    /// <summary>
    /// Espalha o paciente resolvido para as <b>outras solicitações da mesma pessoa</b>.
    ///
    /// <para>A conciliação processa UMA solicitação por paciente (a mais recente) — ir ao hub uma
    /// vez por linha seria desperdício, já que a pessoa é a mesma. Só que o id ficava gravado
    /// nessa única linha, e na tela o bonequinho aparecia no pedido mais novo e sumia nos
    /// anteriores do mesmo paciente. Medido em 10/08/2026: 6.370 solicitações de 25.439 ficaram
    /// assim.</para>
    ///
    /// <para>A chave é (CPF, CNS) — <b>a mesma</b> que agrupou a fila. Duas solicitações no mesmo
    /// par são a mesma pessoa por construção; não há heurística nova aqui.</para>
    /// </summary>
    private Task<int> PropagarAsIrmasAsync(
        SerSolicitacao origem, Guid pacienteId, CancellationToken ct) =>
        db.SerSolicitacoes
            .Where(x => x.Id != origem.Id
                        && x.ExcluidoEm == null
                        && x.PacienteId == null
                        && x.Cpf == origem.Cpf
                        && x.Cns == origem.Cns)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.PacienteId, pacienteId), ct);

    /// <summary>
    /// Um id de solicitação por paciente — a mais recentemente sincronizada — <b>e quem tem CPF
    /// primeiro</b>.
    ///
    /// <para><b>A ordem muda o resultado, não só o tempo.</b> Sem CPF a âncora é o CNS, e o CNS
    /// não é uma chave por pessoa: medido no piloto de 10/08/2026, ancorar por CNS deu ~4
    /// duplicatas em cada 10. Processar antes todos os que têm CPF faz esses pacientes chegarem
    /// ao hub pela chave certa; quando a vez dos só-CNS chegar, parte deles já estará lá com CPF,
    /// e a ponte local→CPF do upsert canônico reaproveita o recurso em vez de criar outro.</para>
    ///
    /// <para>O agrupamento é por (CPF, CNS) <b>como o SER escreveu</b>. Não normalizo aqui de
    /// propósito: quem decide se o CPF vale é <c>CpfPep.Valido</c>, dentro da conciliação, e
    /// duplicar essa régua em SQL criaria duas verdades. No pior caso o mesmo paciente entra
    /// duas vezes na fila e a segunda passagem é um no-op.</para>
    /// </summary>
    private async Task<List<Guid>> IdsMaisRecentesPorPacienteAsync(CancellationToken ct)
    {
        var grupos = await db.SerSolicitacoes
            .AsNoTracking()
            .Where(s => s.ExcluidoEm == null
                        && ((s.Cpf != null && s.Cpf != "") || (s.Cns != null && s.Cns != "")))
            .GroupBy(s => new { s.Cpf, s.Cns })
            .Select(g => new
            {
                Id = g
                    .OrderByDescending(x => x.SincronizadoEm)
                    .ThenByDescending(x => x.DataSolicitacao)
                    .Select(x => x.Id)
                    .First(),
                TemCpf = g.Key.Cpf != null && g.Key.Cpf != "",
            })
            .OrderByDescending(x => x.TemCpf)
            .ToListAsync(ct);

        var comCpf = grupos.Count(x => x.TemCpf);
        logger.LogInformation(
            "SER/backfill: {ComCpf} paciente(s) com CPF vêm primeiro; {SoCns} só com CNS depois.",
            comCpf, grupos.Count - comCpf);

        return [.. grupos.Select(x => x.Id)];
    }
}
