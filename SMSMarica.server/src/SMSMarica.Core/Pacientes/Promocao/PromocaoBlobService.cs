using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Data;

namespace SMSMarica.Core.Pacientes.Promocao;

public sealed record PromocaoResultado(int Total, int Promovidos, int Confirmados, int Erros);

/// <summary>
/// Backfill (ADR-0020 R4): promove a demografia de TODOS os pacientes do blob para campos FHIR
/// nativos (idempotente e resumível pelo marcador <c>urn:smsmarica:promovido</c>) e re-carimba os
/// telefones já confirmados (<c>contato_validado</c>) no FHIR. Grava com If-Match (concorrência
/// otimista) e throttle. GATE operacional: rodar com <b>backup do fhir.patient</b> (o hub não tem undo).
/// </summary>
public interface IPromocaoBlobService
{
    Task<PromocaoResultado> PromoverTodosAsync(int throttleMs = 25, CancellationToken ct = default);
}

public sealed class PromocaoBlobService(
    IPacienteFhirClient fhir,
    SmsMaricaDbContext db,
    ILogger<PromocaoBlobService> logger) : IPromocaoBlobService
{
    private const int TamanhoPagina = 200;

    public async Task<PromocaoResultado> PromoverTodosAsync(int throttleMs = 25, CancellationToken ct = default)
    {
        Guid? cursor = null;
        int total = 0, promovidos = 0, confirmados = 0, erros = 0;
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
                    var (promoveu, carimbou) = await PromoverUmAsync(p, ct);
                    if (promoveu) promovidos++;
                    if (carimbou) confirmados++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    erros++;
                    logger.LogWarning(ex, "Falha ao promover Patient/{Id} no backfill.", p.Id);
                }
                if (throttleMs > 0) await Task.Delay(throttleMs, ct);
            }

            cursor = Guid.Parse(patients[^1].Id!);
            if (bundle.Link.All(l => l.Relation != "next")) break;
            logger.LogInformation("Backfill: {Total} vistos, {Prom} promovidos, {Conf} confirmados, {Err} erros.",
                total, promovidos, confirmados, erros);
        }

        logger.LogInformation("Backfill CONCLUÍDO: {Total} vistos, {Prom} promovidos, {Conf} confirmados, {Err} erros.",
            total, promovidos, confirmados, erros);
        return new PromocaoResultado(total, promovidos, confirmados, erros);
    }

    private async Task<(bool Promoveu, bool Carimbou)> PromoverUmAsync(Patient p, CancellationToken ct)
    {
        var id = Guid.Parse(p.Id!);
        for (var tentativa = 1; ; tentativa++)
        {
            var promoveu = PacienteFhirMapper.PromoverBlobParaNativo(p);
            var carimbou = await ReCarimbarConfirmadoAsync(p, ct);
            if (!promoveu && !carimbou) return (false, false);
            try
            {
                await fhir.AtualizarAsync(id, p, ct); // If-Match embutido no cliente
                return (promoveu, carimbou);
            }
            catch (ConflitoVersaoHubException) when (tentativa < 3)
            {
                p = await fhir.ObterAsync(id, ct) ?? p; // re-lê e reaplica
            }
        }
    }

    /// <summary>Re-carimba no FHIR o telefone confirmado (contato_validado) que ainda não estava marcado.</summary>
    private async Task<bool> ReCarimbarConfirmadoAsync(Patient p, CancellationToken ct)
    {
        var cpf = Digitos(p.Identifier?.FirstOrDefault(i => i.System == PatientMergeFhir.SystemCpf)?.Value);
        if (cpf.Length != 11) return false;

        var numero = await db.ContatosValidados.AsNoTracking()
            .Where(c => c.Cpf == cpf).Select(c => c.Numero).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(numero)) return false;

        var jaMarcado = (p.Telecom ?? []).Any(t =>
            t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null
            && MesmoNumero(Digitos(t.Value), Digitos(numero)));
        if (jaMarcado) return false;

        PatientMergeFhir.MarcarTelefoneConfirmado(p, numero, DateTimeOffset.UtcNow);
        return true;
    }

    private static string Digitos(string? v) => string.IsNullOrEmpty(v) ? string.Empty : new([.. v.Where(char.IsDigit)]);

    private static bool MesmoNumero(string a, string b) =>
        a.Length >= 8 && b.Length >= 8
        && (a.EndsWith(b, StringComparison.Ordinal) || b.EndsWith(a, StringComparison.Ordinal));
}
