using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Regulacao.Conciliacao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Integracoes.EsusSgWeb.Varredura;

/// <summary>Motor de sincronização do ESUS de São Gonçalo → espelho <c>esussg_*</c> (ADR-0063).</summary>
public interface IEsusSgSincronizacaoService
{
    Task<Guid> ExecutarAsync(
        ModoVarreduraEsusSg modo,
        DisparoSincronizacao disparo,
        DateOnly inicio,
        DateOnly fim,
        Guid? usuarioId,
        string? usuarioNome,
        Guid? execucaoParaRetomar,
        CancellationToken cancellationToken);
}

/// <summary>
/// Lê a fila e os agendados de Maricá no ESUS SG e grava o espelho — mesmo desenho do SERNIT
/// (execução com cursor retomável, upsert pela chave natural, diff de situação, gatilhos, carimbo
/// de paciente, conciliação com <c>regulacao_solicitacao</c>), adaptado ao que o ESUS oferece.
///
/// <para><b>Fases:</b></para>
/// <list type="number">
/// <item><b>Fila</b> — a fila inteira de exame e de consulta (650 linhas em ~7 páginas). Posição,
/// prioridade e pendência vêm daqui.</item>
/// <item><b>Agendados</b> — mês a mês na janela de DATA DO AGENDAMENTO, com cursor por mês (a carga
/// inicial lê de 2015 em diante: 4.924 exames de 2019 a 2026 medidos).</item>
/// <item><b>Saídas</b> — quem estava na fila e não aparece em nenhuma das duas listas vira
/// <see cref="SituacaoEsusSg.SaiuDaFila"/>. <b>Só</b> quando a fila daquele tipo fechou lido =
/// declarado e todos os meses lidos fecharam também — senão seria acusar saída por falha de leitura.</item>
/// </list>
///
/// <para><b>Trilha montada:</b> o ESUS não mostra histórico por pedido à conta de Maricá. Os eventos
/// saem dos marcos com data e autor das listas (inclusão na fila, agendamento) e das diferenças
/// entre rodadas (prioridade, pendência, reagendamento, resposta do paciente, saída).</para>
/// </summary>
public sealed class EsusSgSincronizacaoService(
    SmsMaisDbContext db,
    IEsusSgLeitorService leitor,
    IRegulacaoConciliacaoService conciliacao,
    EsusSg.IEsusSgCatalogoSyncService catalogo,
    ILogger<EsusSgSincronizacaoService> logger) : IEsusSgSincronizacaoService
{
    private const int TamanhoLote = 200;
    private const string LotacaoSaoGoncalo = "SÃO GONÇALO";

    private static readonly TipoRecursoEsusSg[] Tipos = [TipoRecursoEsusSg.Exame, TipoRecursoEsusSg.Consulta];

    public async Task<Guid> ExecutarAsync(
        ModoVarreduraEsusSg modo,
        DisparoSincronizacao disparo,
        DateOnly inicio,
        DateOnly fim,
        Guid? usuarioId,
        string? usuarioNome,
        Guid? execucaoParaRetomar,
        CancellationToken cancellationToken)
    {
        var execucao = execucaoParaRetomar is { } idRetomar
            ? await db.EsusSgVarreduraExecucoes.FirstOrDefaultAsync(x => x.Id == idRetomar, cancellationToken)
            : null;

        if (execucao is not null)
        {
            execucao.Status = StatusVarreduraEsusSg.EmExecucao;
            execucao.Retomadas++;
            execucao.RetomadaEm = DateTime.UtcNow;
            logger.LogInformation(
                "ESUS SG: retomando execução {Execucao} na fase {Fase} (retomada nº {N}); cursor mês={Mes}.",
                execucao.Id, execucao.Fase, execucao.Retomadas, execucao.CursorMes);
        }
        else
        {
            execucao = new EsusSgVarreduraExecucao
            {
                Id = Guid.NewGuid(),
                Modo = modo,
                Disparo = disparo,
                Status = StatusVarreduraEsusSg.EmExecucao,
                Fase = FaseVarreduraEsusSg.Fila,
                JanelaInicio = inicio,
                JanelaFim = fim,
                IniciadoEm = DateTime.UtcNow,
                CriadoPor = usuarioId,
                CriadoPorNome = usuarioNome,
            };
            db.EsusSgVarreduraExecucoes.Add(execucao);
        }
        execucao.UltimoSinalEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        // O que foi visto NESTA rodada (retomada relê a fila — é barato — para a detecção de
        // saída não depender de estado em memória perdido na queda).
        var filaCompleta = new Dictionary<TipoRecursoEsusSg, bool>();

        try
        {
            // O catálogo primeiro (1 requisição): pedido novo de procedimento novo já nasce com
            // procedimento canônico, e a análise de regras o alcança na passada seguinte.
            try
            {
                var cat = await catalogo.SincronizarAsync(cancellationToken);
                execucao.Requisicoes++;
                if (cat.Mudou) logger.LogInformation("ESUS SG: catálogo atualizado ({Lidos} procedimentos).", cat.Lidos);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not EscritaNoEsusSgBloqueadaException)
            {
                logger.LogWarning(ex, "ESUS SG: catálogo não atualizou nesta rodada; seguindo com a fila.");
            }

            execucao.Fase = FaseVarreduraEsusSg.Fila;
            foreach (var tipo in Tipos)
            {
                filaCompleta[tipo] = await VarrerFilaAsync(execucao, tipo, cancellationToken);
            }

            var agendadosCompletos = true;
            if (modo != ModoVarreduraEsusSg.SomenteFila)
            {
                execucao.Fase = FaseVarreduraEsusSg.Agendados;
                execucao.UltimoSinalEm = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                agendadosCompletos = await VarrerAgendadosAsync(execucao, cancellationToken);

                foreach (var tipo in Tipos)
                {
                    if (filaCompleta[tipo] && agendadosCompletos)
                    {
                        await MarcarSaidasAsync(execucao, tipo, cancellationToken);
                    }
                    else
                    {
                        logger.LogWarning(
                            "ESUS SG: detecção de saída da fila de {Tipo} pulada — leitura incompleta nesta rodada.",
                            tipo);
                    }
                }
            }

            execucao.Fase = FaseVarreduraEsusSg.Finalizada;
            execucao.Status = execucao.MesesIncompletos > 0 || filaCompleta.Values.Any(ok => !ok)
                ? StatusVarreduraEsusSg.Parcial
                : StatusVarreduraEsusSg.Concluida;
        }
        catch (OperationCanceledException)
        {
            // Queda do serviço: fica retomável pelo cursor (o runner retoma na subida).
            execucao.Status = StatusVarreduraEsusSg.Interrompida;
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ESUS SG: varredura {Execucao} falhou.", execucao.Id);
            execucao.Status = StatusVarreduraEsusSg.Erro;
            execucao.MensagemErro = Truncar(ex.Message, 2000);
            db.EsusSgVarreduraFalhas.Add(NovaFalha(execucao,
                ex is EscritaNoEsusSgBloqueadaException ? TipoFalhaEsusSg.EscritaBloqueada : TipoFalhaEsusSg.ErroSessao,
                null, null, ex.Message, ex.ToString()));
        }
        finally
        {
            execucao.FinalizadoEm = DateTime.UtcNow;
            execucao.DuracaoSegundos = (int)(execucao.FinalizadoEm.Value - execucao.IniciadoEm).TotalSeconds;
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return execucao.Id;
    }

    // ------------------------------------------------------------------ fila

    private async Task<bool> VarrerFilaAsync(
        EsusSgVarreduraExecucao execucao, TipoRecursoEsusSg tipo, CancellationToken cancellationToken)
    {
        LeituraEsusSg<EsusSgLinhaFila> leitura;
        try
        {
            leitura = await leitor.LerFilaAsync(tipo, cancellationToken);
        }
        catch (EsusSgRespostaErroException ex)
        {
            db.EsusSgVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaEsusSg.ErroFila, tipo, null, ex.Message, null));
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        execucao.Requisicoes += leitura.Requisicoes;
        execucao.NaFila += leitura.Linhas.Count;
        if (!leitura.Completa)
        {
            db.EsusSgVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaEsusSg.ContagemNaoFechou, tipo, null,
                $"Fila de {tipo}: lido {leitura.Linhas.Count} ≠ declarado {leitura.Declarado}.", null));
        }

        foreach (var lote in leitura.Linhas.Chunk(TamanhoLote))
        {
            await AplicarFilaAsync(execucao, tipo, lote, cancellationToken);
        }

        execucao.UltimoSinalEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return leitura.Completa;
    }

    private async Task AplicarFilaAsync(
        EsusSgVarreduraExecucao execucao, TipoRecursoEsusSg tipo, IReadOnlyList<EsusSgLinhaFila> linhas,
        CancellationToken cancellationToken)
    {
        var (existentes, chavesEvento) = await CarregarAsync(tipo, linhas.Select(l => l.IdEsusSg), cancellationToken);
        var agora = DateTime.UtcNow;

        foreach (var linha in linhas)
        {
            var situacao = EhPendenciaAtiva(linha.Pendencia) ? SituacaoEsusSg.Pendente : SituacaoEsusSg.EmFila;

            if (!existentes.TryGetValue(linha.IdEsusSg, out var atual))
            {
                atual = NovaSolicitacao(tipo, linha.IdEsusSg, situacao, agora);
                PreencherDaFila(atual, linha);
                atual.VistoNaFilaEm = agora;
                db.EsusSgSolicitacoes.Add(atual);
                existentes[linha.IdEsusSg] = atual;
                execucao.SolicitacoesNovas++;

                RegistrarGatilho(execucao, atual, TipoGatilhoEsusSg.NovaSolicitacao, situacao.ToString(),
                    null, situacao, new { linha.Recurso, linha.Prioridade, linha.DataEntradaFila });
                AdicionarInclusao(execucao, atual, chavesEvento, agora);
                continue;
            }

            var situacaoAnterior = atual.Situacao;
            var prioridadeAnterior = atual.Prioridade;
            var retrato = RetratoDoPaciente(atual);

            PreencherDaFila(atual, linha);
            MarcarSeMudouOPaciente(atual, retrato, agora);
            atual.VistoNaFilaEm = agora;
            atual.SincronizadoEm = agora;
            atual.AtualizadoEm = agora;
            execucao.SolicitacoesAtualizadas++;

            if (situacaoAnterior != situacao)
            {
                MudarSituacao(execucao, atual, situacaoAnterior, situacao, agora);
                // Voltou para a fila: o agendamento antigo não vale mais (a trilha guarda a história).
                if (situacaoAnterior == SituacaoEsusSg.Agendada) LimparAgendamento(atual);
                var (rotulo, tipoEvento) = (situacaoAnterior, situacao) switch
                {
                    (SituacaoEsusSg.Agendada, _) => ("Retorno à fila", TipoEventoExterno.RetornarParaFila),
                    (SituacaoEsusSg.SaiuDaFila, _) => ("Reapareceu na fila", TipoEventoExterno.RetornarParaFila),
                    (_, SituacaoEsusSg.Pendente) => ("Pendência", TipoEventoExterno.Pendenciar),
                    _ => ("Pendência resolvida", TipoEventoExterno.Outro),
                };
                AdicionarEvento(execucao, atual, chavesEvento, agora, rotulo, tipoEvento,
                    usuario: null, lotacao: null, unidade: null,
                    anterior: situacaoAnterior.ToString(), atualTxt: situacao.ToString(),
                    observacao: situacao == SituacaoEsusSg.Pendente ? linha.Pendencia : "Percebido pela varredura.",
                    agora);
            }

            if (!string.IsNullOrWhiteSpace(prioridadeAnterior)
                && !string.Equals(prioridadeAnterior, linha.Prioridade, StringComparison.OrdinalIgnoreCase))
            {
                RegistrarGatilho(execucao, atual, TipoGatilhoEsusSg.MudancaPrioridade,
                    $"{linha.Prioridade}@{agora:yyyyMMddHHmmss}", situacao, situacao,
                    new { de = prioridadeAnterior, para = linha.Prioridade });
                AdicionarEvento(execucao, atual, chavesEvento, agora, "Mudança de prioridade", TipoEventoExterno.Outro,
                    usuario: linha.Regulador, lotacao: null, unidade: null,
                    anterior: prioridadeAnterior, atualTxt: linha.Prioridade,
                    observacao: "Percebido pela varredura.", agora);
            }

            // Já existia (ex.: nasceu nos agendados de uma carga) e ainda não tem o marco de inclusão.
            AdicionarInclusao(execucao, atual, chavesEvento, agora);
        }

        await SalvarEConciliarAsync(linhas.Select(l => l.IdEsusSg), cancellationToken);
    }

    // ------------------------------------------------------------------ agendados

    private async Task<bool> VarrerAgendadosAsync(EsusSgVarreduraExecucao execucao, CancellationToken cancellationToken)
    {
        var primeiroMes = new DateOnly(execucao.JanelaInicio.Year, execucao.JanelaInicio.Month, 1);
        var mes = execucao.CursorMes is { } cursor && cursor > primeiroMes ? cursor : primeiroMes;
        var completo = execucao.MesesIncompletos == 0;

        for (; mes <= execucao.JanelaFim; mes = mes.AddMonths(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var de = mes < execucao.JanelaInicio ? execucao.JanelaInicio : mes;
            var ultimoDia = mes.AddMonths(1).AddDays(-1);
            var ate = ultimoDia > execucao.JanelaFim ? execucao.JanelaFim : ultimoDia;

            foreach (var tipo in Tipos)
            {
                LeituraEsusSg<EsusSgLinhaAgendado> leitura;
                try
                {
                    leitura = await leitor.LerAgendadosAsync(tipo, de, ate, cancellationToken);
                }
                catch (EsusSgRespostaErroException ex)
                {
                    db.EsusSgVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaEsusSg.ErroAgendados, tipo, mes, ex.Message, null));
                    execucao.MesesIncompletos++;
                    completo = false;
                    continue;
                }

                execucao.Requisicoes += leitura.Requisicoes;
                execucao.AgendadosLidos += leitura.Linhas.Count;
                if (!leitura.Completa)
                {
                    db.EsusSgVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaEsusSg.ContagemNaoFechou, tipo, mes,
                        $"Agendados de {tipo} em {mes:MM/yyyy}: lido {leitura.Linhas.Count} ≠ declarado {leitura.Declarado}.", null));
                    execucao.MesesIncompletos++;
                    completo = false;
                }

                foreach (var lote in leitura.Linhas.Chunk(TamanhoLote))
                {
                    await AplicarAgendadosAsync(execucao, tipo, lote, cancellationToken);
                }
            }

            // Cursor e lote na mesma gravação: a retomada recomeça do mês seguinte, nunca pula.
            execucao.CursorMes = mes.AddMonths(1);
            execucao.UltimoSinalEm = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return completo;
    }

    private async Task AplicarAgendadosAsync(
        EsusSgVarreduraExecucao execucao, TipoRecursoEsusSg tipo, IReadOnlyList<EsusSgLinhaAgendado> linhas,
        CancellationToken cancellationToken)
    {
        var (existentes, chavesEvento) = await CarregarAsync(tipo, linhas.Select(l => l.IdEsusSg), cancellationToken);
        var agora = DateTime.UtcNow;
        var hoje = FusoBrasilia.HojeEmBrasilia();

        // Uma linha = um agendamento; o pedido pode ter vários (sessões). O espelho do pedido mostra
        // o PRINCIPAL — o próximo a acontecer, ou o último se todos já passaram — e cada sessão vira
        // um marco na trilha.
        foreach (var grupo in linhas.GroupBy(l => l.IdEsusSg, StringComparer.Ordinal))
        {
            var sessoes = grupo.ToList();
            var principal = Principal(sessoes, hoje);
            var datasDoPedido = sessoes.Select(TextoData).Where(d => d is not null).ToHashSet(StringComparer.Ordinal);

            if (!existentes.TryGetValue(grupo.Key, out var atual))
            {
                atual = NovaSolicitacao(tipo, grupo.Key, SituacaoEsusSg.Agendada, agora);
                PreencherDoAgendado(atual, principal);
                atual.VistoNosAgendadosEm = agora;
                db.EsusSgSolicitacoes.Add(atual);
                existentes[grupo.Key] = atual;
                execucao.SolicitacoesNovas++;

                RegistrarGatilho(execucao, atual, TipoGatilhoEsusSg.NovaSolicitacao, SituacaoEsusSg.Agendada.ToString(),
                    null, SituacaoEsusSg.Agendada, new { principal.Recurso, principal.DataAgendada, principal.UnidadeExecutora });
                AdicionarInclusao(execucao, atual, chavesEvento, agora);
                foreach (var s in sessoes) AdicionarAgendamento(execucao, atual, s, chavesEvento, agora);
                continue;
            }

            var situacaoAnterior = atual.Situacao;
            var dataAnterior = atual.DataHoraAgendadaTexto ?? atual.DataAgendada?.ToString("dd/MM/yyyy");
            var respostaAnterior = atual.NotificacaoResposta;
            var retrato = RetratoDoPaciente(atual);

            PreencherDoAgendado(atual, principal);
            MarcarSeMudouOPaciente(atual, retrato, agora);
            atual.VistoNosAgendadosEm = agora;
            atual.SincronizadoEm = agora;
            atual.AtualizadoEm = agora;
            execucao.SolicitacoesAtualizadas++;

            if (situacaoAnterior != SituacaoEsusSg.Agendada)
            {
                MudarSituacao(execucao, atual, situacaoAnterior, SituacaoEsusSg.Agendada, agora);
            }
            else if (!string.IsNullOrWhiteSpace(dataAnterior) && !datasDoPedido.Contains(dataAnterior))
            {
                // Remarcação de verdade: a data que tínhamos SUMIU do conjunto de sessões do pedido.
                // (A troca do principal da sessão 1 para a 2, quando a 1 passa, não é remarcação.)
                var dataNova = TextoData(principal);
                RegistrarGatilho(execucao, atual, TipoGatilhoEsusSg.MudancaAgendamento,
                    $"{dataNova}@{agora:yyyyMMddHHmmss}", SituacaoEsusSg.Agendada, SituacaoEsusSg.Agendada,
                    new { de = dataAnterior, para = dataNova });
                AdicionarEvento(execucao, atual, chavesEvento, agora, "Reagendamento", TipoEventoExterno.Reagendar,
                    usuario: principal.UsuarioAgendamento, lotacao: LotacaoSaoGoncalo, unidade: principal.UnidadeExecutora,
                    anterior: dataAnterior, atualTxt: dataNova, observacao: "Percebido pela varredura.", agora);
            }

            AdicionarInclusao(execucao, atual, chavesEvento, agora);
            foreach (var s in sessoes) AdicionarAgendamento(execucao, atual, s, chavesEvento, agora);

            if (!string.IsNullOrWhiteSpace(principal.NotificacaoResposta)
                && !string.Equals(respostaAnterior, principal.NotificacaoResposta, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(principal.NotificacaoResposta, "AGUARDANDO", StringComparison.OrdinalIgnoreCase))
            {
                AdicionarEvento(execucao, atual, chavesEvento, agora,
                    $"Resposta do paciente ({principal.NotificacaoTipo ?? "aviso"})", TipoEventoExterno.WhatsApp,
                    usuario: null, lotacao: LotacaoSaoGoncalo, unidade: principal.UnidadeExecutora,
                    anterior: respostaAnterior, atualTxt: principal.NotificacaoResposta,
                    observacao: "Resposta à notificação que o próprio ESUS enviou.", agora);
            }
        }

        await SalvarEConciliarAsync(linhas.Select(l => l.IdEsusSg), cancellationToken);
    }

    /// <summary>O agendamento que representa o pedido: o próximo (hoje em diante); se todos já
    /// passaram, o mais recente.</summary>
    internal static EsusSgLinhaAgendado Principal(IReadOnlyList<EsusSgLinhaAgendado> sessoes, DateOnly hoje)
    {
        static DateTime Quando(EsusSgLinhaAgendado s) =>
            s.DataHoraAgendada ?? s.DataAgendada?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MinValue;
        var futuras = sessoes.Where(s => s.DataAgendada is { } d && d >= hoje).OrderBy(Quando).ToList();
        return futuras.Count > 0 ? futuras[0] : sessoes.OrderByDescending(Quando).First();
    }

    private static string? TextoData(EsusSgLinhaAgendado s) =>
        s.DataHoraAgendadaTexto ?? s.DataAgendada?.ToString("dd/MM/yyyy");

    // ------------------------------------------------------------------ saídas

    /// <summary>Quem estava na fila (ou agendado dentro da janela lida) e não apareceu em nenhuma
    /// das duas listas nesta rodada. O motivo não é visível à conta de Maricá — declarado assim.</summary>
    private async Task MarcarSaidasAsync(
        EsusSgVarreduraExecucao execucao, TipoRecursoEsusSg tipo, CancellationToken cancellationToken)
    {
        var inicioRodada = execucao.IniciadoEm;
        var janelaIni = execucao.JanelaInicio;
        var janelaFim = execucao.JanelaFim;

        var sumidos = await db.EsusSgSolicitacoes
            .Where(x => x.Tipo == tipo && x.ExcluidoEm == null
                && (x.VistoNaFilaEm == null || x.VistoNaFilaEm < inicioRodada)
                && (x.VistoNosAgendadosEm == null || x.VistoNosAgendadosEm < inicioRodada)
                && ((x.Situacao == SituacaoEsusSg.EmFila || x.Situacao == SituacaoEsusSg.Pendente)
                    || (x.Situacao == SituacaoEsusSg.Agendada && x.DataAgendada != null
                        && x.DataAgendada >= janelaIni && x.DataAgendada <= janelaFim)))
            .ToListAsync(cancellationToken);
        if (sumidos.Count == 0) return;

        var ids = sumidos.Select(s => s.Id).ToList();
        var chaves = (await db.EsusSgEventos.AsNoTracking()
                .Where(e => ids.Contains(e.EsusSgSolicitacaoId))
                .Select(e => new { e.EsusSgSolicitacaoId, e.DataEvento, e.Evento })
                .ToListAsync(cancellationToken))
            .Select(e => ChaveEvento(e.EsusSgSolicitacaoId, e.DataEvento, e.Evento))
            .ToHashSet(StringComparer.Ordinal);

        var agora = DateTime.UtcNow;
        foreach (var s in sumidos)
        {
            var anterior = s.Situacao;
            MudarSituacao(execucao, s, anterior, SituacaoEsusSg.SaiuDaFila, agora);
            AdicionarEvento(execucao, s, chaves, agora,
                anterior == SituacaoEsusSg.Agendada ? "Agendamento não consta mais" : "Saiu da fila",
                TipoEventoExterno.Outro, usuario: null, lotacao: null, unidade: s.UnidadeExecutora,
                anterior: anterior.ToString(), atualTxt: SituacaoEsusSg.SaiuDaFila.ToString(),
                observacao: "Não consta mais na fila nem nos agendados do ESUS. A conta da unidade solicitante não vê "
                    + "o motivo (exclusão, cancelamento ou transferência).", agora);
            s.AtualizadoEm = agora;
            execucao.SaidasDaFila++;
        }

        await SalvarEConciliarAsync(sumidos.Select(s => s.IdEsusSg), cancellationToken);
        logger.LogInformation("ESUS SG: {N} pedido(s) de {Tipo} saíram da fila nesta rodada.", sumidos.Count, tipo);
    }

    // ------------------------------------------------------------------ preenchimento

    private static EsusSgSolicitacao NovaSolicitacao(
        TipoRecursoEsusSg tipo, string id, SituacaoEsusSg situacao, DateTime agora) => new()
        {
            Id = Guid.NewGuid(),
            IdEsusSg = id,
            Tipo = tipo,
            Situacao = situacao,
            CriadoEm = agora,
            SincronizadoEm = agora,
            PacienteConciliarEm = agora,
        };

    private static void PreencherDaFila(EsusSgSolicitacao a, EsusSgLinhaFila l)
    {
        a.Recurso = Truncar(l.Recurso, 300)!;
        a.CodigoInterno = Truncar(l.CodigoInterno, 20);
        a.Subprocedimentos = l.Subprocedimentos;
        a.DataSolicitacao = l.DataSolicitacao ?? a.DataSolicitacao;
        a.DataEntradaFila = l.DataEntradaFila ?? a.DataEntradaFila;
        a.Prioridade = Truncar(l.Prioridade, 80);
        a.PrioridadeCor = Truncar(l.PrioridadeCor, 20);
        a.Pendencia = Truncar(l.Pendencia, 300);
        a.PosicaoFila = l.PosicaoFila;
        a.OrdemEntrada = l.OrdemEntrada;
        a.ProfissionalSolicitante = Truncar(l.ProfissionalSolicitante, 200) ?? a.ProfissionalSolicitante;
        a.UnidadeSolicitante = Truncar(l.UnidadeSolicitante, 200) ?? a.UnidadeSolicitante;
        a.UsuarioInclusao = Truncar(l.UsuarioInclusao, 200) ?? a.UsuarioInclusao;
        a.Regulador = Truncar(l.Regulador, 200) ?? a.Regulador;
        a.PessoaIdEsus = Truncar(l.PessoaIdEsus, 20) ?? a.PessoaIdEsus;
        a.PacienteNome = Truncar(l.PacienteNome, 200)!;
        a.Cpf = l.Cpf ?? a.Cpf;
        a.Cns = l.Cns ?? a.Cns;
        a.DataNascimento = l.DataNascimento ?? a.DataNascimento;
        a.Sexo = Truncar(l.Sexo, 10) ?? a.Sexo;
        a.NomeMae = Truncar(l.NomeMae, 200) ?? a.NomeMae;
        a.Telefone = Truncar(l.Telefone, 40) ?? a.Telefone;
        a.Celular = Truncar(l.Celular, 40) ?? a.Celular;
        a.MunicipioPaciente = Truncar(l.MunicipioPaciente, 120) ?? a.MunicipioPaciente;
        a.Bairro = Truncar(l.Bairro, 150) ?? a.Bairro;
    }

    /// <summary>A lista de agendados traz menos do paciente que a fila (sem CNS, nascimento, mãe):
    /// só sobrescreve o que ela tem, para não apagar o que a fila trouxe.</summary>
    private static void PreencherDoAgendado(EsusSgSolicitacao a, EsusSgLinhaAgendado l)
    {
        a.Recurso = Truncar(l.Recurso, 300)!;
        a.DataSolicitacao = l.DataSolicitacao ?? a.DataSolicitacao;
        a.DataEntradaFila = l.DataEntradaFila ?? a.DataEntradaFila;
        a.Prioridade = Truncar(l.Prioridade, 80) ?? a.Prioridade;
        a.PrioridadeCor = Truncar(l.PrioridadeCor, 20) ?? a.PrioridadeCor;
        a.PosicaoFila = null; // saiu da fila: posição não existe mais
        a.ProfissionalSolicitante = Truncar(l.ProfissionalSolicitante, 200) ?? a.ProfissionalSolicitante;
        a.UnidadeSolicitante = Truncar(l.UnidadeSolicitante, 200) ?? a.UnidadeSolicitante;
        a.UsuarioInclusao = Truncar(l.UsuarioInclusao, 200) ?? a.UsuarioInclusao;
        a.PessoaIdEsus = Truncar(l.PessoaIdEsus, 20) ?? a.PessoaIdEsus;
        a.PacienteNome = Truncar(l.PacienteNome, 200) ?? a.PacienteNome;
        a.Cpf = l.Cpf ?? a.Cpf;
        a.Telefone = Truncar(l.Telefone, 40) ?? a.Telefone;

        a.UnidadeExecutora = Truncar(l.UnidadeExecutora, 300);
        a.CnesExecutora = l.CnesExecutora;
        a.Setor = Truncar(l.Setor, 200);
        a.Local = Truncar(l.Local, 200);
        a.DataAgendada = l.DataAgendada;
        a.DataHoraAgendadaTexto = Truncar(l.DataHoraAgendadaTexto, 40);
        a.UsuarioAgendamento = Truncar(l.UsuarioAgendamento, 200);
        a.AgendamentoCadastradoEm = l.AgendamentoCadastradoEm;
        a.DataSaidaFila = l.DataSaidaFila;
        a.ComprovanteImpresso = l.ComprovanteImpresso;
        a.AgendadoTfd = l.AgendadoTfd;
        a.NotificacaoTipo = Truncar(l.NotificacaoTipo, 40);
        a.NotificacaoEntrega = Truncar(l.NotificacaoEntrega, 40);
        a.NotificacaoResposta = Truncar(l.NotificacaoResposta, 60);
    }

    private static void LimparAgendamento(EsusSgSolicitacao a)
    {
        a.UnidadeExecutora = null;
        a.CnesExecutora = null;
        a.Setor = null;
        a.Local = null;
        a.DataAgendada = null;
        a.DataHoraAgendadaTexto = null;
        a.UsuarioAgendamento = null;
        a.AgendamentoCadastradoEm = null;
        a.DataSaidaFila = null;
        a.NotificacaoTipo = null;
        a.NotificacaoEntrega = null;
        a.NotificacaoResposta = null;
    }

    /// <summary>"NAO" e "TODAS RESOLVIDAS" são os dois valores de "sem pendência" medidos
    /// (624 e 31 em 655). Qualquer outro texto é pendência ativa.</summary>
    internal static bool EhPendenciaAtiva(string? pendencia) =>
        !string.IsNullOrWhiteSpace(pendencia)
        && !pendencia.Trim().Equals("NAO", StringComparison.OrdinalIgnoreCase)
        && !pendencia.Trim().Equals("NÃO", StringComparison.OrdinalIgnoreCase)
        && !pendencia.Trim().Equals("TODAS RESOLVIDAS", StringComparison.OrdinalIgnoreCase);

    private static string RetratoDoPaciente(EsusSgSolicitacao s) => string.Join('|', [
        s.PacienteNome, s.Cpf, s.Cns, s.NomeMae, s.Sexo, s.DataNascimento?.ToString("O"),
        s.MunicipioPaciente, s.Bairro, s.Telefone, s.Celular,
    ]);

    private static void MarcarSeMudouOPaciente(EsusSgSolicitacao alvo, string antes, DateTime agora)
    {
        if (RetratoDoPaciente(alvo) != antes) alvo.PacienteConciliarEm = agora;
    }

    // ------------------------------------------------------------------ eventos e gatilhos

    private void MudarSituacao(
        EsusSgVarreduraExecucao execucao, EsusSgSolicitacao s, SituacaoEsusSg de, SituacaoEsusSg para, DateTime agora)
    {
        s.SituacaoAnterior = de;
        s.Situacao = para;
        s.SituacaoMudouEm = agora;
        execucao.MudancasSituacao++;
        RegistrarGatilho(execucao, s, TipoGatilhoEsusSg.MudancaSituacao,
            $"{de}>{para}@{agora:yyyyMMddHHmmss}", de, para, new { de = de.ToString(), para = para.ToString() });
    }

    /// <summary>Marco "Inclusão na fila" — data de entrada (sem hora: meia-noite de Brasília) e o
    /// operador da unidade solicitante que incluiu, com a lotação como o ESUS escreve a unidade
    /// (nada institucional em código — ADR-0043). É o que alimenta a estatística de quem inclui.</summary>
    private void AdicionarInclusao(
        EsusSgVarreduraExecucao execucao, EsusSgSolicitacao s, HashSet<string> chaves, DateTime agora)
    {
        if (s.DataEntradaFila is not { } entrada) return;
        AdicionarEvento(execucao, s, chaves, MeiaNoiteBrasilia(entrada), "Inclusão na fila", TipoEventoExterno.Solicitar,
            usuario: s.UsuarioInclusao, lotacao: s.UnidadeSolicitante, unidade: null,
            anterior: null, atualTxt: SituacaoEsusSg.EmFila.ToString(),
            observacao: s.Prioridade is null ? null : $"Prioridade: {s.Prioridade}", agora);
    }

    /// <summary>Marco de agendamento de UMA sessão — data em que SG marcou, o operador de SG e a data
    /// marcada no rótulo (o pedido pode ter várias sessões cadastradas no mesmo dia).</summary>
    private void AdicionarAgendamento(
        EsusSgVarreduraExecucao execucao, EsusSgSolicitacao s, EsusSgLinhaAgendado sessao,
        HashSet<string> chaves, DateTime agora)
    {
        var quando = sessao.AgendamentoCadastradoEm ?? sessao.DataSaidaFila;
        if (quando is null) return;
        var para = TextoData(sessao);
        var rotulo = para is null ? "Agendamento" : $"Agendamento para {Truncar(para, 16)}";
        AdicionarEvento(execucao, s, chaves, MeiaNoiteBrasilia(quando.Value), rotulo, TipoEventoExterno.Agendar,
            usuario: sessao.UsuarioAgendamento, lotacao: LotacaoSaoGoncalo, unidade: sessao.UnidadeExecutora,
            anterior: SituacaoEsusSg.EmFila.ToString(), atualTxt: SituacaoEsusSg.Agendada.ToString(),
            observacao: sessao.Setor is null ? null : $"Setor: {sessao.Setor}", agora);
    }

    private void AdicionarEvento(
        EsusSgVarreduraExecucao execucao, EsusSgSolicitacao s, HashSet<string> chaves, DateTime data,
        string evento, TipoEventoExterno tipo, string? usuario, string? lotacao, string? unidade,
        string? anterior, string? atualTxt, string? observacao, DateTime agora)
    {
        if (!chaves.Add(ChaveEvento(s.Id, data, evento))) return;
        db.EsusSgEventos.Add(new EsusSgEvento
        {
            Id = Guid.NewGuid(),
            EsusSgSolicitacaoId = s.Id,
            DataEvento = data,
            Evento = evento,
            TipoEvento = tipo,
            EstadoAnterior = Truncar(anterior, 60),
            EstadoAtual = Truncar(atualTxt, 60),
            UnidadeExecutora = Truncar(unidade, 300),
            Usuario = Truncar(usuario, 200),
            LotacaoEvento = lotacao,
            Observacao = observacao,
            CapturadoEm = agora,
        });
        s.EventosCount++;
        if (s.UltimoEventoEm is null || data > s.UltimoEventoEm) s.UltimoEventoEm = data;
        execucao.EventosNovos++;
    }

    private void RegistrarGatilho(
        EsusSgVarreduraExecucao execucao, EsusSgSolicitacao s, TipoGatilhoEsusSg tipo, string chave,
        SituacaoEsusSg? anterior, SituacaoEsusSg? atual, object? payload)
    {
        db.EsusSgGatilhos.Add(new EsusSgGatilho
        {
            Id = Guid.NewGuid(),
            EsusSgSolicitacaoId = s.Id,
            IdEsusSg = s.IdEsusSg,
            Tipo = tipo,
            ChaveEvento = Truncar(chave, 80)!,
            SituacaoAnterior = anterior,
            SituacaoAtual = atual,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            CriadoEm = DateTime.UtcNow,
        });
        execucao.GatilhosGerados++;
    }

    // ------------------------------------------------------------------ util

    private async Task<(Dictionary<string, EsusSgSolicitacao> Existentes, HashSet<string> ChavesEvento)> CarregarAsync(
        TipoRecursoEsusSg tipo, IEnumerable<string> idsEsus, CancellationToken cancellationToken)
    {
        var ids = idsEsus.Distinct(StringComparer.Ordinal).ToList();
        var existentes = await db.EsusSgSolicitacoes
            .Where(x => x.Tipo == tipo && ids.Contains(x.IdEsusSg) && x.ExcluidoEm == null)
            .ToDictionaryAsync(x => x.IdEsusSg, StringComparer.Ordinal, cancellationToken);

        var idsInternos = existentes.Values.Select(e => e.Id).ToList();
        var chaves = (await db.EsusSgEventos.AsNoTracking()
                .Where(e => idsInternos.Contains(e.EsusSgSolicitacaoId))
                .Select(e => new { e.EsusSgSolicitacaoId, e.DataEvento, e.Evento })
                .ToListAsync(cancellationToken))
            .Select(e => ChaveEvento(e.EsusSgSolicitacaoId, e.DataEvento, e.Evento))
            .ToHashSet(StringComparer.Ordinal);
        return (existentes, chaves);
    }

    private async Task SalvarEConciliarAsync(IEnumerable<string> idsEsus, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);

        // A carga inicial grava ~280 lotes: sem soltar o que já foi gravado, cada SaveChanges varre
        // milhares de entidades rastreadas. A execução continua rastreada (os contadores dela seguem).
        foreach (var e in db.ChangeTracker.Entries()
                     .Where(e => e.Entity is not EsusSgVarreduraExecucao && e.State == EntityState.Unchanged)
                     .ToList())
        {
            e.State = EntityState.Detached;
        }

        // Espelho gravado: casa com as solicitações abertas no SMSMais pelo número externo
        // (plano 05). Em try/catch — um caso torto não derruba a varredura.
        var ids = idsEsus.Distinct(StringComparer.Ordinal).ToList();
        try
        {
            await conciliacao.ConciliarEsusSgAsync(ids, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Conciliação da regulação falhou no lote do ESUS SG ({N} ids).", ids.Count);
        }
    }

    internal static DateTime MeiaNoiteBrasilia(DateOnly d) =>
        FusoBrasilia.DeBrasiliaParaUtc(d.ToDateTime(TimeOnly.MinValue));

    private static string ChaveEvento(Guid solicitacao, DateTime data, string evento) =>
        $"{solicitacao:N}|{data.ToUniversalTime():O}|{evento.Trim().ToLowerInvariant()}";

    private static EsusSgVarreduraFalha NovaFalha(
        EsusSgVarreduraExecucao execucao, TipoFalhaEsusSg tipo, TipoRecursoEsusSg? recurso, DateOnly? mes,
        string mensagem, string? detalhe) => new()
        {
            Id = Guid.NewGuid(),
            ExecucaoId = execucao.Id,
            Tipo = tipo,
            TipoRecurso = recurso,
            Mes = mes,
            Mensagem = Truncar(mensagem, 1000)!,
            Detalhe = detalhe,
            CriadoEm = DateTime.UtcNow,
        };

    private static string? Truncar(string? texto, int max) =>
        texto is null ? null : texto.Length <= max ? texto : texto[..max];
}
