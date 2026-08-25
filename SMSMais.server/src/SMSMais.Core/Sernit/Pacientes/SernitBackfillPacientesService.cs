using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Integracoes.Pep;
using SMSMais.Data;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit.Pacientes;

public sealed record BackfillPacientesSernitDto(
    int Pacientes, int Criados, int Enriquecidos, int Inalterados, int SemChave, int Falhas,
    int DuracaoSegundos);

/// <summary>
/// Alinhamento entre a base espelhada do SERNIT e o hub FHIR. <c>ExecutarAsync</c> é o acerto único
/// (todos os pacientes distintos); <c>ExecutarPendentesAsync</c> é o regime permanente (só a fila
/// que a varredura carimbou). Espelho do <c>SerBackfillPacientesService</c>: uma solicitação por
/// paciente (a mais recente), <b>contato só acumula</b> (empresta das irmãs; o hub nunca perde), e
/// quem tem CPF é conciliado primeiro (o CNS não é chave por pessoa).
///
/// <para><b>Gate operacional: o hub não tem undo.</b> Rodar o alinhamento único escreve identidade
/// em milhares de pacientes — só com backup do <c>fhir.patient</c>.</para>
/// </summary>
public interface ISernitBackfillPacientesService
{
    Task<BackfillPacientesSernitDto> ExecutarAsync(int throttleMs, CancellationToken ct);
    Task<BackfillPacientesSernitDto> ExecutarPendentesAsync(int limite, CancellationToken ct);
}

public sealed class SernitBackfillPacientesService(
    SmsMaisDbContext db,
    ISernitConciliacaoPacienteService conciliacao,
    ILogger<SernitBackfillPacientesService> logger) : ISernitBackfillPacientesService
{
    private const int Lote = 200;

    public async Task<BackfillPacientesSernitDto> ExecutarAsync(int throttleMs, CancellationToken ct)
    {
        var inicio = DateTime.UtcNow;
        int criados = 0, enriquecidos = 0, inalterados = 0, semChave = 0, falhas = 0, vistos = 0;

        var ids = await IdsMaisRecentesPorPacienteAsync(ct);
        logger.LogInformation(
            "SERNIT/backfill de pacientes: {Qtd} pacientes distintos a conciliar com o hub.", ids.Count);

        foreach (var pagina in ids.Chunk(Lote))
        {
            ct.ThrowIfCancellationRequested();

            var solicitacoes = await db.SernitSolicitacoes
                .Where(s => pagina.Contains(s.Id))
                .ToListAsync(ct);

            foreach (var s in solicitacoes)
            {
                ct.ThrowIfCancellationRequested();
                vistos++;
                try
                {
                    var r = await conciliacao.ConciliarAsync(await ComDadosDasIrmasAsync(s, ct), ct);
                    if (r.PacienteId is { } pid)
                    {
                        s.PacienteId = pid;
                        await PropagarAsIrmasAsync(s, pid, ct);
                    }
                    switch (r.Resultado)
                    {
                        case ResultadoConciliacaoSernit.Criado: criados++; break;
                        case ResultadoConciliacaoSernit.Enriquecido: enriquecidos++; break;
                        case ResultadoConciliacaoSernit.Inalterado: inalterados++; break;
                        case ResultadoConciliacaoSernit.SemChave: semChave++; break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    falhas++;
                    logger.LogWarning(ex, "SERNIT/backfill: falhou no paciente da solicitação {Id}.", s.IdSernit);
                }

                if (throttleMs > 0) await Task.Delay(throttleMs, ct);
            }

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "SERNIT/backfill: {Vistos}/{Total} — {Criados} criados, {Enriq} enriquecidos, "
                + "{Inalt} inalterados, {SemChave} sem chave, {Falhas} falhas.",
                vistos, ids.Count, criados, enriquecidos, inalterados, semChave, falhas);
        }

        if (falhas == 0)
        {
            await db.SernitSolicitacoes
                .Where(s => s.PacienteConciliarEm != null)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.PacienteConciliarEm, (DateTime?)null), ct);
        }

        var duracao = (int)(DateTime.UtcNow - inicio).TotalSeconds;
        logger.LogInformation("SERNIT/backfill de pacientes: terminado em {Seg}s.", duracao);

        return new BackfillPacientesSernitDto(
            ids.Count, criados, enriquecidos, inalterados, semChave, falhas, duracao);
    }

    public async Task<BackfillPacientesSernitDto> ExecutarPendentesAsync(int limite, CancellationToken ct)
    {
        var inicio = DateTime.UtcNow;
        int criados = 0, enriquecidos = 0, inalterados = 0, semChave = 0, falhas = 0;

        var pendentes = await db.SernitSolicitacoes
            .Where(s => s.ExcluidoEm == null && s.PacienteConciliarEm != null)
            .OrderBy(s => s.PacienteConciliarEm)
            .Take(limite)
            .ToListAsync(ct);

        if (pendentes.Count == 0) return new BackfillPacientesSernitDto(0, 0, 0, 0, 0, 0, 0);

        foreach (var s in pendentes)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var r = await conciliacao.ConciliarAsync(await ComDadosDasIrmasAsync(s, ct), ct);
                switch (r.Resultado)
                {
                    case ResultadoConciliacaoSernit.Criado: criados++; break;
                    case ResultadoConciliacaoSernit.Enriquecido: enriquecidos++; break;
                    case ResultadoConciliacaoSernit.Inalterado: inalterados++; break;
                    case ResultadoConciliacaoSernit.SemChave: semChave++; break;
                }

                if (r.PacienteId is { } pid)
                {
                    s.PacienteId = pid;
                    await PropagarAsIrmasAsync(s, pid, ct);
                }

                // Limpa a marca (vale inclusive para "sem chave"): se o SERNIT trouxer CPF/CNS
                // depois, o retrato do paciente muda e a varredura remarca sozinha.
                s.PacienteConciliarEm = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                // Vai para o FIM da fila (re-carimba), não para a cabeça — retenta sem bloquear os outros.
                s.PacienteConciliarEm = DateTime.UtcNow;
                logger.LogWarning(
                    ex, "SERNIT/conciliação: falhou no paciente da solicitação {Id}. Recolocado no FIM da fila.",
                    s.IdSernit);
            }
        }

        await db.SaveChangesAsync(ct);

        var duracao = (int)(DateTime.UtcNow - inicio).TotalSeconds;
        logger.LogInformation(
            "SERNIT/conciliação: {Qtd} pendente(s) — {Criados} criados, {Enriq} enriquecidos, "
            + "{Inalt} inalterados, {SemChave} sem chave, {Falhas} falhas em {Seg}s.",
            pendentes.Count, criados, enriquecidos, inalterados, semChave, falhas, duracao);

        return new BackfillPacientesSernitDto(
            pendentes.Count, criados, enriquecidos, inalterados, semChave, falhas, duracao);
    }

    /// <summary>A solicitação com o que só as irmãs sabem (telefones e CPF), sem tocar no espelho.
    /// Irmã é quem compartilha a CNS (ou, na falta, o CPF válido). Cópia destacada, nunca a
    /// entidade rastreada — o espelho é somente-leitura.</summary>
    private async Task<SernitSolicitacao> ComDadosDasIrmasAsync(SernitSolicitacao s, CancellationToken ct)
    {
        var faltaZap = string.IsNullOrWhiteSpace(s.TelefoneWhatsapp);
        var faltaContato = string.IsNullOrWhiteSpace(s.TelefoneContato);
        var faltaResidencial = string.IsNullOrWhiteSpace(s.TelefoneResidencial);
        var faltaCpf = !CpfPep.Valido(s.Cpf);
        if (!faltaZap && !faltaContato && !faltaResidencial && !faltaCpf) return s;

        var cns = s.Cns ?? string.Empty;
        var cpfAncora = CpfPep.Valido(s.Cpf) ? s.Cpf : null;
        if (cns.Length == 0 && cpfAncora is null) return s;

        var irmas = await db.SernitSolicitacoes
            .AsNoTracking()
            .Where(x => x.Id != s.Id && x.ExcluidoEm == null
                        && (cns.Length > 0 ? x.Cns == cns : x.Cpf == cpfAncora))
            .OrderByDescending(x => x.SincronizadoEm)
            .ThenByDescending(x => x.DataSolicitacao)
            .Select(x => new { x.Cpf, x.TelefoneWhatsapp, x.TelefoneContato, x.TelefoneResidencial })
            .ToListAsync(ct);

        if (irmas.Count == 0) return s;

        var copia = (SernitSolicitacao)db.Entry(s).CurrentValues.ToObject();
        if (faltaZap)
            copia.TelefoneWhatsapp = irmas.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.TelefoneWhatsapp))?.TelefoneWhatsapp;
        if (faltaContato)
            copia.TelefoneContato = irmas.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.TelefoneContato))?.TelefoneContato;
        if (faltaResidencial)
            copia.TelefoneResidencial = irmas.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.TelefoneResidencial))?.TelefoneResidencial;
        if (faltaCpf)
            copia.Cpf = irmas.FirstOrDefault(x => CpfPep.Valido(x.Cpf))?.Cpf;
        return copia;
    }

    /// <summary>Espalha o paciente resolvido para as outras solicitações da mesma pessoa (chave
    /// (CPF, CNS) — a mesma que agrupou a fila).</summary>
    private Task<int> PropagarAsIrmasAsync(SernitSolicitacao origem, Guid pacienteId, CancellationToken ct) =>
        db.SernitSolicitacoes
            .Where(x => x.Id != origem.Id
                        && x.ExcluidoEm == null
                        && x.PacienteId == null
                        && x.Cpf == origem.Cpf
                        && x.Cns == origem.Cns)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.PacienteId, pacienteId), ct);

    /// <summary>Um id por paciente (a mais recente), com quem tem CPF primeiro — o CNS não é chave
    /// por pessoa, então processar os CPF antes faz eles chegarem ao hub pela chave certa.</summary>
    private async Task<List<Guid>> IdsMaisRecentesPorPacienteAsync(CancellationToken ct)
    {
        var grupos = await db.SernitSolicitacoes
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
            "SERNIT/backfill: {ComCpf} paciente(s) com CPF vêm primeiro; {SoCns} só com CNS depois.",
            comCpf, grupos.Count - comCpf);

        return [.. grupos.Select(x => x.Id)];
    }
}
