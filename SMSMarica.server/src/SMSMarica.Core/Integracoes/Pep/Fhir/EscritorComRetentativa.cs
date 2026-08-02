using System.Diagnostics;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Integracoes.Pep.Progresso;

namespace SMSMarica.Core.Integracoes.Pep.Fhir;

/// <summary>
/// Decorator do <see cref="IHubFhirEscritor"/> que reenvia chamadas que falharam por
/// motivo TRANSITÓRIO (saturação de conexão no hub → 503/502/504/408/429, timeout,
/// falha de transporte) com backoff exponencial + jitter, até esgotar o budget. O
/// registro NUNCA é descartado por saturação: fica "pendente" e é reempurrado até o
/// banco liberar. Só vira falha definitiva se estourar o budget ou se o erro for
/// permanente (400/404/422/500 = dado inválido/bug).
///
/// <para><b>Idempotência:</b> POSTs (criar) só são retentados em <b>503 explícito</b>
/// (saturação acontece ANTES de gravar — seguro), nunca em erro de transporte
/// ambíguo, pra não duplicar recurso. GET/PUT/DELETE são idempotentes e podem
/// retentar qualquer transitório.</para>
/// </summary>
public sealed class EscritorComRetentativa(
    IHubFhirEscritor inner, ProgressoImportacao progresso, TimeSpan budget, ILogger logger)
    : IHubFhirEscritor
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(2);

    public Task<Resource> CriarAsync(Resource recurso, CancellationToken ct = default) =>
        Com(c => inner.CriarAsync(recurso, c), idempotente: false, ct);

    public Task<Resource> AtualizarAsync(string tipo, string id, Resource recurso, CancellationToken ct = default) =>
        Com(c => inner.AtualizarAsync(tipo, id, recurso, c), idempotente: true, ct);

    public Task<Resource> UpsertPorIdentifierAsync(Resource recurso, string system, string value, CancellationToken ct = default) =>
        Com(c => inner.UpsertPorIdentifierAsync(recurso, system, value, c), idempotente: true, ct);

    public Task ExcluirAsync(string tipo, string id, CancellationToken ct = default) =>
        Com(async c => { await inner.ExcluirAsync(tipo, id, c); return 0; }, idempotente: true, ct);

    public Task<Bundle> BuscarPorPacienteAsync(string tipo, string pacienteId, CancellationToken ct = default) =>
        Com(c => inner.BuscarPorPacienteAsync(tipo, pacienteId, c), idempotente: true, ct);

    public Task<Bundle> BuscarPorIdentifierAsync(string tipo, string system, string value, CancellationToken ct = default) =>
        Com(c => inner.BuscarPorIdentifierAsync(tipo, system, value, c), idempotente: true, ct);

    public Task<Bundle> ListarAsync(string tipo, CancellationToken ct = default) =>
        Com(c => inner.ListarAsync(tipo, c), idempotente: true, ct);

    public Task<string> ObterEstatisticasAsync(string source, CancellationToken ct = default) =>
        Com(c => inner.ObterEstatisticasAsync(source, c), idempotente: true, ct);

    private async Task<T> Com<T>(Func<CancellationToken, Task<T>> op, bool idempotente, CancellationToken ct)
    {
        var inicio = Stopwatch.GetTimestamp();
        var tentativa = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await op(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && DeveRetentar(ex, idempotente))
            {
                if (Stopwatch.GetElapsedTime(inicio) >= budget)
                {
                    logger.LogWarning(ex,
                        "[hub-retry] budget de {Budget}s esgotado após {N} tentativa(s) — vira falha durável.",
                        budget.TotalSeconds, tentativa);
                    throw;
                }
                tentativa++;
                Interlocked.Increment(ref progresso.Retentativas);
                await Task.Delay(ProximoDelay(tentativa), ct);
            }
        }
    }

    private static bool DeveRetentar(Exception ex, bool idempotente)
    {
        // 503/502/504/408/429: o hub sinaliza saturação ANTES de gravar — seguro até em POST.
        if (ex is HubFhirHttpException h) return h.Transitorio;
        // POST não idempotente: não retenta erro de transporte ambíguo (poderia duplicar).
        if (!idempotente) return false;
        return ex is TaskCanceledException or TimeoutException
            || (ex is HttpRequestException hre && hre.StatusCode is null); // sem resposta = falha de transporte
    }

    private static TimeSpan ProximoDelay(int tentativa)
    {
        var exp = BaseDelay.TotalMilliseconds * Math.Pow(2, tentativa - 1);
        var capado = Math.Min(exp, MaxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capado + Random.Shared.Next(0, 250)); // jitter anti-thundering-herd
    }
}
