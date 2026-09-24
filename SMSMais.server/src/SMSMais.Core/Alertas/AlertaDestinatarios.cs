using Microsoft.EntityFrameworkCore;
using SMSMais.Data;

namespace SMSMais.Core.Alertas;

/// <summary>
/// A ÚNICA lista de quem recebe aviso de erro e falha da plataforma: os telefones de
/// Sistema → Avisos no celular (<c>alerta_destinatario</c>).
///
/// <para><b>Por que uma só:</b> houve duas — a da plataforma e uma por integração (na tela do
/// SISREG). O operador cadastrou o celular na do SISREG e, de 22 a 24/09/2026, o robô ficou mudo
/// por limite da conta da IA com 2.377 avisos gerados e nenhum entregue: a lista da plataforma
/// estava vazia. Quem cadastra num lugar espera receber tudo dali. Não crie outra lista.</para>
/// </summary>
public interface IAlertaDestinatarios
{
    /// <summary>Telefones ativos, só dígitos, sem repetição (compara os 10 últimos dígitos).</summary>
    Task<IReadOnlyList<string>> ListarAtivosAsync(CancellationToken ct = default);
}

public sealed class AlertaDestinatarios(SmsMaisDbContext db) : IAlertaDestinatarios
{
    public async Task<IReadOnlyList<string>> ListarAtivosAsync(CancellationToken ct = default)
    {
        var telefones = await db.AlertaDestinatarios.AsNoTracking()
            .Where(d => d.Ativo)
            .Select(d => d.Telefone)
            .ToListAsync(ct);

        return [.. telefones
            .Select(SoDigitos)
            .Where(t => t.Length >= 10)
            .DistinctBy(t => t[^10..])];
    }

    private static string SoDigitos(string? valor) =>
        new([.. (valor ?? string.Empty).Where(char.IsDigit)]);
}
