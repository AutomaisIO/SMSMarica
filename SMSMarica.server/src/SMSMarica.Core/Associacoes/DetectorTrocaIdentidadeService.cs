using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Associacoes;

/// <summary>Um par suspeito: quem produziu estudo sem passar pela recepção × quem passou e não produziu.</summary>
public sealed record SuspeitaTrocaDto(
    DateOnly Dia,
    Guid UnidadeId,
    string? UnidadeNome,
    Guid ExameComEstudoId,
    string ExameComEstudoAccession,
    string ExameComEstudoStudyUID,
    DateTime? DataEstudo,
    Guid ExameSemEstudoId,
    string ExameSemEstudoAccession,
    string Explicacao);

public interface IDetectorTrocaIdentidadeService
{
    /// <summary>Varre o dia atrás de pares suspeitos. Só relata — não abre incidente.</summary>
    Task<IReadOnlyList<SuspeitaTrocaDto>> VarrerAsync(DateOnly dia, CancellationToken cancellationToken = default);

    /// <summary>Varre e coloca cada suspeita em quarentena. Devolve quantas foram abertas.</summary>
    Task<int> VarrerEQuarentenarAsync(DateOnly dia, CancellationToken cancellationToken = default);
}

/// <summary>
/// Detector do "par órfão da recepção" — a assinatura da troca de item de worklist.
///
/// <para>Quando a técnica seleciona o item errado, as chaves do estudo voltam <b>coerentes entre
/// si</b> e nenhuma checagem de metadado acusa nada. Mas o rastro fica no mundo real: no fim do
/// dia sobra um paciente que <b>passou pela recepção e não produziu estudo</b>, e outro que
/// <b>produziu estudo sem ter passado pela recepção naquele dia</b>. Esse par, na mesma unidade e
/// no mesmo dia, é quase inequívoco.</para>
///
/// <para>Foi assim no incidente de 11/08/2026: a Patricia ficou autorizada, com item pendente na
/// worklist e sem estudo, enquanto o exame do João constava como realizado.</para>
/// </summary>
public sealed class DetectorTrocaIdentidadeService(
    SmsMaricaDbContext db,
    IQuarentenaIdentidadeService quarentena,
    ILogger<DetectorTrocaIdentidadeService> logger) : IDetectorTrocaIdentidadeService
{
    public async Task<IReadOnlyList<SuspeitaTrocaDto>> VarrerAsync(
        DateOnly dia, CancellationToken cancellationToken = default)
    {
        // O dia é o de BRASÍLIA: usar UtcNow.Date cortaria o expediente às 21h.
        var inicio = FusoBrasilia.DeBrasiliaParaUtc(dia.ToDateTime(TimeOnly.MinValue));
        var fim = inicio.AddDays(1);
        // DataEstudo é wall-clock local (timestamp WITHOUT time zone) — comparar com o mesmo relógio.
        var inicioLocal = dia.ToDateTime(TimeOnly.MinValue);
        var fimLocal = inicioLocal.AddDays(1);

        // Lado A: exame que PRODUZIU estudo no dia. A data que vale é a do DICOM (data real da
        // aquisição), não a de quando o servidor detectou.
        var comEstudo = await db.ExamesImagem.AsNoTracking()
            .Include(e => e.Solicitacao!).ThenInclude(s => s.UnidadeExecutante)
            .Where(e => e.ExcluidoEm == null
                        && e.DataEstudo != null
                        && e.DataEstudo >= inicioLocal && e.DataEstudo < fimLocal
                        && (e.Status == StatusSolicitacaoExame.Realizada
                            || e.Status == StatusSolicitacaoExame.Laudada))
            .ToListAsync(cancellationToken);
        if (comEstudo.Count == 0) return [];

        // Lado B: exame AUTORIZADO na recepção no dia e que continua esperando o equipamento.
        var semEstudo = await db.ExamesImagem.AsNoTracking()
            .Include(e => e.Solicitacao!).ThenInclude(s => s.UnidadeExecutante)
            .Where(e => e.ExcluidoEm == null
                        && e.DataEstudo == null
                        && e.Solicitacao!.AutorizadoEm != null
                        && e.Solicitacao.AutorizadoEm >= inicio && e.Solicitacao.AutorizadoEm < fim
                        && (e.Status == StatusSolicitacaoExame.Solicitada
                            || e.Status == StatusSolicitacaoExame.Enviada
                            || e.Status == StatusSolicitacaoExame.Recebida))
            .ToListAsync(cancellationToken);
        if (semEstudo.Count == 0) return [];

        var suspeitas = new List<SuspeitaTrocaDto>();
        foreach (var a in comEstudo)
        {
            // O que torna o par suspeito é o lado A NÃO ter passado pela recepção no dia: exame
            // realizado por quem não foi atendido é o sinal forte. Quem foi autorizado e fez o
            // exame é o caminho normal e não entra.
            var autorizadoNoDia = a.Solicitacao!.AutorizadoEm is { } aut && aut >= inicio && aut < fim;
            if (autorizadoNoDia) continue;

            var unidade = a.Solicitacao.UnidadeExecutanteId;
            foreach (var b in semEstudo.Where(x => x.Solicitacao!.UnidadeExecutanteId == unidade))
            {
                if (await quarentena.EmQuarentenaAsync(a.StudyInstanceUID, cancellationToken)) continue;
                suspeitas.Add(new SuspeitaTrocaDto(
                    dia, unidade, a.Solicitacao.UnidadeExecutante?.Nome,
                    a.Id, a.AccessionNumber, a.StudyInstanceUID, a.DataEstudo,
                    b.Id, b.AccessionNumber,
                    $"O exame {a.AccessionNumber} consta como realizado em {a.DataEstudo:dd/MM HH:mm}, " +
                    $"mas o paciente não passou pela recepção neste dia. No mesmo dia e na mesma unidade, " +
                    $"o exame {b.AccessionNumber} foi autorizado e continua sem imagens."));
            }
        }
        return suspeitas;
    }

    public async Task<int> VarrerEQuarentenarAsync(DateOnly dia, CancellationToken cancellationToken = default)
    {
        var suspeitas = await VarrerAsync(dia, cancellationToken);
        var abertos = 0;
        // Um estudo pode formar par com mais de um candidato; a quarentena é idempotente por
        // estudo, então o primeiro abre e os demais recaem no mesmo incidente.
        foreach (var s in suspeitas.DistinctBy(x => x.ExameComEstudoStudyUID, StringComparer.Ordinal))
        {
            try
            {
                await quarentena.AbrirAsync(
                    new AbrirIncidenteRequest(s.ExameComEstudoStudyUID, s.Explicacao),
                    automatico: true, cancellationToken);
                abertos++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao abrir incidente automático para o estudo {Uid}.",
                    s.ExameComEstudoStudyUID);
            }
        }
        if (abertos > 0)
            logger.LogWarning("Detector de troca de identidade: {N} exame(s) em quarentena no dia {Dia}.",
                abertos, dia);
        return abertos;
    }
}
