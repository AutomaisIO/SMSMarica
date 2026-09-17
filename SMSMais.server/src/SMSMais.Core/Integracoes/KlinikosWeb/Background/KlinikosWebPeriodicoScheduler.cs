using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Integracoes.KlinikosWeb.Escrita;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Core.Integracoes.KlinikosWeb.Background;

/// <summary>
/// Puxa a espinha do dia corrente de cada fonte <see cref="TipoFonte.KlinikosWeb"/> na cadência que
/// o OPERADOR configurou por base (<c>periodicoIntervaloMin</c> no <c>ParametrosJson</c>). O painel
/// do secretário lê do hub; este scheduler é o que mantém o hub fresco sem onerar a origem.
///
/// <para><b>A cadência é MANUAL e por base.</b> Não há auto-ajuste: o motor lê o número, agenda e
/// obedece. Descer de 60 → 30 → 15 é decisão sua, medindo o ponto de saturação do Crystal de cada
/// unidade — o motor nunca reescreve o seu setting.</para>
///
/// <para><b>Inerte por padrão, em quatro travas:</b> (1) <c>KlinikosWeb:SchedulersHabilitados</c> e
/// (2) <c>KlinikosWeb:EscritaHabilitada</c> (ambas false por padrão); (3) a base precisa ter
/// <c>periodicoLigado=true</c> com intervalo &gt; 0; (4) a escrita só ocorre onde
/// <c>webPrimaria=true</c> (Conde). Sem tudo isso, o tick não faz nada.</para>
///
/// <para><b>Circuit breaker (airbag):</b> se um ciclo bater no Crystal saturado
/// (<see cref="KlinikosCrystalIndisponivelException"/>) ou em qualquer falha, o ciclo é pulado e a
/// base espera a PRÓXIMA cadência — nunca retenta rápido, nunca mexe no número configurado. É o
/// oposto de martelar o servidor de relatório deles.</para>
/// </summary>
public sealed class KlinikosWebPeriodicoScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<KlinikosWebOpcoes> opcoes,
    ILogger<KlinikosWebPeriodicoScheduler> logger) : BackgroundService
{
    private const int TickSegundos = 30;
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Último ciclo (tentado, com sucesso ou falha) por slug — em memória. Reinício do
    /// serviço reavalia do zero; nunca roda mais que a cadência entre dois ciclos da mesma base.</summary>
    private readonly Dictionary<string, DateTime> _ultimoCicloUtc = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(TickSegundos));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Um tick ruim não derruba o host nem para os próximos.
                logger.LogError(ex, "Erro no tick do periódico do Klinikos web.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var opc = opcoes.Value;
        // Travas mestras: sem schedulers OU sem escrita, o periódico não existe. (Barato: nem abre escopo.)
        if (!opc.SchedulersHabilitados || !opc.EscritaHabilitada) return;

        List<FonteCadencia> fontes;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
            fontes = await db.Set<IaFonte>().AsNoTracking()
                .Where(f => f.Tipo == TipoFonte.KlinikosWeb && f.Ativo && f.ExcluidoEm == null
                    && f.Slug != null)
                .Select(f => new FonteCadencia(f.Slug!, f.ParametrosJson))
                .ToListAsync(ct);
        }

        var agoraUtc = DateTime.UtcNow;
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, Brasilia));

        foreach (var f in fontes)
        {
            ct.ThrowIfCancellationRequested();

            var p = KlinikosWebParametros.Resolver(f.Slug, f.ParametrosJson);
            // Cadência manual por base: sem periódico ligado ou sem intervalo, não roda.
            if (!p.PeriodicoLigado || p.PeriodicoIntervaloMin <= 0) continue;
            // Só onde o web é primário (Conde). As UPAs têm dono SQL — gravar ali duplicaria.
            if (!p.WebPrimaria) continue;

            var intervalo = TimeSpan.FromMinutes(p.PeriodicoIntervaloMin);
            if (_ultimoCicloUtc.TryGetValue(f.Slug, out var ultimo) && agoraUtc - ultimo < intervalo)
            {
                continue;
            }

            // Marca ANTES de rodar: um ciclo — sucesso ou falha — consome a cadência inteira. Nunca martela.
            _ultimoCicloUtc[f.Slug] = agoraUtc;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var escrita = scope.ServiceProvider.GetRequiredService<IKlinikosWebEscritaService>();
                var resumo = await escrita.GravarEspinhaAsync(f.Slug, hoje, ct);
                logger.LogInformation(
                    "KLINIKOS_PERIODICO {Slug} {Dia}: {Enc} Encounter, {Cond} Condition, "
                    + "{Fila} enfileirados no deep (cadência {Min} min).",
                    f.Slug, hoje, resumo.Encounters, resumo.Conditions, resumo.Enfileirados,
                    p.PeriodicoIntervaloMin);
            }
            catch (KlinikosCrystalIndisponivelException)
            {
                // AIRBAG: Crystal saturado. Pula o ciclo e espera a próxima cadência — sem retentar
                // rápido, sem tocar no número. (Provavelmente os usuários da unidade já gastaram o budget.)
                logger.LogWarning(
                    "KLINIKOS_PERIODICO {Slug}: Crystal indisponível (saturado); ciclo pulado, "
                    + "aguardando {Min} min.", f.Slug, p.PeriodicoIntervaloMin);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "KLINIKOS_PERIODICO {Slug}: falha no ciclo; aguardando {Min} min até a próxima tentativa.",
                    f.Slug, p.PeriodicoIntervaloMin);
            }
        }
    }

    private sealed record FonteCadencia(string Slug, string? ParametrosJson);
}
