using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Institucional;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Indicadores.Background;

/// <summary>
/// O que o coletor está fazendo agora (memória): o trabalho em curso, por que está esperando e o
/// contador das próprias requisições — lido pela tela e pelo agendador.
/// </summary>
public sealed class ColetaIndicadoresEstadoVivo
{
    private readonly Lock _trava = new();

    public ContadorRequisicoesColeta Requisicoes { get; } = new();

    public TrabalhoColeta? Trabalho { get; private set; }
    public EsperaColetaIndicadores? Espera { get; private set; }
    public DateTime? UltimoPassoEm { get; private set; }

    /// <summary>Há um item no meio (entre passos) — outro motor que queira a sessão pode olhar aqui.</summary>
    public bool EmExecucao => Trabalho is not null && Espera is null;

    public void Assumir(TrabalhoColeta? trabalho)
    {
        lock (_trava) Trabalho = trabalho;
    }

    public void Esperando(EsperaColetaIndicadores? motivo)
    {
        lock (_trava) Espera = motivo;
    }

    public void Passo(DateTime agoraUtc)
    {
        lock (_trava) UltimoPassoEm = agoraUtc;
    }
}

/// <summary>Conta, para o teto do coletor, cada ida ao SISREG feita pelos trabalhos.</summary>
internal sealed class SessaoContadaColeta(ISisregWebSessao interna, ContadorRequisicoesColeta contador) : ISisregWebSessao
{
    public Task<string> PostFormAsync(string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken)
    {
        contador.Registrar(DateTime.UtcNow);
        return interna.PostFormAsync(caminho, campos, cancellationToken);
    }

    public Task<string> GetAsync(
        string caminho, IReadOnlyDictionary<string, string>? query, CancellationToken cancellationToken,
        Func<string, bool>? pareceSessaoCaida = null)
    {
        contador.Registrar(DateTime.UtcNow);
        return interna.GetAsync(caminho, query, cancellationToken, pareceSessaoCaida);
    }

    public void UsarCredencialDoOperador(string usuario, string senha) =>
        throw new InvalidOperationException("O coletor de indicadores só lê — nunca assina como operador.");
}

/// <summary>
/// Mantém em dia as tabelas dos Indicadores de Regulação do SISREG: faltas oficiais, cotas PPI,
/// marcações canceladas do mês e desfechos (devolvidas/negadas/canceladas antes de agendar) por unidade.
///
/// <para><b>Nasce DESLIGADO</b> (Configuração do SISREG → Indicadores). Com ele ligado, cada tick dá
/// no máximo UM passo — uma requisição — e só quando:</para>
/// <list type="bullet">
///   <item>a chave-mestra do sincronismo automático está ligada;</item>
///   <item>não há pausa de CAPTCHA (24 h, gravada no banco — um restart não a desfaz);</item>
///   <item>está entre 01:20 e 18:00 (fora da varredura das agendas);</item>
///   <item>nenhum outro motor está usando a sessão (varredura, importação, lote, escalas, fila);</item>
///   <item>o próprio coletor fez menos de 150 requisições na última hora e o orçamento global tem folga.</item>
/// </list>
///
/// <para><b>Ritmo.</b> Um tick a cada 30 s → no máximo ~120 requisições por hora. O que existe para
/// ler é pouco no dia a dia: ~4 semanas de faltas por mês (2 req cada), 1 PPI por mês, ~45 unidades × 3
/// situações de desfechos por mês e, só quando falta o total de um mês recente, o mês inteiro de canceladas
/// (~100–120 páginas). Os motivos do dia a dia vêm da conciliação, que já lê a tela e agora grava as linhas;
/// os dos meses passados (carga de 30/09 só com o total) vêm de uma AMOSTRA de 6 páginas espalhadas por mês
/// — ~120 requisições para jan/2025–ago/2026 inteiros.</para>
/// </summary>
public sealed class ColetaIndicadoresScheduler(
    IServiceScopeFactory scopeFactory,
    ColetaIndicadoresEstadoVivo estado,
    Varredura.Background.VarreduraSisregEstadoVivo varreduraEstadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    MapeamentoLote.Background.MapeamentoLoteEstadoVivo loteEstadoVivo,
    Escalas.Background.EscalasSincronizacaoEstadoVivo escalasEstadoVivo,
    Fila.Background.FilaPendenteEstadoVivo filaEstadoVivo,
    SisregOrcamentoRequisicoes orcamento,
    IOptions<ColetaIndicadoresOpcoes> opcoes,
    IOptions<SisregOrcamentoOpcoes> orcamentoOpcoes,
    ILogger<ColetaIndicadoresScheduler> logger) : BackgroundService
{
    private readonly ColetaIndicadoresOpcoes _opcoes = opcoes.Value;

    /// <summary>O plano (criar janelas, re-armar) é consulta ao banco — uma vez por hora basta.</summary>
    private static readonly TimeSpan IntervaloDoPlano = TimeSpan.FromHours(1);

    private DateTime _proximoPlano = DateTime.MinValue;
    private bool _resetInicial;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(_opcoes.TickSegundos, 10, 600)));
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
                // Um tick ruim não pode derrubar o host nem impedir o próximo. O trabalho em curso é
                // descartado: o item volta a pendente no próximo tick (reset dos órfãos).
                estado.Assumir(null);
                logger.LogError(ex, "Erro no tick do coletor de indicadores do SISREG.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var servico = scope.ServiceProvider.GetRequiredService<IColetaIndicadoresSisregService>();
        var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();

        if (!_resetInicial)
        {
            // O restart deixa "em andamento" o item que estava no meio — ninguém mais o executa.
            await servico.ResetarEmAndamentoAsync(null, ct);
            _resetInicial = true;
        }

        var config = await ColetaIndicadoresConfig.ObterAsync(credenciais, ct);
        var agoraUtc = DateTime.UtcNow;
        var local = FusoBrasilia.ParaExibicao(agoraUtc);
        var chave = !config.Ativa || await SincronismoAutomaticoSisreg.LigadoAsync(
            scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>(), ct);

        var espera = PortaoColetaIndicadores.Decidir(new EntradaPortaoColeta(
            config.Ativa, chave, config.PausadaAte, agoraUtc, TimeOnly.FromDateTime(local),
            OutroMotorVivo(), estado.Requisicoes.NaUltimaHora(agoraUtc),
            orcamento.Restante(orcamentoOpcoes.Value.TetoAutomatico)), _opcoes);
        estado.Esperando(espera);

        if (espera is not null)
        {
            // Desligado no meio de um item: devolve o item, sem culpa. Nas outras esperas o trabalho
            // continua de onde parou no próximo tick livre.
            if (espera is EsperaColetaIndicadores.Desligada or EsperaColetaIndicadores.ChaveMestraDesligada
                && estado.Trabalho is { } parado)
            {
                await servico.DevolverAsync(parado.Item.Id, "o coletor foi desligado no meio da leitura", ct);
                estado.Assumir(null);
            }
            return;
        }

        var trabalho = estado.Trabalho;
        if (trabalho is null)
        {
            var hoje = DateOnly.FromDateTime(local);
            if (agoraUtc >= _proximoPlano)
            {
                await servico.PlanejarAsync(hoje, ct);
                _proximoPlano = agoraUtc + IntervaloDoPlano;
            }

            // Sem trabalho em memória, qualquer "em andamento" no banco é órfão.
            await servico.ResetarEmAndamentoAsync(null, ct);
            var item = await servico.ReservarProximoAsync(ct);
            if (item is null) return;

            trabalho = await CriarAsync(scope, item, ct);
            estado.Assumir(trabalho);
            logger.LogInformation("SISREG_INDICADORES: começando {Trabalho}.", trabalho.Descricao);
        }

        var sessao = new SessaoContadaColeta(
            scope.ServiceProvider.GetRequiredService<ISisregWebSessao>(), estado.Requisicoes);
        var armazem = scope.ServiceProvider.GetRequiredService<IArmazemIndicadoresSisreg>();

        var r = await TrabalhoColeta.ExecutarAsync(trabalho, sessao, armazem, ct);
        estado.Passo(DateTime.UtcNow);
        await AplicarAsync(servico, credenciais, trabalho, r, ct);
    }

    private async Task AplicarAsync(
        IColetaIndicadoresSisregService servico, IIntegracaoCredencialService credenciais,
        TrabalhoColeta trabalho, ResultadoPasso r, CancellationToken ct)
    {
        var item = trabalho.Item;
        switch (r.Desfecho)
        {
            case DesfechoPasso.Continuar:
                return;

            case DesfechoPasso.Concluida:
                await servico.ConcluirAsync(item.Id, r.Linhas ?? 0, ct);
                if (r.Unidades is { Count: > 0 } unidades)
                    await servico.CriarItensDeUnidadesAsync(item.Inicio, unidades, ct);
                logger.LogInformation("SISREG_INDICADORES: {Trabalho} concluído ({Linhas} linha(s)).",
                    trabalho.Descricao, r.Linhas);
                break;

            case DesfechoPasso.TempoEsgotado when item.Coletor == ColetorIndicadorSisreg.Faltas
                                                  && item.Fim > item.Inicio:
                await servico.DividirEmDiasAsync(item.Id, r.Mensagem ?? "tempo esgotado", ct);
                logger.LogWarning("SISREG_INDICADORES: {Trabalho} estourou o tempo — dividido em dias.", trabalho.Descricao);
                break;

            case DesfechoPasso.Captcha:
                // O operador está bloqueado: parar tudo por um dia. O item não tem culpa — volta a pendente.
                await servico.DevolverAsync(item.Id, "CAPTCHA — coletor pausado", ct);
                var ate = DateTime.UtcNow.AddHours(Math.Max(1, _opcoes.HorasDePausaNoCaptcha));
                await ColetaIndicadoresConfig.PausarAsync(credenciais, ate, ct);
                // Error: é o nível que leva o aviso ao celular.
                logger.LogError(
                    "SISREG_INDICADORES_CAPTCHA: o SISREG pediu CAPTCHA em {Trabalho}. Coletor pausado até {Ate:dd/MM HH:mm} UTC.",
                    trabalho.Descricao, ate);
                break;

            default:
                await servico.FalharAsync(item.Id, r.Mensagem ?? "falha sem mensagem", ct);
                logger.LogWarning("SISREG_INDICADORES_FALHA: {Trabalho}: {Mensagem}", trabalho.Descricao, r.Mensagem);
                break;
        }
        estado.Assumir(null);
    }

    private async Task<TrabalhoColeta> CriarAsync(IServiceScope scope, ItemColeta item, CancellationToken ct)
    {
        var fracao = _opcoes.FracaoMinimaNaSubstituicao;
        switch (item.Coletor)
        {
            case ColetorIndicadorSisreg.Faltas:
                return new TrabalhoFaltas(item, fracao);
            case ColetorIndicadorSisreg.Canceladas when item.Escopo == PlanoColetaIndicadores.EscopoAmostra:
                return new TrabalhoCanceladasAmostra(item, Math.Max(2, _opcoes.PaginasDaAmostra));
            case ColetorIndicadorSisreg.Canceladas:
                return new TrabalhoCanceladasMes(item);
            case ColetorIndicadorSisreg.Desfechos when item.Escopo == TrabalhoUnidades.Escopo:
                return new TrabalhoUnidades(item);
            case ColetorIndicadorSisreg.Desfechos:
                return new TrabalhoDesfechos(item);
            default:
                // O SISREG identifica a central pelo IBGE de 6 dígitos (sem o verificador).
                var inst = await scope.ServiceProvider.GetRequiredService<IInstituicaoService>().ObterAsync(ct);
                var ibge = inst.CodigoIbge is { Length: >= 6 } c ? c[..6] : null;
                return new TrabalhoPpi(item, ibge, fracao);
        }
    }

    /// <summary>Todos dividem a mesma sessão e o mesmo orçamento: com trabalho vivo de outro motor, espera.</summary>
    private bool OutroMotorVivo() =>
        varreduraEstadoVivo.ObterAtual() is not null
        || importacaoEstadoVivo.ObterAtual() is not null
        || loteEstadoVivo.EmExecucao
        || escalasEstadoVivo.EmExecucao
        || filaEstadoVivo.EmExecucao;
}
