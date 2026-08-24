using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Pacientes.Fhir;

namespace SMSMais.Core.Pacientes.Promocao;

public sealed record PromocaoResultado(int Total, int Promovidos, int Erros);

/// <summary>
/// Backfill (ADR-0020 R4): promove a demografia de TODOS os pacientes do blob para campos FHIR
/// nativos (idempotente e resumível pelo marcador <c>urn:smsmarica:promovido</c>). Grava com
/// If-Match (concorrência otimista) e throttle. Os telefones confirmados já vivem SÓ no telecom
/// do Patient (a tabela contato_validado foi aposentada) — nada a re-carimbar aqui.
/// GATE operacional: rodar com <b>backup do fhir.patient</b> (o hub não tem undo).
/// </summary>
public interface IPromocaoBlobService
{
    Task<PromocaoResultado> PromoverTodosAsync(int throttleMs = 25, CancellationToken ct = default);
}

public sealed class PromocaoBlobService(
    IPacienteFhirClient fhir,
    ILogger<PromocaoBlobService> logger) : IPromocaoBlobService
{
    private const int TamanhoPagina = 200;

    public async Task<PromocaoResultado> PromoverTodosAsync(int throttleMs = 25, CancellationToken ct = default)
    {
        Guid? cursor = null;
        int total = 0, promovidos = 0, erros = 0;
        logger.LogInformation("Backfill blob→nativo iniciado (throttle {Throttle}ms).", throttleMs);

        while (!ct.IsCancellationRequested)
        {
            var bundle = await fhir.ListarParaManutencaoAsync(cursor, TamanhoPagina, ct);
            var patients = bundle.Entry.Select(e => e.Resource).OfType<Patient>()
                .Where(p => p.Id is not null).ToList();
            if (patients.Count == 0) break;

            foreach (var p in patients)
            {
                total++;
                try
                {
                    var promoveu = await PromoverUmAsync(p, ct);
                    if (promoveu) promovidos++;
                    // Throttle SÓ quando houve escrita (a varredura dos "pulados" não carrega o hub).
                    if (promoveu && throttleMs > 0) await Task.Delay(throttleMs, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    erros++;
                    logger.LogWarning(ex, "Falha ao promover Patient/{Id} no backfill.", p.Id);
                }
            }

            cursor = Guid.Parse(patients[^1].Id!);
            if (bundle.Link.All(l => l.Relation != "next")) break;
            logger.LogInformation("Backfill: {Total} vistos, {Prom} promovidos, {Err} erros.",
                total, promovidos, erros);
        }

        logger.LogInformation("Backfill CONCLUÍDO: {Total} vistos, {Prom} promovidos, {Err} erros.",
            total, promovidos, erros);
        return new PromocaoResultado(total, promovidos, erros);
    }

    private async Task<bool> PromoverUmAsync(Patient p, CancellationToken ct)
    {
        var id = Guid.Parse(p.Id!);
        for (var tentativa = 1; ; tentativa++)
        {
            var promoveu = PacienteFhirMapper.PromoverBlobParaNativo(p);
            if (!promoveu) return false;
            try
            {
                await fhir.AtualizarAsync(id, p, ct); // If-Match embutido no cliente
                return true;
            }
            catch (ConflitoVersaoHubException) when (tentativa < 3)
            {
                p = await fhir.ObterAsync(id, ct) ?? p; // re-lê e reaplica
            }
        }
    }
}
