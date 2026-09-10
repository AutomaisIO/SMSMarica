using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb.Fila.Background;

/// <summary>
/// Dá vida ao motor da fila: o diário e a carga pedida pela tela.
///
/// <para><b>Por que existe.</b> O motor foi escrito e registrado, mas nada o chamava — em 10/09/2026
/// a tabela de produção tinha <b>zero linhas</b>, e a tela de Ofertas abria sempre "ninguém
/// esperando". Uma lista vazia que parece resposta é pior que erro.</para>
///
/// <para><b>Uma janela por tick.</b> Cada janela custa 2 requisições e até ~75 s; fazer a carga
/// inteira (~33 janelas) num laço prenderia a sessão do operador por meia hora. Um tick por vez
/// deixa os outros motores e o humano intercalarem, e o tick cede a vez a qualquer trabalho vivo
/// do SISREG.</para>
///
/// <para><b>O diário respeita a chave-mestra; a carga pedida, não.</b> Mesma regra do histórico da
/// agenda: a chave pausa o que roda sem ninguém pedir. Clicar em "carregar a fila" já é o comando
/// de uma pessoa — gateá-lo faria o botão não produzir efeito nenhum.</para>
/// </summary>
public sealed class FilaPendenteScheduler(
    IServiceScopeFactory scopeFactory,
    FilaPendenteEstadoVivo estado,
    Varredura.Background.VarreduraSisregEstadoVivo varreduraEstadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    MapeamentoLote.Background.MapeamentoLoteEstadoVivo loteEstadoVivo,
    Escalas.Background.EscalasSincronizacaoEstadoVivo escalasEstadoVivo,
    ILogger<FilaPendenteScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Antes do expediente: a sessão do SISREG é única por operador, e 75 s de download no meio da
    /// manhã disputam com quem está atendendo. A tela da fila não tem a trava 07:30–15:00.
    /// </summary>
    private static readonly TimeOnly HoraDoDiario = new(5, 30);

    /// <summary>Com a chave-mestra desligada, reconsultar a cada tick seria polling à toa.</summary>
    private static readonly TimeSpan EsperaComChaveDesligada = TimeSpan.FromMinutes(10);

    private DateOnly? _diarioEnfileiradoEm;
    private DateTime _proximaChecagemDaChave = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);

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
                // Um tick ruim não pode derrubar o host nem parar os próximos.
                logger.LogError(ex, "Erro no tick do motor da fila de espera do SISREG.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // Todos dividem a mesma sessão e o mesmo orçamento: com trabalho vivo, espera.
        if (varreduraEstadoVivo.ObterAtual() is not null
            || importacaoEstadoVivo.ObterAtual() is not null
            || loteEstadoVivo.EmExecucao
            || escalasEstadoVivo.EmExecucao)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();

        await TalvezEnfileirarDiarioAsync(scope, ct);

        var janela = estado.Proxima();
        if (janela is null) return;

        var servico = scope.ServiceProvider.GetRequiredService<IFilaPendenteSisregService>();
        try
        {
            var r = await servico.LerJanelaAsync(janela.Inicio, janela.Fim, ct);
            estado.Concluir(r);
            logger.LogInformation(
                "SISREG_FILA_JANELA: {Ini}..{Fim} — {Lidas} na fila, {Novas} novas, {Saidas} saídas.",
                r.Inicio, r.Fim, r.Lidas, r.Novas, r.Saidas);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            estado.Adiar();
            throw;
        }
        catch (Exception ex) when (SisregWebSessao.EhCaptcha(ex))
        {
            estado.Falhar("O SISREG pediu CAPTCHA — leitura interrompida. Tente de novo amanhã.", desistir: true);
            logger.LogWarning("SISREG_FILA_CAPTCHA: leitura da fila interrompida em {Ini}..{Fim}.",
                janela.Inicio, janela.Fim);
        }
        catch (ConflitoException)
        {
            // Orçamento curto nesta hora: a janela volta para a frente e sai quando houver folga.
            estado.Adiar();
        }
        catch (Exception ex)
        {
            estado.Falhar(ex.Message, desistir: false);
            logger.LogError(ex, "SISREG_FILA_FALHA: janela {Ini}..{Fim}.", janela.Inicio, janela.Fim);
        }
    }

    private async Task TalvezEnfileirarDiarioAsync(IServiceScope scope, CancellationToken ct)
    {
        var agora = FusoBrasilia.ParaExibicao(DateTime.UtcNow);
        var hoje = DateOnly.FromDateTime(agora);

        if (_diarioEnfileiradoEm == hoje || TimeOnly.FromDateTime(agora) < HoraDoDiario) return;
        if (estado.EmExecucao || DateTime.UtcNow < _proximaChecagemDaChave) return;

        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
        if (!await SincronismoAutomaticoSisreg.LigadoAsync(db, ct))
        {
            _proximaChecagemDaChave = DateTime.UtcNow + EsperaComChaveDesligada;
            return;
        }

        if (estado.Enfileirar(JanelasDaFila.Recente(hoje), completa: false))
        {
            _diarioEnfileiradoEm = hoje;
            logger.LogInformation("SISREG_FILA_DIARIO: leitura da janela recente enfileirada.");
        }
    }
}
