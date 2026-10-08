using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Alertas;
using SMSMais.Core.Common.Tempo;

namespace SMSMais.Core.Integracoes.Sisreg.Base;

/// <summary>
/// Uma vez por dia, na <see cref="FrescorBaseSisregOpcoes.HoraDoAviso"/>, confere a base do SISREG
/// (<see cref="IFrescorBaseSisregService"/>) e, se faltar alguma coisa, avisa no celular pela lista
/// única de avisos. Pedido do Bernardo em 07/10/2026: "preciso saber quando isso estiver
/// desatualizado — isso precisa me ser notificado".
///
/// <para>Uma mensagem por dia, com o resumo, em vez de uma por falha: o SISREG corta metade das
/// leituras de faltas e a rodada seguinte fecha — avisar cada corte seria barulho que ensina a
/// ignorar. O que importa é o dia que continua sem lista e a unidade que deixou de ser relida.</para>
/// </summary>
public sealed class AvisoFrescorBaseSisregWorker(
    IServiceScopeFactory scopes,
    IAlertaPlataforma alerta,
    IOptions<FrescorBaseSisregOpcoes> opcoes,
    ILogger<AvisoFrescorBaseSisregWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Quantos dias e unidades a mensagem cita; o resto vira "e mais N".</summary>
    private const int Citados = 6;

    private DateOnly? _avisadoEm;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Error: se a conferência quebrar, isso também tem de chegar ao celular.
                logger.LogError(ex, "SISREG_FRESCOR: a conferência diária da base falhou.");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var o = opcoes.Value;
        if (!o.AvisoLigado) return;

        var agora = FusoBrasilia.ParaExibicao(DateTime.UtcNow);
        var hoje = DateOnly.FromDateTime(agora);
        if (_avisadoEm == hoje || TimeOnly.FromDateTime(agora) < o.HoraDoAviso) return;

        using var scope = scopes.CreateScope();
        var frescor = await scope.ServiceProvider.GetRequiredService<IFrescorBaseSisregService>()
            .ConferirAsync(hoje.AddDays(-Math.Max(1, o.DiasDoAviso)), hoje.AddDays(-1), null, ct);
        _avisadoEm = hoje;

        if (frescor.EmDia)
        {
            logger.LogInformation("SISREG_FRESCOR: base em dia — faltas lidas e chegadas relidas nos últimos {Dias} dias.",
                o.DiasDoAviso);
            return;
        }

        var (titulo, detalhe) = Montar(frescor);
        logger.LogInformation("SISREG_FRESCOR: {Titulo} — {Detalhe}", titulo, detalhe);
        alerta.Reportar(new EventoAlerta(AlertaCatalogo.SisregFrescor, titulo, detalhe)
        {
            Rotulo = "Base do SISREG desatualizada",
            Grupo = "Sincronismo",
        });
    }

    /// <summary>O texto do aviso. Público para teste: é o que o Bernardo lê no celular.</summary>
    public static (string Titulo, string Detalhe) Montar(FrescorBaseSisregDto f)
    {
        var partes = new List<string>();
        if (f.DiasSemFaltas.Count > 0)
            partes.Add($"{f.DiasSemFaltas.Count} dia(s) sem a lista de faltas");
        if (f.ChegadasAtrasadas.Count > 0)
            partes.Add($"{f.ChegadasAtrasadas.Count} unidade(s) sem releitura de chegada");
        var titulo = string.Join(" e ", partes);

        var sb = new StringBuilder();
        if (f.DiasSemFaltas.Count > 0)
        {
            sb.Append("Faltas não lidas: ");
            sb.Append(string.Join(", ", f.DiasSemFaltas.Take(Citados).Select(d =>
                $"{d.Dia.ToString("dd/MM", Br)} ({d.Agendamentos} em aberto, "
                + (d.UltimaLeitura is { } u ? $"última leitura {Quando(u)}" : "nunca lido") + ")")));
            if (f.DiasSemFaltas.Count > Citados) sb.Append($" e mais {f.DiasSemFaltas.Count - Citados}");
            sb.Append(". ");
        }
        if (f.ChegadasAtrasadas.Count > 0)
        {
            sb.Append($"Chegadas não relidas há mais de {f.HorasParaAtraso} h: ");
            sb.Append(string.Join(", ", f.ChegadasAtrasadas.Take(Citados).Select(c =>
                $"{c.Unidade} ({c.Agendamentos} em aberto, "
                + (c.UltimaLeitura is { } u ? $"desde {Quando(u)}" : "nunca relidas") + ")")));
            if (f.ChegadasAtrasadas.Count > Citados) sb.Append($" e mais {f.ChegadasAtrasadas.Count - Citados}");
            sb.Append(". ");
        }
        sb.Append("Enquanto isso, SISREG → Consultar mostra esses atendimentos como \"Pendente de atualização\".");
        return (titulo, sb.ToString());
    }

    private static string Quando(DateTime utc) =>
        FusoBrasilia.ParaExibicao(utc).ToString("dd/MM HH:mm", Br);
}
