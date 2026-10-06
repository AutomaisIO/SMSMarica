using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Conversas;
using SMSMais.Data;

namespace SMSMais.Core.AgenteIa.WhatsApp;

/// <param name="Telefone">Como está cadastrado em Avisos no celular (só dígitos).</param>
/// <param name="UsuarioId">Usuário que o telefone representa no agente.</param>
public sealed record TelefoneAgenteIa(string Telefone, Guid UsuarioId, string UsuarioNome);

/// <summary>
/// Quais telefones conversam com o Agente IA (ADR-0068): os de Avisos no celular com a chave
/// "Conversa com o Agente IA" ligada, ativos e com usuário vinculado (ativo e não excluído).
/// Lido do banco a cada chamada — é o interruptor: desligar na tela corta na mensagem seguinte.
///
/// <para><b>Comparação pelo número normalizado com o nono dígito.</b> A Meta entrega o
/// <c>from</c> de celular brasileiro às vezes SEM o nono dígito (12 dígitos), e a tela guarda como
/// a pessoa digitou. Comparar texto cru deixaria o agente mudo para o próprio dono.</para>
/// </summary>
public interface ITelefonesAgenteIa
{
    /// <summary>O dono do telefone no agente, ou null se este número não fala com o agente.</summary>
    Task<TelefoneAgenteIa?> ObterAsync(string telefone, CancellationToken ct = default);

    /// <summary>
    /// Todas as grafias (com e sem 55, com e sem nono dígito) dos telefones do agente — para
    /// esconder do módulo Conversas por igualdade com <c>conversa.telefone_canonical</c>.
    /// </summary>
    Task<IReadOnlyList<string>> GrafiasOcultasAsync(CancellationToken ct = default);
}

public sealed class TelefonesAgenteIa(SmsMaisDbContext db) : ITelefonesAgenteIa
{
    public async Task<TelefoneAgenteIa?> ObterAsync(string telefone, CancellationToken ct = default)
    {
        var chave = Chave(telefone);
        if (chave.Length == 0) return null;
        return (await ListarAsync(ct)).FirstOrDefault(t => Chave(t.Telefone) == chave);
    }

    public async Task<IReadOnlyList<string>> GrafiasOcultasAsync(CancellationToken ct = default) =>
        [.. (await ListarAsync(ct)).SelectMany(t => Grafias(t.Telefone)).Distinct()];

    private async Task<List<TelefoneAgenteIa>> ListarAsync(CancellationToken ct) =>
        await db.AlertaDestinatarios.AsNoTracking()
            .Where(d => d.AgenteIa && d.Ativo && d.AgenteUsuarioId != null
                && d.AgenteUsuario!.Ativo && d.AgenteUsuario.ExcluidoEm == null)
            .Select(d => new TelefoneAgenteIa(d.Telefone, d.AgenteUsuarioId!.Value, d.AgenteUsuario!.NomeCompleto))
            .ToListAsync(ct);

    /// <summary>55 + DDD + 9 + 8 dígitos — a forma de comparar.</summary>
    public static string Chave(string? telefone) =>
        string.IsNullOrWhiteSpace(telefone) || !telefone.Any(char.IsDigit)
            ? string.Empty
            : TelefoneWhatsApp.NormalizarNonoDigito(telefone);

    public static IEnumerable<string> Grafias(string telefone)
    {
        var canonico = TelefoneWhatsApp.Canonizar(telefone);
        var com9 = TelefoneWhatsApp.NormalizarNonoDigito(telefone);
        yield return canonico;
        yield return com9;
        if (com9.Length == 13 && com9.StartsWith("55", StringComparison.Ordinal) && com9[4] == '9')
            yield return com9[..4] + com9[5..];
    }
}
