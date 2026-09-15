using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Extensao;

/// <summary>Resultado de uma passada do processador (para o disparo manual / futura agenda).</summary>
public sealed record ProcessamentoResultado(
    int CancelamentosProcessados,
    int CancelamentosAplicados,
    int CancelamentosSemCorrespondencia);

public interface IProcessadorCapturasSisreg
{
    /// <summary>Trata as capturas ainda não processadas (fase 1: só cancelamento). Idempotente:
    /// marca cada captura com <c>ProcessadoEm</c>.</summary>
    Task<ProcessamentoResultado> ProcessarAsync(CancellationToken ct = default);
}

/// <summary>
/// Aplica na base as AÇÕES observadas pela extensão (fase 1: cancelamento).
/// Lê as capturas cruas que já temos, correlaciona e atualiza a Solicitação — reusando o modelo
/// existente. A marcação (que precisa ler o número na resposta) entra numa próxima fatia.
/// </summary>
public sealed class ProcessadorCapturasSisreg(SmsMaisDbContext db) : IProcessadorCapturasSisreg
{
    private const string CaminhoCancelamento = "/cgi-bin/cons_verificar";
    private const string EtapaCancelamento = "EXCLUIR_SOLICITACAO";

    public async Task<ProcessamentoResultado> ProcessarAsync(CancellationToken ct = default)
    {
        var aplicados = 0;
        var semCorrespondencia = 0;

        var cancelamentos = await db.SisregCapturasNavegador
            .Where(x => x.ProcessadoEm == null
                && x.Kind == "requisicao"
                && x.Caminho == CaminhoCancelamento
                && x.Etapa == EtapaCancelamento)
            .OrderBy(x => x.CriadoEm)
            .ToListAsync(ct);

        var agora = DateTime.UtcNow;
        foreach (var cap in cancelamentos)
        {
            var (codigo, justificativa) = LerCancelamento(cap.PayloadJson);
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                var solic = await db.Solicitacoes.FirstOrDefaultAsync(
                    s => s.CodigoSolicitacao == codigo && s.ExcluidoEm == null && s.CanceladoEm == null, ct);

                if (solic is not null)
                {
                    solic.Status = StatusSolicitacao.Cancelada;
                    solic.CanceladoEm = agora;
                    solic.CanceladoPorUsuarioId = cap.UsuarioId;
                    solic.MotivoCancelamento = string.IsNullOrWhiteSpace(justificativa)
                        ? "Cancelada no SISREG (observado pela extensão)."
                        : justificativa;
                    solic.AtualizadoEm = agora;
                    solic.AtualizadoPor = cap.UsuarioId;
                    aplicados++;
                }
                else
                {
                    // Já cancelada, excluída, ou solicitação que nunca existiu na nossa base.
                    semCorrespondencia++;
                }
            }

            cap.ProcessadoEm = agora;
        }

        await db.SaveChangesAsync(ct);
        return new ProcessamentoResultado(cancelamentos.Count, aplicados, semCorrespondencia);
    }

    // O envio do EXCLUIR_SOLICITACAO traz codigo_solicitacao (ou co_solic) + justificativa.
    private static (string? Codigo, string? Justificativa) LerCancelamento(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("campos", out var campos) || campos.ValueKind != JsonValueKind.Object)
                return (null, null);
            var codigo = PrimeiroCampo(campos, "codigo_solicitacao") ?? PrimeiroCampo(campos, "co_solic");
            var justificativa = PrimeiroCampo(campos, "justificativa");
            return (SoDigitos(codigo), justificativa);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>Cada campo é um array de strings (form-data). Pega o primeiro valor não-vazio.</summary>
    private static string? PrimeiroCampo(JsonElement campos, string nome)
    {
        if (!campos.TryGetProperty(nome, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        foreach (var v in arr.EnumerateArray())
            if (v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                return v.GetString();
        return null;
    }

    private static string? SoDigitos(string? s) =>
        string.IsNullOrEmpty(s) ? s : new string(s.Where(char.IsDigit).ToArray());
}
