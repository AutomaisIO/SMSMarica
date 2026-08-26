using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Informa o horário de atendimento. Usa o horário configurado no assunto (HorarioInicio/Fim +
/// DiasSemana); se não houver, dá uma orientação genérica (procurar o posto).
/// </summary>
public sealed class InformarHorarioAtendimentoComando(SmsMaisDbContext db) : IRoboComando
{
    private static readonly string[] Dias = ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];

    public ComandoRobo Comando => ComandoRobo.InformarHorarioAtendimento;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:horario";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        var a = ctx.AssuntoId is { } id
            ? await db.RoboAssuntos.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.HorarioInicio, x.HorarioFim, x.DiasSemana }).FirstOrDefaultAsync(ct)
            : null;

        if (a?.HorarioInicio is { } ini && a.HorarioFim is { } fim)
        {
            var quando = $"{FormatarDias(a.DiasSemana)}, das {ini.ToString("HH\\hmm", CultureInfo.InvariantCulture)} "
                + $"às {fim.ToString("HH\\hmm", CultureInfo.InvariantCulture)}";
            return new(true, $"Informe que o atendimento humano funciona {quando}.");
        }

        return new(true,
            "Informe que o atendimento humano funciona em horário comercial; para detalhes e o local, oriente a procurar "
            + "o posto de saúde onde a pessoa é cadastrada.");
    }

    private static string FormatarDias(int? mask)
    {
        if (mask is not { } m || m == 0) return "todos os dias";
        var marcados = Enumerable.Range(0, 7).Where(b => (m & (1 << b)) != 0).ToList();
        if (marcados.Count == 7) return "todos os dias";
        if (marcados.SequenceEqual([1, 2, 3, 4, 5])) return "de segunda a sexta";
        return string.Join(", ", marcados.Select(b => Dias[b]));
    }
}
