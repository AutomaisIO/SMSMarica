using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Ser.Background;
using SMSMais.Core.Sernit.Background;
using SMSMais.Data;

namespace SMSMais.Core.Regulacao.Catalogo;

/// <summary>Configuração da cópia diária dos catálogos do SER e do SERNIT.</summary>
public sealed class CatalogoDiarioOpcoes
{
    public const string Secao = "Regulacao:CatalogoDiario";

    /// <summary>Desligar faz o catálogo só andar pelo botão da tela de configuração.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Hora de Brasília a partir da qual o agendador copia o catálogo do dia. Madrugada: a cópia
    /// usa a mesma sessão do sistema que a varredura, e de manhã cedo ninguém está pedindo nada.
    /// Quem abrir uma solicitação antes disso também dispara (<see cref="ICatalogosRegulacaoFrescor.Cutucar"/>).
    /// </summary>
    public int HoraLocal { get; set; } = 5;
}

/// <summary>
/// Garante que os catálogos do SER e do SERNIT foram copiados <b>hoje</b> (pedido do Bernardo,
/// 07/10/2026): o agendador confere de madrugada, e a primeira solicitação aberta no dia confere
/// também — se a cópia de hoje não rodou, ela é enfileirada.
///
/// <para><b>O que a cópia diária resolve e o que ela NÃO resolve.</b> Ela traz recurso novo, tira o
/// que saiu do ar e atualiza o número que cada recurso tem hoje. Mas ninguém depende desse número
/// para acertar o recurso: a identidade é o nome (<see cref="IdentidadePorNome"/>) e quem conversa
/// com o sistema ao vivo acha o recurso pelo nome na hora. A cópia de hoje é frescor, não
/// correção.</para>
///
/// <para>Só copia o que já foi copiado alguma vez: instância sem SER/SERNIT (ADR-0043) não tem
/// linha no espelho e não passa a bater em sistema que não usa.</para>
/// </summary>
public interface ICatalogosRegulacaoFrescor
{
    /// <summary>
    /// Confere em segundo plano, sem esperar nem lançar — é chamado no caminho de abrir uma
    /// solicitação, que não pode ficar lento nem falhar por causa disso. Conferências seguidas
    /// dentro de poucos minutos viram uma só.
    /// </summary>
    void Cutucar();

    /// <summary>Confere agora e enfileira a cópia de quem não foi copiado hoje.</summary>
    Task<CatalogosFrescorResultado> ConferirAsync(CancellationToken cancellationToken);
}

/// <summary>O que a conferência fez com cada sistema.</summary>
public sealed record CatalogosFrescorResultado(bool SerEnfileirado, bool SernitEnfileirado);

public sealed class CatalogosRegulacaoFrescor(
    IServiceScopeFactory scopes,
    ISerCatalogoSyncFila filaSer,
    ISernitCatalogoSyncFila filaSernit,
    IOptions<CatalogoDiarioOpcoes> opcoes,
    ILogger<CatalogosRegulacaoFrescor> logger) : ICatalogosRegulacaoFrescor
{
    /// <summary>Abrir solicitação é frequente; a conferência custa uma consulta pequena, mas não
    /// precisa acontecer a cada clique.</summary>
    private static readonly TimeSpan IntervaloEntreConferencias = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Depois de enfileirar, espera isto antes de tentar de novo no mesmo dia. Se a cópia falhou
    /// (sistema fora do ar, aviso pendente na home do SER), tentar a cada abertura de solicitação
    /// só ocuparia a sessão que a varredura também usa.
    /// </summary>
    private static readonly TimeSpan EsperaEntreTentativas = TimeSpan.FromHours(1);

    private readonly Lock _trava = new();
    private DateTime _ultimaConferencia = DateTime.MinValue;
    private DateTime _ultimaTentativaSer = DateTime.MinValue;
    private DateTime _ultimaTentativaSernit = DateTime.MinValue;

    public void Cutucar()
    {
        if (!opcoes.Value.Ativo) return;

        var agora = DateTime.UtcNow;
        lock (_trava)
        {
            if (agora - _ultimaConferencia < IntervaloEntreConferencias) return;
            _ultimaConferencia = agora;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ConferirAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Catálogos da regulação: a conferência do dia falhou.");
            }
        });
    }

    public async Task<CatalogosFrescorResultado> ConferirAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
        var inicioDeHoje = FusoBrasilia.InicioDoDiaAtualEmUtc();

        // Por COMBO (tipo e ramo), e o mais atrasado decide: uma cópia que caiu no meio deixa um
        // combo com a data de hoje e outro com a de ontem, e o máximo geral diria "já foi".
        var ser = await db.SerCatalogoRecursos.AsNoTracking()
            .GroupBy(r => new { r.Tipo, r.AmbulatorioEstadual })
            .Select(g => (DateTime?)g.Max(r => r.SincronizadoEm))
            .ToListAsync(cancellationToken);
        var sernit = await db.SernitCatalogoRecursos.AsNoTracking()
            .GroupBy(r => r.Tipo)
            .Select(g => (DateTime?)g.Max(r => r.SincronizadoEm))
            .ToListAsync(cancellationToken);

        var serEnfileirado = Precisa(ser, inicioDeHoje)
                             && Enfileirar("SER", ref _ultimaTentativaSer, () => filaSer.TentarEnfileirar(false));
        var sernitEnfileirado = Precisa(sernit, inicioDeHoje)
                                && Enfileirar("SERNIT", ref _ultimaTentativaSernit, () => filaSernit.TentarEnfileirar(false));

        return new CatalogosFrescorResultado(serEnfileirado, sernitEnfileirado);
    }

    /// <summary>Catálogo vazio = sistema que esta instância não usa; não é "atrasado".</summary>
    private static bool Precisa(List<DateTime?> porCombo, DateTime inicioDeHoje) =>
        porCombo.Count > 0 && porCombo.Min() < inicioDeHoje;

    private bool Enfileirar(string sistema, ref DateTime ultimaTentativa, Func<bool> tentar)
    {
        var agora = DateTime.UtcNow;
        lock (_trava)
        {
            if (agora - ultimaTentativa < EsperaEntreTentativas) return false;
            ultimaTentativa = agora;
        }

        // `false` também quando a cópia já está rodando — e então não há o que fazer.
        if (!tentar()) return false;

        logger.LogInformation("Catálogos da regulação: cópia de hoje do {Sistema} enfileirada.", sistema);
        return true;
    }
}

/// <summary>
/// Confere de tempos em tempos se a cópia de hoje já rodou, a partir de
/// <see cref="CatalogoDiarioOpcoes.HoraLocal"/>. A conferência é barata (uma consulta agrupada) e
/// só enfileira quando o catálogo não é de hoje — por isso o intervalo curto não custa nada ao
/// sistema de fora.
/// </summary>
public sealed class CatalogosRegulacaoDiarioScheduler(
    ICatalogosRegulacaoFrescor frescor,
    IOptions<CatalogoDiarioOpcoes> opcoes,
    ILogger<CatalogosRegulacaoDiarioScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opcoes.Value.Ativo)
        {
            logger.LogInformation("Catálogos da regulação: cópia diária desligada ({Secao}:Ativo).",
                CatalogoDiarioOpcoes.Secao);
            return;
        }

        using var timer = new PeriodicTimer(Intervalo);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (FusoBrasilia.ParaExibicao(DateTime.UtcNow).Hour < opcoes.Value.HoraLocal) continue;
                await frescor.ConferirAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Um tick ruim não derruba o host nem impede o próximo.
                logger.LogWarning(ex, "Catálogos da regulação: conferência do agendador falhou.");
            }
        }
    }
}
