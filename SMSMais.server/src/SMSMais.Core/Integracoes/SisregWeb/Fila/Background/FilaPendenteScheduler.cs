using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb.Fila.Background;

/// <summary>
/// Dá vida ao motor da fila: o diário, a carga pedida pela tela e o fechamento pela agenda.
///
/// <para><b>Por que existe.</b> O motor foi escrito e registrado, mas nada o chamava — em 10/09/2026
/// a tabela de produção tinha <b>zero linhas</b>, e a tela de Ofertas abria sempre "ninguém
/// esperando". Uma lista vazia que parece resposta é pior que erro.</para>
///
/// <para><b>Daqui para frente.</b> O SISREG só é relido na janela recente (o diário); o passado é
/// carregado uma vez, a pedido. Quem sai da fila por agendamento — 94% das saídas medidas — sai
/// pelo fechamento pela agenda, que não custa requisição.</para>
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

    /// <summary>
    /// Entre duas tentativas do diário no mesmo dia. Leitura que falha (sessão caída às 05:30, como
    /// em 12/09/2026) é tentada de novo mais tarde, em vez de o dia inteiro ficar sem leitura.
    /// </summary>
    private static readonly TimeSpan EsperaEntreTentativasDoDiario = TimeSpan.FromMinutes(30);

    /// <summary>Fechamento pela agenda: barato (uma consulta), sem SISREG.</summary>
    private static readonly TimeSpan IntervaloDoFechamento = TimeSpan.FromMinutes(10);

    private DateTime _proximaTentativaDoDiario = DateTime.MinValue;
    private DateTime _proximoFechamento = DateTime.MinValue;

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
        using var scope = scopeFactory.CreateScope();
        var servico = scope.ServiceProvider.GetRequiredService<IFilaPendenteSisregService>();

        // Não fala com o SISREG: roda mesmo com outro motor usando a sessão.
        if (DateTime.UtcNow >= _proximoFechamento)
        {
            _proximoFechamento = DateTime.UtcNow + IntervaloDoFechamento;
            await servico.FecharAgendadosAsync(ct);
        }

        // Todos dividem a mesma sessão e o mesmo orçamento: com trabalho vivo, espera.
        if (varreduraEstadoVivo.ObterAtual() is not null
            || importacaoEstadoVivo.ObterAtual() is not null
            || loteEstadoVivo.EmExecucao
            || escalasEstadoVivo.EmExecucao)
        {
            return;
        }

        await TalvezEnfileirarDiarioAsync(scope, servico, ct);

        var janela = estado.Proxima();
        if (janela is null) return;

        try
        {
            var r = await servico.LerJanelaAsync(janela.Inicio, janela.Fim, ct);
            estado.Concluir(r);
            logger.LogInformation(
                "SISREG_FILA_JANELA: {Ini}..{Fim} — {Lidas} na fila, {Novas} novas, {Saidas} saídas.",
                r.Inicio, r.Fim, r.Lidas, r.Novas, r.Saidas);
            await servico.FecharAgendadosAsync(ct);
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
            // Inclui LeituraDaFilaInvalidaException: a janela volta para a frente e é relida no
            // próximo tick (a sessão já terá sido refeita). Três seguidas desistem da leitura.
            estado.Falhar(ex.Message, desistir: false);
            logger.LogWarning(ex, "SISREG_FILA_FALHA: janela {Ini}..{Fim}.", janela.Inicio, janela.Fim);
        }
    }

    /// <summary>
    /// "Já li hoje?" é decidido pelo BANCO (a última pessoa vista na fila), não pela memória do
    /// processo. Assim um restart depois das 05:30 não relê o que já foi lido, e uma leitura que
    /// falhou é tentada de novo meia hora depois.
    /// </summary>
    private async Task TalvezEnfileirarDiarioAsync(
        IServiceScope scope, IFilaPendenteSisregService servico, CancellationToken ct)
    {
        var agora = FusoBrasilia.ParaExibicao(DateTime.UtcNow);
        var hoje = DateOnly.FromDateTime(agora);

        if (TimeOnly.FromDateTime(agora) < HoraDoDiario) return;
        if (estado.EmExecucao || DateTime.UtcNow < _proximaTentativaDoDiario) return;

        _proximaTentativaDoDiario = DateTime.UtcNow + EsperaEntreTentativasDoDiario;

        var resumo = await servico.ResumoAsync(ct);
        if (resumo.UltimaLeitura is { } ultima
            && DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(ultima)) == hoje)
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
        if (!await SincronismoAutomaticoSisreg.LigadoAsync(db, ct)) return;

        if (estado.Enfileirar(JanelasDaFila.Recente(hoje), completa: false))
        {
            logger.LogInformation("SISREG_FILA_DIARIO: leitura da janela recente enfileirada.");
        }
    }
}
