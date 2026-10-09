using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Alertas;
using SMSMais.Core.Common.Tempo;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.Sisreg.Base;

/// <summary>
/// Confere a base do SISREG (<see cref="IFrescorBaseSisregService"/>) de hora em hora, entre
/// <see cref="FrescorBaseSisregOpcoes.HoraDoAviso"/> e <see cref="FrescorBaseSisregOpcoes.HoraFimDoAviso"/>,
/// e avisa no celular pela lista única de avisos. Pedido do Bernardo em 07/10/2026: "preciso saber
/// quando isso estiver desatualizado — isso precisa me ser notificado".
///
/// <para><b>Quando manda</b> (<see cref="Decidir"/>): na primeira conferência do dia, se houver
/// pendência (o resumo para quem vai abrir o relatório); em qualquer hora, quando aparece pendência
/// NOVA (um dia ou uma unidade que não estava no último aviso); e quando tudo volta ao normal depois
/// de um aviso. A mesma pendência não é repetida de hora em hora — o SISREG corta metade das
/// leituras e a rodada seguinte fecha; avisar cada corte seria barulho que ensina a ignorar.</para>
///
/// <para><b>Reinício do servidor</b> (<see cref="RestaurarEstado"/>). O que já foi avisado vive em
/// memória; sem restaurar, cada deploy repetia o resumo do dia (09/10/2026: 08:28 e 11:01, os dois
/// por deploy, durante a recuperação da troca de IP do SISREG). Na primeira conferência depois de
/// subir, a última ocorrência de cada chave em <c>alerta_origem</c> diz se o resumo de hoje já saiu e
/// se a última palavra foi "desatualizada" — aí as pendências atuais contam como já avisadas. O custo:
/// pendência surgida entre o último aviso e o reinício só avisa se sair e voltar.</para>
/// </summary>
public sealed class AvisoFrescorBaseSisregWorker(
    IServiceScopeFactory scopes,
    IAlertaPlataforma alerta,
    IOptions<FrescorBaseSisregOpcoes> opcoes,
    ILogger<AvisoFrescorBaseSisregWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Quantos dias e unidades a mensagem cita; o resto vira "e mais N".</summary>
    private const int Citados = 6;

    private DateTime? _ultimaHoraConferida;
    private DateOnly? _resumoDoDia;
    private IReadOnlySet<string> _avisadas = new HashSet<string>();
    private bool _estadoRestaurado;

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
                logger.LogError(ex, "SISREG_FRESCOR: a conferência da base falhou.");
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
        var hora = new DateTime(agora.Year, agora.Month, agora.Day, agora.Hour, 0, 0);
        var relogio = TimeOnly.FromDateTime(agora);
        if (_ultimaHoraConferida == hora || relogio < o.HoraDoAviso || relogio > o.HoraFimDoAviso.AddMinutes(59)) return;

        var hoje = DateOnly.FromDateTime(agora);
        using var scope = scopes.CreateScope();
        var frescor = await scope.ServiceProvider.GetRequiredService<IFrescorBaseSisregService>()
            .ConferirAsync(hoje.AddDays(-Math.Max(1, o.DiasDoAviso)), hoje.AddDays(-1), null, ct);
        _ultimaHoraConferida = hora;

        var pendencias = frescor.Pendencias();

        if (!_estadoRestaurado)
        {
            var ultimas = await scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>().AlertaOrigens
                .AsNoTracking()
                .Where(o => o.Chave == AlertaCatalogo.SisregFrescor || o.Chave == AlertaCatalogo.SisregFrescorOk)
                .Select(o => new { o.Chave, o.UltimaOcorrenciaEm })
                .ToListAsync(ct);
            var restaurado = RestaurarEstado(
                ultimas.FirstOrDefault(u => u.Chave == AlertaCatalogo.SisregFrescor)?.UltimaOcorrenciaEm,
                ultimas.FirstOrDefault(u => u.Chave == AlertaCatalogo.SisregFrescorOk)?.UltimaOcorrenciaEm,
                hoje);
            if (restaurado.ResumoJaMandado) _resumoDoDia = hoje;
            if (restaurado.PendenciasJaAvisadas) _avisadas = pendencias;
            _estadoRestaurado = true;
            logger.LogInformation(
                "SISREG_FRESCOR: estado restaurado após o reinício (resumo de hoje já mandado: {Resumo}; pendências atuais tidas como avisadas: {Avisadas}).",
                restaurado.ResumoJaMandado, restaurado.PendenciasJaAvisadas);
        }

        var decisao = Decidir(pendencias, _avisadas, primeiraDoDia: _resumoDoDia != hoje);
        _resumoDoDia = hoje;

        switch (decisao)
        {
            case DecisaoAviso.Avisar:
                var (titulo, detalhe) = Montar(frescor);
                logger.LogWarning("SISREG_FRESCOR: {Titulo} — {Detalhe}", titulo, detalhe);
                alerta.Reportar(new EventoAlerta(AlertaCatalogo.SisregFrescor, titulo, detalhe)
                {
                    Rotulo = "Base do SISREG desatualizada",
                    Grupo = "Sincronismo",
                });
                _avisadas = pendencias;
                break;

            case DecisaoAviso.AvisarQueNormalizou:
                logger.LogInformation("SISREG_FRESCOR: base do SISREG voltou a ficar em dia.");
                alerta.Reportar(new EventoAlerta(AlertaCatalogo.SisregFrescorOk, "faltas e chegadas em dia",
                    "As pendências do último aviso foram lidas: nenhum dia passado sem a lista de faltas e nenhuma "
                    + "unidade sem releitura de chegada. SISREG → Consultar está confiável de novo.")
                {
                    Rotulo = "Base do SISREG voltou ao normal",
                    Grupo = "Sincronismo",
                });
                _avisadas = pendencias;
                break;

            default:
                // Mesma pendência já avisada (ou nada pendente): só o registro. O que foi resolvido sai
                // da lista — se voltar a falhar, é pendência nova e avisa de novo.
                _avisadas = _avisadas.Intersect(pendencias).ToHashSet();
                logger.LogInformation("SISREG_FRESCOR: {Situacao}.",
                    pendencias.Count == 0 ? "base em dia" : $"{pendencias.Count} pendência(s) já avisada(s)");
                break;
        }
    }

    public enum DecisaoAviso { Nada, Avisar, AvisarQueNormalizou }

    /// <summary>
    /// A regra de quando mandar. Pura, para teste: é ela que decide se o Bernardo fica sabendo.
    /// </summary>
    /// <param name="atuais">Pendências desta conferência.</param>
    /// <param name="avisadas">Pendências do último aviso mandado (vazio = nada pendente avisado).</param>
    /// <param name="primeiraDoDia">Primeira conferência do dia — manda o resumo se houver pendência.</param>
    public static DecisaoAviso Decidir(IReadOnlySet<string> atuais, IReadOnlySet<string> avisadas, bool primeiraDoDia)
    {
        if (atuais.Count == 0)
            return avisadas.Count > 0 ? DecisaoAviso.AvisarQueNormalizou : DecisaoAviso.Nada;
        if (primeiraDoDia || atuais.Any(p => !avisadas.Contains(p)))
            return DecisaoAviso.Avisar;
        return DecisaoAviso.Nada;
    }

    public sealed record EstadoRestaurado(bool ResumoJaMandado, bool PendenciasJaAvisadas);

    /// <summary>
    /// O que o aviso já tinha dito hoje, reconstruído depois de um reinício a partir da última ocorrência
    /// de cada chave (UTC). Pura, para teste.
    /// </summary>
    /// <param name="ultimoDesatualizado">Última ocorrência de <see cref="AlertaCatalogo.SisregFrescor"/>.</param>
    /// <param name="ultimoNormalizou">Última ocorrência de <see cref="AlertaCatalogo.SisregFrescorOk"/>.</param>
    /// <param name="hoje">Hoje em Brasília.</param>
    public static EstadoRestaurado RestaurarEstado(DateTime? ultimoDesatualizado, DateTime? ultimoNormalizou, DateOnly hoje)
    {
        bool EhHoje(DateTime? utc) => utc is { } u && DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(u)) == hoje;

        var desatualizadoHoje = EhHoje(ultimoDesatualizado);
        if (!desatualizadoHoje && !EhHoje(ultimoNormalizou))
            return new EstadoRestaurado(false, false);

        // A última palavra de hoje foi "desatualizada": o que está pendente agora já foi dito.
        var ultimaFoiDesatualizada = desatualizadoHoje && (ultimoNormalizou is null || ultimoDesatualizado > ultimoNormalizou);
        return new EstadoRestaurado(true, ultimaFoiDesatualizada);
    }

    /// <summary>O texto do aviso. Público para teste: é o que o Bernardo lê no celular.</summary>
    public static (string Titulo, string Detalhe) Montar(FrescorBaseSisregDto f)
    {
        var partes = new List<string>();
        if (f.DiasSemFaltas.Count > 0)
            partes.Add($"FALHOU a atualização das faltas do SISREG em {f.DiasSemFaltas.Count} dia(s)");
        if (f.ChegadasAtrasadas.Count > 0)
            partes.Add($"FALHOU a releitura de chegada em {f.ChegadasAtrasadas.Count} unidade(s)");
        var titulo = string.Join(" e ", partes);

        var sb = new StringBuilder();
        if (f.DiasSemFaltas.Count > 0)
        {
            sb.Append("Faltas não atualizadas: ");
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
