using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>Escolhe o assunto ativo cujas condições casam com a mensagem (pré-match barato, sem IA).</summary>
public interface IRoboClassificador
{
    Task<Guid?> ClassificarAsync(string? texto, CancellationToken ct);

    /// <summary>
    /// Devolve o assunto a usar no turno, já com treinos e comandos carregados: o informado, ou o
    /// classificado pela mensagem, ou — se nada casar — o marcado como PADRÃO. Atendimento real e
    /// simulação chamam este mesmo método justamente para não divergirem.
    /// </summary>
    Task<RoboAssunto?> ResolverAsync(Guid? assuntoId, string? texto, CancellationToken ct);
}

public sealed class RoboClassificador(SmsMaisDbContext db, ILogger<RoboClassificador> logger) : IRoboClassificador
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    public async Task<Guid?> ClassificarAsync(string? texto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var alvo = Normalizar(texto);

        var assuntos = await db.RoboAssuntos.AsNoTracking()
            .Where(a => a.Ativo && a.ExcluidoEm == null)
            .OrderBy(a => a.Ordem).ThenBy(a => a.Nome)
            .Select(a => new
            {
                a.Id,
                Condicoes = a.Condicoes.Where(c => c.Ativo).Select(c => new { c.Tipo, c.Valor }).ToList(),
            })
            .ToListAsync(ct);

        foreach (var a in assuntos)
        {
            foreach (var c in a.Condicoes)
            {
                if (Casou(alvo, texto, c.Tipo, c.Valor)) return a.Id;
            }
        }
        return null;
    }

    public async Task<RoboAssunto?> ResolverAsync(Guid? assuntoId, string? texto, CancellationToken ct)
    {
        var id = assuntoId ?? await ClassificarAsync(texto, ct);
        var assunto = id is { } alvo ? await CarregarAsync(a => a.Id == alvo, ct) : null;

        // Nada casou. Antes isso deixava o robô SEM orientação, sem treinos, sem limiar e sem os
        // comandos do assunto — e é onde cai a maior parte do que o classificador não prevê. O
        // assunto padrão é a rede: se existir um marcado, ele assume; se não, o comportamento é
        // exatamente o de antes.
        return assunto ?? await CarregarAsync(a => a.Padrao, ct);
    }

    private Task<RoboAssunto?> CarregarAsync(Expression<Func<RoboAssunto, bool>> filtro, CancellationToken ct) =>
        db.RoboAssuntos.AsNoTracking()
            .Include(a => a.Treinos)
            .Include(a => a.Comandos)
            .Where(a => a.Ativo && a.ExcluidoEm == null)
            .FirstOrDefaultAsync(filtro, ct);

    private bool Casou(string alvoNorm, string textoOriginal, TipoCondicaoRobo tipo, string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;
        switch (tipo)
        {
            case TipoCondicaoRobo.PalavraChave:
            case TipoCondicaoRobo.Frase:
                return alvoNorm.Contains(Normalizar(valor), StringComparison.Ordinal);
            case TipoCondicaoRobo.Regex:
                try
                {
                    return Regex.IsMatch(textoOriginal, valor,
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
                }
                catch (Exception ex) when (ex is RegexParseException or RegexMatchTimeoutException)
                {
                    logger.LogWarning(ex, "Condição regex inválida/lenta ignorada: {Valor}", valor);
                    return false;
                }
            default:
                return false;
        }
    }

    private static string Normalizar(string s)
    {
        var semAcento = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(semAcento.Length);
        foreach (var ch in semAcento)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Trim();
    }
}
