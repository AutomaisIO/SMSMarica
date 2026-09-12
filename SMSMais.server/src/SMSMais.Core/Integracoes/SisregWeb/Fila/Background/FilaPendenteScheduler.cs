using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb.Fila.Background;

/// <summary>
/// Dá vida ao motor da fila: a releitura completa diária, a leitura pedida pela configuração e o
/// fechamento pela agenda.
///
/// <para><b>Por que existe.</b> O motor foi escrito e registrado, mas nada o chamava — em 10/09/2026
/// a tabela de produção tinha <b>zero linhas</b>, e a tela de Ofertas abria sempre "ninguém
/// esperando". Uma lista vazia que parece resposta é pior que erro.</para>
///
/// <para><b>Todo dia, o passado inteiro.</b> Decidido em 12/09/2026 (ver
/// <see cref="AgendamentoDaFila"/>): ler só "daqui para frente" deixa passar saída sem agendamento,
/// pedido antigo reenviado com a data original, troca de risco/procedimento e os nossos próprios
/// erros. Uma vez por dia, na hora configurada (padrão 03:00), o acervo inteiro é relido. Quem vira
/// agendamento sai antes disso, pelo fechamento pela agenda, que não custa requisição.</para>
///
/// <para><b>Uma janela por tick.</b> Cada janela custa 2 requisições e até ~2 min; um tick por vez
/// deixa os outros motores intercalarem, e o tick cede a vez a qualquer trabalho vivo do SISREG.</para>
///
/// <para><b>A releitura diária respeita a chave-mestra; o botão da configuração, não.</b> Mesma regra
/// do histórico da agenda: a chave pausa o que roda sem ninguém pedir.</para>
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
    /// Até quanto depois da hora configurada ainda vale disparar: servidor reiniciando ou outro motor
    /// ocupando a sessão na hora exata não pode fazer o dia ficar sem releitura.
    /// </summary>
    private static readonly TimeSpan JanelaDeDisparo = TimeSpan.FromHours(2);

    /// <summary>O horário vem da configuração; relê-lo a cada tick seria consulta à toa.</summary>
    private static readonly TimeSpan ValidadeDoAgendamento = TimeSpan.FromMinutes(5);

    /// <summary>Com a chave-mestra desligada, reconsultar a cada tick seria polling à toa.</summary>
    private static readonly TimeSpan EsperaComChaveDesligada = TimeSpan.FromMinutes(10);

    /// <summary>Fechamento pela agenda: barato (uma consulta), sem SISREG.</summary>
    private static readonly TimeSpan IntervaloDoFechamento = TimeSpan.FromMinutes(10);

    private DateOnly? _releituraDisparadaEm;
    private FilaAgendamentoDto? _agendamento;
    private DateTime _agendamentoLidoEm = DateTime.MinValue;
    private DateTime _proximaChecagemDaChave = DateTime.MinValue;
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

        await TalvezDispararReleituraAsync(scope, servico, ct);

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
            // Error (não Warning): leitura desistida do dia é falha que precisa de gente — é o
            // nível que leva o aviso ao celular.
            logger.LogError("SISREG_FILA_CAPTCHA: leitura da fila interrompida em {Ini}..{Fim}.",
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
    /// Enfileira a releitura do acervo inteiro uma vez por dia, dentro de
    /// <see cref="JanelaDeDisparo"/> a partir da hora configurada.
    ///
    /// <para>"Já rodou hoje?" olha também o BANCO: se alguém foi visto na fila depois da hora de hoje,
    /// a releitura já aconteceu (ou está acontecendo) — um restart no meio da janela não relê tudo
    /// de novo.</para>
    /// </summary>
    private async Task TalvezDispararReleituraAsync(
        IServiceScope scope, IFilaPendenteSisregService servico, CancellationToken ct)
    {
        var agora = FusoBrasilia.ParaExibicao(DateTime.UtcNow);
        var hoje = DateOnly.FromDateTime(agora);

        if (_releituraDisparadaEm == hoje || estado.EmExecucao) return;

        if (_agendamento is null || DateTime.UtcNow - _agendamentoLidoEm > ValidadeDoAgendamento)
        {
            var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
            _agendamento = await AgendamentoDaFila.ObterAsync(credenciais, ct);
            _agendamentoLidoEm = DateTime.UtcNow;
        }

        if (!_agendamento.Ativo
            || !TimeOnly.TryParseExact(_agendamento.HoraLocal, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var hora))
        {
            return;
        }

        var inicioDoSlot = agora.Date + hora.ToTimeSpan();
        var desdeOSlot = agora - inicioDoSlot;
        if (desdeOSlot < TimeSpan.Zero || desdeOSlot >= JanelaDeDisparo) return;

        var resumo = await servico.ResumoAsync(ct);
        if (resumo.UltimaLeitura is { } ultima && FusoBrasilia.ParaExibicao(ultima) >= inicioDoSlot)
        {
            _releituraDisparadaEm = hoje;
            return;
        }

        if (DateTime.UtcNow < _proximaChecagemDaChave) return;
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
        if (!await SincronismoAutomaticoSisreg.LigadoAsync(db, ct))
        {
            _proximaChecagemDaChave = DateTime.UtcNow + EsperaComChaveDesligada;
            return;
        }

        if (estado.Enfileirar(JanelasDaFila.Completa(hoje, JanelasDaFila.InicioDoAcervo), completa: true))
        {
            _releituraDisparadaEm = hoje;
            logger.LogInformation(
                "SISREG_FILA_RELEITURA: releitura completa diária enfileirada (slot {Hora} Brasília).",
                _agendamento.HoraLocal);
        }
    }
}
