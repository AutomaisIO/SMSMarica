using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Tempo;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Integracoes.SernitWeb.Varredura;

/// <summary>
/// Aplica no NOSSO banco o que o motor leu do SERNIT: espelha a grade, faz o diff de situação, relê
/// o histórico de quem precisa e gera os gatilhos — subsistema irmão do SER-RJ (ADR-0042).
///
/// <para><b>Regra de releitura de histórico:</b> solicitação nova lê tudo; quem mudou de situação
/// relê e faz diff; e <b>todo mundo em <c>EmFila</c> relê todo dia</b> (FollowUP é
/// <c>Em fila -&gt; Em fila</c> e não aparece em diff de grade).</para>
///
/// <para><b>Diferença do SER-RJ:</b> a grade é lida por PAGINAÇÃO (não há export), inclusive a
/// situação <c>Alta</c> — o combo do SERNIT a oferece. Teto de 100 por consulta; o total real vem
/// no aviso da tela.</para>
/// </summary>
public interface ISernitSincronizacaoService
{
    Task<Guid> ExecutarAsync(
        ModoVarreduraSernit modo,
        DisparoSincronizacao disparo,
        DateOnly inicio,
        DateOnly fim,
        IReadOnlyList<SituacaoSernit>? situacoes,
        Guid? usuarioId,
        string? usuarioNome,
        Guid? execucaoParaRetomar,
        CancellationToken cancellationToken);
}

public sealed class SernitSincronizacaoService(
    SmsMaisDbContext db,
    ISernitLeitorService leitor,
    VarredorSernitPorPaginacao varredor,
    ILogger<SernitSincronizacaoService> logger) : ISernitSincronizacaoService
{
    private static readonly SituacaoSernit[] TodasSituacoes =
    [
        SituacaoSernit.EmFila,
        SituacaoSernit.Pendente,
        SituacaoSernit.Agendada,
        SituacaoSernit.ChegadaNaoConfirmada,
        SituacaoSernit.ChegadaConfirmada,
        SituacaoSernit.Cancelada,
        SituacaoSernit.Alta,
    ];

    public async Task<Guid> ExecutarAsync(
        ModoVarreduraSernit modo,
        DisparoSincronizacao disparo,
        DateOnly inicio,
        DateOnly fim,
        IReadOnlyList<SituacaoSernit>? situacoes,
        Guid? usuarioId,
        string? usuarioNome,
        Guid? execucaoParaRetomar,
        CancellationToken cancellationToken)
    {
        var alvo = situacoes is { Count: > 0 } ? situacoes : TodasSituacoes;

        var execucao = execucaoParaRetomar is { } idRetomar
            ? await db.SernitVarreduraExecucoes.FirstOrDefaultAsync(x => x.Id == idRetomar, cancellationToken)
            : null;

        if (execucao is not null)
        {
            execucao.Status = StatusVarreduraSernit.EmExecucao;
            execucao.Retomadas++;
            execucao.RetomadaEm = DateTime.UtcNow;
            logger.LogInformation(
                "SERNIT: retomando execução {Execucao} na fase {Fase} (retomada nº {N}); cursor: "
                + "situação={Situacao} data={Data} id={Id}.",
                execucao.Id, execucao.Fase, execucao.Retomadas,
                execucao.CursorSituacao, execucao.CursorData, execucao.CursorIdSernit);
        }
        else
        {
            execucao = new SernitVarreduraExecucao
            {
                Id = Guid.NewGuid(),
                Modo = modo,
                Disparo = disparo,
                Status = StatusVarreduraSernit.EmExecucao,
                Fase = FaseVarreduraSernit.Grade,
                JanelaInicio = inicio,
                JanelaFim = fim,
                SituacoesVarridas = string.Join(',', alvo.Select(s => s.ToString())),
                IniciadoEm = DateTime.UtcNow,
                CriadoPor = usuarioId,
                CriadoPorNome = usuarioNome,
            };
            db.SernitVarreduraExecucoes.Add(execucao);
        }
        execucao.UltimoSinalEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var precisamHistorico = new List<(string IdSernit, SituacaoSernit Situacao, string Motivo)>();

            if (execucao.Fase != FaseVarreduraSernit.Historico)
            {
                execucao.Fase = FaseVarreduraSernit.Grade;
                await VarrerGradeAsync(execucao, alvo, inicio, fim, precisamHistorico, cancellationToken);
            }
            else
            {
                logger.LogInformation(
                    "SERNIT: retomando {Execucao} direto na fase de histórico — a grade já foi varrida.",
                    execucao.Id);
            }

            if (modo != ModoVarreduraSernit.SomenteGrade)
            {
                execucao.Fase = FaseVarreduraSernit.Historico;
                execucao.UltimoSinalEm = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                await AplicarHistoricosAsync(execucao, modo, precisamHistorico, cancellationToken);
            }

            execucao.Fase = FaseVarreduraSernit.Finalizada;
            execucao.Status = execucao.FatiasTruncadas > 0
                ? StatusVarreduraSernit.Parcial
                : StatusVarreduraSernit.Concluida;
        }
        catch (OperationCanceledException)
        {
            execucao.Status = StatusVarreduraSernit.Cancelada;
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SERNIT: varredura {Execucao} falhou.", execucao.Id);
            execucao.Status = StatusVarreduraSernit.Erro;
            execucao.MensagemErro = Truncar(ex.Message, 2000);
        }
        finally
        {
            execucao.FinalizadoEm = DateTime.UtcNow;
            execucao.DuracaoSegundos = (int)(execucao.FinalizadoEm.Value - execucao.IniciadoEm).TotalSeconds;
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return execucao.Id;
    }

    // ------------------------------------------------------------------ grade

    private async Task VarrerGradeAsync(
        SernitVarreduraExecucao execucao,
        IReadOnlyList<SituacaoSernit> alvo,
        DateOnly inicio,
        DateOnly fim,
        List<(string, SituacaoSernit, string)> precisamHistorico,
        CancellationToken cancellationToken)
    {
        await leitor.PrepararAsync(cancellationToken);

        var indiceInicial = 0;
        var inicioDaPrimeira = inicio;

        if (execucao.CursorSituacao is { } cursorSituacao)
        {
            var i = alvo.ToList().IndexOf(cursorSituacao);
            if (i >= 0)
            {
                indiceInicial = i;
                inicioDaPrimeira = execucao.CursorData ?? inicio;
            }
        }

        for (var i = indiceInicial; i < alvo.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var situacao = alvo[i];
            var de = i == indiceInicial ? inicioDaPrimeira : inicio;
            if (de > fim) continue;

            var vistos = new HashSet<string>(StringComparer.Ordinal);
            var resultado = new ResultadoVarreduraSernit();
            var buscasAntes = execucao.Buscas;
            var paginasAntes = execucao.Paginas;
            var encontradasAntes = execucao.SolicitacoesEncontradas;

            async Task AplicarAsync(
                IReadOnlyList<SernitLinhaGrade> linhas, DateOnly cursorConcluido, CancellationToken ct)
            {
                if (linhas.Count > 0)
                {
                    await AplicarGradeAsync(execucao, situacao, linhas, vistos, precisamHistorico, ct);
                }

                execucao.CursorSituacao = situacao;
                execucao.CursorData = cursorConcluido;
                execucao.Buscas = buscasAntes + resultado.Buscas;
                execucao.Paginas = paginasAntes + resultado.Paginas;
                execucao.SolicitacoesEncontradas = encontradasAntes + vistos.Count;
                execucao.UltimoSinalEm = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);
            }

            await AplicarAsync([], de, cancellationToken);

            await varredor.VarrerAsync(situacao, de, fim, AplicarAsync, cancellationToken, resultado);
            RegistrarTruncadas(execucao, resultado);

            execucao.Buscas = buscasAntes + resultado.Buscas;
            execucao.Paginas = paginasAntes + resultado.Paginas;
            execucao.SolicitacoesEncontradas = encontradasAntes + vistos.Count;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private void RegistrarTruncadas(SernitVarreduraExecucao execucao, ResultadoVarreduraSernit resultado)
    {
        foreach (var fatia in resultado.Truncadas)
        {
            execucao.FatiasTruncadas++;
            db.SernitVarreduraFalhas.Add(new SernitVarreduraFalha
            {
                Id = Guid.NewGuid(),
                ExecucaoId = execucao.Id,
                Tipo = TipoFalhaSernit.FatiaTruncada,
                Situacao = fatia.Situacao,
                FatiaInicio = fatia.Dia,
                FatiaFim = fatia.Dia,
                TipoRecurso = fatia.Tipo,
                Mensagem =
                    $"Mais de 100 registros em {fatia.Dia:dd/MM/yyyy} ({fatia.Situacao}"
                    + (fatia.Tipo is { } t ? $", {t}" : string.Empty)
                    + "). A tela de Solicitação do SERNIT não devolve além de 100 — há registros NÃO lidos.",
                CriadoEm = DateTime.UtcNow,
            });
        }
    }

    private async Task AplicarGradeAsync(
        SernitVarreduraExecucao execucao,
        SituacaoSernit situacao,
        IReadOnlyList<SernitLinhaGrade> linhas,
        HashSet<string> vistos,
        List<(string, SituacaoSernit, string)> precisamHistorico,
        CancellationToken cancellationToken)
    {
        var doLote = new Dictionary<string, SernitLinhaGrade>(StringComparer.Ordinal);
        foreach (var l in linhas)
        {
            if (!string.IsNullOrWhiteSpace(l.IdSernit)) doLote[l.IdSernit] = l;
        }
        if (doLote.Count == 0) return;

        var ids = doLote.Keys.ToList();
        var existentes = await db.SernitSolicitacoes
            .Where(x => ids.Contains(x.IdSernit) && x.ExcluidoEm == null)
            .ToDictionaryAsync(x => x.IdSernit, cancellationToken);

        var agora = DateTime.UtcNow;

        foreach (var (idSernit, linha) in doLote)
        {
            vistos.Add(idSernit);

            if (!existentes.TryGetValue(idSernit, out var atual))
            {
                var nova = new SernitSolicitacao
                {
                    Id = Guid.NewGuid(),
                    IdSernit = idSernit,
                    CriadoEm = agora,
                    SincronizadoEm = agora,
                    Situacao = situacao,
                };
                PreencherDaGrade(nova, linha, situacao);
                nova.PacienteConciliarEm = agora;
                db.SernitSolicitacoes.Add(nova);

                execucao.SolicitacoesNovas++;
                RegistrarGatilho(execucao, nova, TipoGatilhoSernit.NovaSolicitacao, situacao.ToString(),
                    null, situacao, new { linha.Recurso, linha.Paciente, linha.DataSolicitacao });

                // Já nasce em Alta: o SERNIT não oferece o histórico dela e não vimos a transição,
                // então não há o que ler nem o que anexar — só marca indisponível (evita a falha).
                if (situacao == SituacaoSernit.Alta) nova.HistoricoIndisponivel = true;
                else precisamHistorico.Add((idSernit, situacao, "nova"));
                continue;
            }

            var situacaoAnterior = atual.Situacao;
            var agendadoAnterior = atual.AgendadoParaTexto;

            var retratoAntes = RetratoDoPaciente(atual);
            PreencherDaGrade(atual, linha, situacao);
            MarcarSeMudouOPaciente(atual, retratoAntes, agora);
            atual.SincronizadoEm = agora;
            atual.AtualizadoEm = agora;
            execucao.SolicitacoesAtualizadas++;

            if (situacaoAnterior != situacao)
            {
                atual.SituacaoAnterior = situacaoAnterior;
                atual.SituacaoMudouEm = agora;
                execucao.MudancasSituacao++;

                RegistrarGatilho(execucao, atual, TipoGatilhoSernit.MudancaSituacao,
                    $"{situacaoAnterior}>{situacao}@{agora:yyyyMMddHHmmss}",
                    situacaoAnterior, situacao, new { de = situacaoAnterior.ToString(), para = situacao.ToString() });

                if (situacao == SituacaoSernit.Alta)
                {
                    // O SERNIT ESCONDE o histórico quando a solicitação vai para Alta — reler
                    // falharia e não traria nada. Em vez de perder a trilha (como o SERNIT faz),
                    // PRESERVAMOS os eventos já capturados e ANEXAMOS a linha da mudança para Alta
                    // com os dados da grade. Não enfileira para leitura de histórico.
                    AnexarMudancaParaAlta(execucao, atual, situacaoAnterior, agora);
                }
                else
                {
                    precisamHistorico.Add((idSernit, situacao, "mudou_situacao"));
                }
            }
            else if (!string.Equals(
                         DataDoAgendamento(agendadoAnterior),
                         DataDoAgendamento(linha.AgendadoPara),
                         StringComparison.Ordinal)
                     && !string.IsNullOrWhiteSpace(linha.AgendadoPara))
            {
                RegistrarGatilho(execucao, atual, TipoGatilhoSernit.MudancaAgendamento,
                    $"{DataDoAgendamento(linha.AgendadoPara)}@{agora:yyyyMMddHHmmss}",
                    situacao, situacao,
                    new { de = agendadoAnterior, para = linha.AgendadoPara });

                precisamHistorico.Add((idSernit, situacao, "remarcou"));
            }
            else if (situacao == SituacaoSernit.EmFila && !atual.HistoricoIndisponivel)
            {
                precisamHistorico.Add((idSernit, situacao, "em_fila_diario"));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void PreencherDaGrade(SernitSolicitacao alvo, SernitLinhaGrade linha, SituacaoSernit situacao)
    {
        alvo.Tipo = SernitCodigos.DoTextoTipo(linha.Tipo) ?? alvo.Tipo;
        alvo.Recurso = linha.Recurso ?? alvo.Recurso;
        alvo.DataSolicitacao = ParseData(linha.DataSolicitacao) ?? alvo.DataSolicitacao;
        alvo.PacienteNome = linha.Paciente ?? alvo.PacienteNome;
        alvo.IdadeTexto = linha.Idade ?? alvo.IdadeTexto;
        alvo.Cpf = SoDigitos(linha.Cpf) ?? alvo.Cpf;
        alvo.Cns = SoDigitos(linha.Cns) ?? alvo.Cns;
        alvo.Cid = linha.Cid ?? alvo.Cid;
        alvo.SolicitanteNome = linha.Solicitante ?? alvo.SolicitanteNome;
        alvo.MunicipioSolicitante = linha.MunicipioSolicitante ?? alvo.MunicipioSolicitante;
        alvo.UnidadeExecutora = linha.UnidadeExecutora ?? alvo.UnidadeExecutora;
        alvo.AgendadoParaTexto = Truncar(linha.AgendadoPara, 300) ?? alvo.AgendadoParaTexto;
        alvo.Situacao = situacao;
    }

    // ------------------------------------------------------------------ histórico

    private async Task AplicarHistoricosAsync(
        SernitVarreduraExecucao execucao,
        ModoVarreduraSernit modo,
        List<(string IdSernit, SituacaoSernit Situacao, string Motivo)> pedidos,
        CancellationToken cancellationToken)
    {
        var lista = await MontarFilaDeHistoricoAsync(execucao, modo, pedidos, cancellationToken);

        var fila = lista
            .DistinctBy(p => p.IdSernit)
            .OrderBy(p => ChaveNumerica(p.IdSernit), StringComparer.Ordinal)
            .ToList();

        if (execucao.CursorIdSernit is { } cursor)
        {
            var antes = fila.Count;
            var chaveCursor = ChaveNumerica(cursor);
            fila = fila
                .Where(p => string.CompareOrdinal(ChaveNumerica(p.IdSernit), chaveCursor) > 0)
                .ToList();

            logger.LogInformation(
                "SERNIT: retomando a fase de histórico de {Execucao} depois de {Cursor} — "
                + "{Pulados} já lidos, {Faltam} pela frente.",
                execucao.Id, cursor, antes - fila.Count, fila.Count);
        }

        execucao.HistoricosPendentes = fila.Count;
        execucao.UltimoSinalEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (idSernit, situacao, motivo) in fila)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var solicitacao = await db.SernitSolicitacoes
                .FirstOrDefaultAsync(x => x.IdSernit == idSernit && x.ExcluidoEm == null, cancellationToken);
            if (solicitacao is null) continue;

            try
            {
                var historico = await leitor.LerHistoricoPorIdAsync(idSernit, situacao, cancellationToken);
                await AplicarHistoricoAsync(execucao, solicitacao, historico, cancellationToken);
                execucao.HistoricosLidos++;
            }
            catch (HistoricoSernitIndisponivelException ex)
            {
                solicitacao.HistoricoIndisponivel = true;
                execucao.HistoricosIndisponiveis++;
                db.SernitVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaSernit.HistoricoIndisponivel,
                    idSernit, ex.Message, situacao));
            }
            catch (EscritaNoSernitBloqueadaException ex)
            {
                logger.LogError(ex, "SERNIT: trava de leitura recusou a operação em {Id}.", idSernit);
                db.SernitVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaSernit.EscritaBloqueada,
                    idSernit, ex.Message, situacao));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SERNIT: falha ao ler histórico de {Id} ({Motivo}).", idSernit, motivo);
                db.SernitVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaSernit.ErroHistorico,
                    idSernit, ex.Message, situacao));
            }

            execucao.CursorIdSernit = idSernit;
            execucao.HistoricosPendentes = Math.Max(0, execucao.HistoricosPendentes - 1);
            execucao.UltimoSinalEm = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<List<(string IdSernit, SituacaoSernit Situacao, string Motivo)>> MontarFilaDeHistoricoAsync(
        SernitVarreduraExecucao execucao,
        ModoVarreduraSernit modo,
        List<(string IdSernit, SituacaoSernit Situacao, string Motivo)> pedidos,
        CancellationToken cancellationToken)
    {
        if (modo == ModoVarreduraSernit.CargaInicial)
        {
            var todas = await db.SernitSolicitacoes
                .Where(x => x.ExcluidoEm == null && !x.HistoricoIndisponivel)
                .Select(x => new { x.IdSernit, x.Situacao })
                .ToListAsync(cancellationToken);

            return todas.Select(x => (x.IdSernit, x.Situacao, "carga_inicial")).ToList();
        }

        if (pedidos.Count > 0) return pedidos;
        if (execucao.Retomadas == 0) return [];

        var candidatos = await db.SernitSolicitacoes
            .Where(x => x.ExcluidoEm == null
                        && !x.HistoricoIndisponivel
                        && (x.Situacao == SituacaoSernit.EmFila
                            || x.CriadoEm >= execucao.IniciadoEm
                            || (x.SituacaoMudouEm != null && x.SituacaoMudouEm >= execucao.IniciadoEm)))
            .Select(x => new { x.IdSernit, x.Situacao })
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "SERNIT: fila de histórico remontada do banco na retomada de {Execucao} — {Qtd} candidatas.",
            execucao.Id, candidatos.Count);

        return candidatos.Select(x => (x.IdSernit, x.Situacao, "retomada")).ToList();
    }

    /// <summary>IdSernit alinhado à direita para comparar como número via texto (ids curtos no SERNIT).</summary>
    private static string ChaveNumerica(string idSernit) =>
        idSernit.Length >= 12 ? idSernit : idSernit.PadLeft(12, '0');

    private async Task AplicarHistoricoAsync(
        SernitVarreduraExecucao execucao,
        SernitSolicitacao solicitacao,
        SernitHistorico historico,
        CancellationToken cancellationToken)
    {
        var retratoAntes = RetratoDoPaciente(solicitacao);
        PreencherDadosDoPaciente(solicitacao, historico.Paciente);
        MarcarSeMudouOPaciente(solicitacao, retratoAntes, DateTime.UtcNow);

        var jaTemos = await db.SernitEventos
            .Where(e => e.SernitSolicitacaoId == solicitacao.Id)
            .Select(e => new { e.DataEvento, e.Evento })
            .ToListAsync(cancellationToken);

        var conhecidos = jaTemos
            .Select(e => Chave(e.DataEvento, e.Evento))
            .ToHashSet(StringComparer.Ordinal);

        var agora = DateTime.UtcNow;
        DateTime? maisRecente = solicitacao.UltimoEventoEm;

        foreach (var lido in historico.Eventos)
        {
            var data = ParseDataHora(lido.Data);
            if (data is null || string.IsNullOrWhiteSpace(lido.Evento)) continue;

            if (maisRecente is null || data > maisRecente) maisRecente = data;

            if (!conhecidos.Add(Chave(data.Value, lido.Evento!))) continue;

            db.SernitEventos.Add(new SernitEvento
            {
                Id = Guid.NewGuid(),
                SernitSolicitacaoId = solicitacao.Id,
                DataEvento = data.Value,
                Evento = lido.Evento!,
                EstadoAnterior = lido.EstadoAnterior,
                EstadoAtual = lido.EstadoAtual,
                CentralRegulacao = lido.CentralRegulacao,
                UnidadeExecutora = lido.UnidadeExecutora,
                Usuario = lido.Usuario,
                LotacaoEvento = lido.LotacaoEvento,
                Ip = lido.Ip,
                Observacao = lido.Observacao,
                CapturadoEm = agora,
            });

            execucao.EventosNovos++;

            if (EhFollowUp(lido.Evento!))
            {
                execucao.FollowUpsNovos++;
                RegistrarGatilho(execucao, solicitacao, TipoGatilhoSernit.NovoFollowUp,
                    data.Value.ToString("O", CultureInfo.InvariantCulture),
                    solicitacao.SituacaoAnterior, solicitacao.Situacao,
                    new { data = lido.Data, usuario = lido.Usuario, observacao = lido.Observacao });
            }
        }

        solicitacao.HistoricoLidoEm = agora;
        solicitacao.EventosCount = conhecidos.Count;
        solicitacao.UltimoEventoEm = maisRecente;
    }

    /// <summary>
    /// Anexa a linha <b>"→ Alta"</b> ao histórico LOCAL e marca o histórico como indisponível no
    /// SERNIT — sem tentar reler (Alta esconde o histórico e a releitura falharia). Os eventos já
    /// capturados FICAM: ao contrário do SERNIT, não sumimos com a trilha do processo; só somamos
    /// a transição. Roda uma vez (a guarda <c>HistoricoIndisponivel</c> impede re-anexar).
    /// </summary>
    private void AnexarMudancaParaAlta(
        SernitVarreduraExecucao execucao, SernitSolicitacao sol, SituacaoSernit anterior, DateTime agora)
    {
        if (sol.HistoricoIndisponivel) return;

        db.SernitEventos.Add(new SernitEvento
        {
            Id = Guid.NewGuid(),
            SernitSolicitacaoId = sol.Id,
            DataEvento = agora,
            Evento = "Alta",
            EstadoAnterior = anterior.ToString(),
            EstadoAtual = SituacaoSernit.Alta.ToString(),
            UnidadeExecutora = string.IsNullOrWhiteSpace(sol.UnidadeExecutora) ? null : sol.UnidadeExecutora,
            Observacao = "Alta detectada pela varredura da grade. O SERNIT não oferece o histórico em "
                         + "Alta; os eventos capturados antes ficam preservados e esta linha registra "
                         + "a transição.",
            CapturadoEm = agora,
        });

        sol.EventosCount += 1;
        if (sol.UltimoEventoEm is null || agora > sol.UltimoEventoEm) sol.UltimoEventoEm = agora;
        sol.HistoricoIndisponivel = true; // não re-enfileira nem re-anexa; a tela explica o porquê
        sol.HistoricoLidoEm ??= agora;
        execucao.EventosNovos++;
    }

    private static bool EhFollowUp(string evento) =>
        evento.Replace("-", string.Empty).Replace(" ", string.Empty)
            .Contains("followup", StringComparison.OrdinalIgnoreCase);

    private static string Chave(DateTime data, string evento) =>
        $"{data:O}|{evento.Trim().ToLowerInvariant()}";

    private static string RetratoDoPaciente(SernitSolicitacao s) => string.Join('|', [
        s.PacienteNome, s.Cpf, s.Cns, s.NomeMae, s.Sexo, s.DataNascimento?.ToString("O"),
        s.Cep, s.Uf, s.MunicipioPaciente, s.Bairro, s.TipoLogradouro, s.Logradouro, s.Numero,
        s.Complemento, s.TelefoneResidencial, s.TelefoneWhatsapp, s.TelefoneContato,
    ]);

    private static void MarcarSeMudouOPaciente(SernitSolicitacao alvo, string antes, DateTime agora)
    {
        if (RetratoDoPaciente(alvo) != antes) alvo.PacienteConciliarEm = agora;
    }

    private static void PreencherDadosDoPaciente(
        SernitSolicitacao alvo, IReadOnlyDictionary<string, string> paciente)
    {
        string? V(string chave) => paciente.TryGetValue(chave, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        alvo.NomeMae = V("Nome Mãe") ?? alvo.NomeMae;
        alvo.Sexo = V("Sexo") ?? alvo.Sexo;
        alvo.DataNascimento = ParseData(V("Data Nascimento")) ?? alvo.DataNascimento;
        alvo.Etnia = V("Etnia") ?? alvo.Etnia;
        alvo.Cep = V("CEP") ?? alvo.Cep;
        alvo.Uf = V("UF") ?? alvo.Uf;
        alvo.MunicipioPaciente = V("Município") ?? alvo.MunicipioPaciente;
        alvo.Bairro = V("Bairro") ?? alvo.Bairro;
        alvo.TipoLogradouro = V("Tipo Logradouro") ?? alvo.TipoLogradouro;
        alvo.Logradouro = V("Logradouro") ?? alvo.Logradouro;
        alvo.Numero = V("Número") ?? alvo.Numero;
        alvo.Complemento = V("Complemento") ?? alvo.Complemento;
        alvo.TelefoneResidencial = V("Telefone Residencial") ?? alvo.TelefoneResidencial;
        // No SERNIT o campo de notificação é rotulado "Telefone SMS" (equivale ao WhatsApp do SER-RJ).
        alvo.TelefoneWhatsapp = V("Telefone SMS") ?? V("Telefone WhatsApp") ?? V("Telefone Celular") ?? alvo.TelefoneWhatsapp;
        alvo.TelefoneContato = V("Telefone Contato") ?? V("Telefone") ?? alvo.TelefoneContato;

        alvo.Cns ??= SoDigitos(V("CNS"));
        alvo.Cpf ??= SoDigitos(V("CPF"));
    }

    // ------------------------------------------------------------------ gatilhos

    private void RegistrarGatilho(
        SernitVarreduraExecucao execucao,
        SernitSolicitacao solicitacao,
        TipoGatilhoSernit tipo,
        string chaveEvento,
        SituacaoSernit? anterior,
        SituacaoSernit? atual,
        object? payload)
    {
        db.SernitGatilhos.Add(new SernitGatilho
        {
            Id = Guid.NewGuid(),
            SernitSolicitacaoId = solicitacao.Id,
            IdSernit = solicitacao.IdSernit,
            Tipo = tipo,
            ChaveEvento = Truncar(chaveEvento, 80),
            SituacaoAnterior = anterior,
            SituacaoAtual = atual,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            CriadoEm = DateTime.UtcNow,
        });
        execucao.GatilhosGerados++;
    }

    // ------------------------------------------------------------------ util

    private static SernitVarreduraFalha NovaFalha(
        SernitVarreduraExecucao execucao, TipoFalhaSernit tipo, string idSernit, string mensagem, SituacaoSernit situacao) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExecucaoId = execucao.Id,
            Tipo = tipo,
            Situacao = situacao,
            IdSernit = idSernit,
            Mensagem = Truncar(mensagem, 1000),
            CriadoEm = DateTime.UtcNow,
        };

    private static string? SoDigitos(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var digitos = new string(v.Where(char.IsDigit).ToArray());
        return digitos.Length == 0 ? null : digitos;
    }

    internal static string? DataDoAgendamento(string? texto)
    {
        var t = texto?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(t, @"\d{2}/\d{2}/\d{4}");
        return m.Success ? m.Value : t;
    }

    private static DateOnly? ParseData(string? texto) =>
        DateOnly.TryParseExact(texto?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;

    private static DateTime? ParseDataHora(string? texto)
    {
        var t = texto?.Trim();
        if (string.IsNullOrEmpty(t)) return null;

        string[] formatos = ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy"];
        return DateTime.TryParseExact(t, formatos, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt)
            ? FusoBrasilia.DeBrasiliaParaUtc(dt)
            : null;
    }

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(texto))]
    private static string? Truncar(string? texto, int max) =>
        texto is null || texto.Length <= max ? texto : texto[..max];
}
