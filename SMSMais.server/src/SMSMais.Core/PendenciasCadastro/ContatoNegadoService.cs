using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Conversas;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.PendenciasCadastro;

/// <summary>
/// Régua ÚNICA do contato negado, para o bloqueio central de mensagens automáticas (LGPD):
/// quem atende um número disse que NÃO conhece o paciente ⇒ nada automático sobre ESSE paciente
/// sai para ESSE número — confirmação, resultado, laudo, pesquisa de satisfação, TFD, robô.
///
/// <para>O bloqueio é do <b>par número × paciente</b> (decisão do produto em 17/09/2026): o dono
/// legítimo do número continua recebendo o que é dele. A exceção é a pendência registrada sem
/// paciente ("número errado" genérico), que bloqueia todo automático daquele número.</para>
///
/// <para>Duas fontes, porque nenhuma sozinha cobre: a <b>pendência aberta</b> (fila da recepção,
/// some quando resolvem) e o <b>carimbo no telecom FHIR</b> (fica no cadastro até o número ser
/// trocado ou a denúncia dada por improcedente).</para>
/// </summary>
public interface IContatoNegadoService
{
    /// <summary>Este número está negado para este paciente (ou negado de forma genérica)?</summary>
    Task<bool> BloqueadoAsync(string? telefone, Guid? pacienteId, CancellationToken ct = default);
}

public sealed class ContatoNegadoService(
    SmsMaisDbContext db,
    IPacienteFhirClient fhir,
    Microsoft.Extensions.Logging.ILogger<ContatoNegadoService> logger) : IContatoNegadoService
{
    public async Task<bool> BloqueadoAsync(string? telefone, Guid? pacienteId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return false;

        // 1. Pendência ABERTA de número errado: do paciente, ou genérica (sem paciente).
        var abertas = await db.PendenciasCadastro.AsNoTracking()
            .Where(p => p.Status == StatusPendenciaCadastro.Aberta
                && p.Tipo == TipoPendenciaCadastro.NumeroErrado
                && (p.PacienteId == null || p.PacienteId == pacienteId))
            .Select(p => p.TelefoneCanonical)
            .ToListAsync(ct);
        if (abertas.Any(t => TelefoneWhatsApp.MesmoNumero(t, telefone))) return true;

        // 2. Carimbo no cadastro (sobrevive à baixa da pendência). Best-effort: hub fora do ar
        //    não pode derrubar o envio — a pendência acima já é a trava dura.
        if (pacienteId is not { } id) return false;
        try
        {
            var patient = await fhir.ObterAsync(id, ct);
            if (patient is null) return false;
            var negado = PatientMergeFhir.TelefoneNegado(patient);
            return negado is { } n && TelefoneWhatsApp.MesmoNumero(n.Numero, telefone);
        }
        catch (Exception ex)
        {
            Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(
                logger, ex, "Não foi possível conferir o contato negado do paciente {Paciente}.", id);
            return false;
        }
    }
}
