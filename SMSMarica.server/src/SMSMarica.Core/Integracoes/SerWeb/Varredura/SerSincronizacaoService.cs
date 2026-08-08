using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ser;
using SMSMarica.Core.Integracoes.SerWeb.Varredura.Export;

namespace SMSMarica.Core.Integracoes.SerWeb.Varredura;

/// <summary>
/// Aplica no NOSSO banco o que o motor leu do SER: espelha a grade, faz o diff de situação,
/// relê o histórico de quem precisa e gera os gatilhos — ADR-0042.
///
/// <para><b>Regra de releitura de histórico</b> (a parte cara e a que não dá para evitar):
/// solicitação nova lê tudo; quem mudou de situação relê e faz diff; e <b>todo mundo em
/// <c>EmFila</c> relê todo dia</b>, porque FollowUP é <c>Em fila -&gt; Em fila</c> e não aparece
/// em nenhum diff de grade.</para>
/// </summary>
public interface ISerSincronizacaoService
{
    Task<Guid> ExecutarAsync(
        ModoVarreduraSer modo,
        DisparoSincronizacao disparo,
        DateOnly inicio,
        DateOnly fim,
        IReadOnlyList<SituacaoSer>? situacoes,
        Guid? usuarioId,
        string? usuarioNome,
        Guid? execucaoParaRetomar,
        CancellationToken cancellationToken);
}

public sealed class SerSincronizacaoService(
    SmsMaricaDbContext db,
    ISerLeitorService leitor,
    ISerExportLeitor exportLeitor,
    ISerExportSolicitacaoLeitor exportSolicitacaoLeitor,
    VarredorSerPorExport varredorExport,
    ILogger<SerSincronizacaoService> logger) : ISerSincronizacaoService
{
    /// <summary>Todas as situações do SER. A busca EXIGE o filtro, então varrer "tudo" é
    /// necessariamente uma passada por situação.</summary>
    private static readonly SituacaoSer[] TodasSituacoes =
    [
        SituacaoSer.EmFila,
        SituacaoSer.Pendente,
        SituacaoSer.Agendada,
        SituacaoSer.ChegadaNaoConfirmada,
        SituacaoSer.ChegadaConfirmada,
        SituacaoSer.Cancelada,
        SituacaoSer.Alta,
    ];

    public async Task<Guid> ExecutarAsync(
        ModoVarreduraSer modo,
        DisparoSincronizacao disparo,
        DateOnly inicio,
        DateOnly fim,
        IReadOnlyList<SituacaoSer>? situacoes,
        Guid? usuarioId,
        string? usuarioNome,
        Guid? execucaoParaRetomar,
        CancellationToken cancellationToken)
    {
        var alvo = situacoes is { Count: > 0 } ? situacoes : TodasSituacoes;

        // RETOMADA: continua a execução existente a partir do ponteiro, em vez de abrir outra.
        // Abrir uma nova a cada restart perderia o progresso e encheria o histórico de rodadas
        // fantasma.
        var execucao = execucaoParaRetomar is { } idRetomar
            ? await db.SerVarreduraExecucoes.FirstOrDefaultAsync(x => x.Id == idRetomar, cancellationToken)
            : null;

        if (execucao is not null)
        {
            execucao.Status = StatusVarreduraSer.EmExecucao;
            execucao.Retomadas++;
            execucao.RetomadaEm = DateTime.UtcNow;
            logger.LogInformation(
                "SER: retomando execução {Execucao} na fase {Fase} (retomada nº {N}); cursor: "
                + "situação={Situacao} data={Data} idSer={IdSer}.",
                execucao.Id, execucao.Fase, execucao.Retomadas,
                execucao.CursorSituacao, execucao.CursorData, execucao.CursorIdSer);
        }
        else
        {
            execucao = new SerVarreduraExecucao
            {
                Id = Guid.NewGuid(),
                Modo = modo,
                Disparo = disparo,
                Status = StatusVarreduraSer.EmExecucao,
                Fase = FaseVarreduraSer.Grade,
                JanelaInicio = inicio,
                JanelaFim = fim,
                SituacoesVarridas = string.Join(',', alvo.Select(s => s.ToString())),
                IniciadoEm = DateTime.UtcNow,
                CriadoPor = usuarioId,
                CriadoPorNome = usuarioNome,
            };
            db.SerVarreduraExecucoes.Add(execucao);
        }
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            // ---- fase 1: grade (todas as situações) ----
            var precisamHistorico = new List<(string IdSer, SituacaoSer Situacao, string Motivo)>();

            // Retomada: a fase de histórico já começou, então a grade inteira está feita.
            if (execucao.Fase != FaseVarreduraSer.Historico)
            {
                execucao.Fase = FaseVarreduraSer.Grade;
                await VarrerGradeAsync(
                    execucao, alvo, inicio, fim, precisamHistorico, cancellationToken);
            }
            else
            {
                logger.LogInformation(
                    "SER: retomando {Execucao} direto na fase de histórico — a grade já foi varrida.",
                    execucao.Id);
            }

            // ---- fase 2: histórico ----
            if (modo != ModoVarreduraSer.SomenteGrade)
            {
                execucao.Fase = FaseVarreduraSer.Historico;
                await db.SaveChangesAsync(cancellationToken);
                await AplicarHistoricosAsync(execucao, modo, precisamHistorico, cancellationToken);
            }

            execucao.Fase = FaseVarreduraSer.Finalizada;
            execucao.Status = execucao.FatiasTruncadas > 0
                ? StatusVarreduraSer.Parcial
                : StatusVarreduraSer.Concluida;
        }
        catch (OperationCanceledException)
        {
            execucao.Status = StatusVarreduraSer.Cancelada;
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SER: varredura {Execucao} falhou.", execucao.Id);
            execucao.Status = StatusVarreduraSer.Erro;
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

    /// <summary>
    /// Fase 1: espelha a grade, situação por situação.
    ///
    /// <para><b>Cada lote é gravado com o cursor na mesma passada.</b> Acumular tudo em memória e
    /// gravar no fim faria uma queda no meio jogar horas fora; e avançar o cursor sem ter gravado o
    /// lote seria pior ainda — a retomada pularia o trecho e deixaria um buraco que ninguém veria.</para>
    ///
    /// <para><b>ALTA vai pela tela de Solicitação</b> (paginada, teto de 100): o combo da tela de
    /// Histórico, que é a que exporta, não oferece essa situação.</para>
    /// </summary>
    private async Task VarrerGradeAsync(
        SerVarreduraExecucao execucao,
        IReadOnlyList<SituacaoSer> alvo,
        DateOnly inicio,
        DateOnly fim,
        List<(string, SituacaoSer, string)> precisamHistorico,
        CancellationToken cancellationToken)
    {
        // Retomada: as situações antes do cursor já foram varridas por inteiro; a do cursor
        // recomeça na primeira data ainda não varrida.
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

            async Task AplicarAsync(
                IReadOnlyList<SerLinhaGrade> linhas, DateOnly cursorConcluido, CancellationToken ct)
            {
                if (linhas.Count > 0)
                {
                    await AplicarGradeAsync(execucao, situacao, linhas, vistos, precisamHistorico, ct);
                }

                execucao.CursorSituacao = situacao;
                execucao.CursorData = cursorConcluido;
                await db.SaveChangesAsync(ct);
            }

            await AplicarAsync([], de, cancellationToken);

            // ALTA só existe no combo da tela de Solicitação (teto de 100, sem aviso de corte); as
            // outras seis vêm da de Histórico, que devolve 500 e avisa por escrito. As duas são
            // lidas por ARQUIVO: a leitura paginada foi aposentada em 08/08/2026 depois de perder
            // 853 registros de ALTA declarando cobertura completa.
            var leitorDaVez = situacao == SituacaoSer.Alta ? exportSolicitacaoLeitor : exportLeitor;

            await leitorDaVez.PrepararAsync(cancellationToken);
            var resultado = await varredorExport.VarrerAsync(
                leitorDaVez, situacao, de, fim, AplicarAsync, cancellationToken);
            RegistrarTruncadas(execucao, resultado, leitorDaVez);

            execucao.Buscas += resultado.Buscas;
            execucao.SolicitacoesEncontradas += vistos.Count;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Fatia que estourou o teto = registros NÃO lidos. Fica declarada, e a rodada vira
    /// Parcial em vez de Concluída.</summary>
    private void RegistrarTruncadas(
        SerVarreduraExecucao execucao, ResultadoVarreduraSer resultado, ISerExportLeitor leitor)
    {
        var teto = leitor.TetoPorLote;
        var tela = leitor.Tela;

        foreach (var fatia in resultado.Truncadas)
        {
            execucao.FatiasTruncadas++;
            db.SerVarreduraFalhas.Add(new SerVarreduraFalha
            {
                Id = Guid.NewGuid(),
                ExecucaoId = execucao.Id,
                Tipo = TipoFalhaSer.FatiaTruncada,
                Situacao = fatia.Situacao,
                FatiaInicio = fatia.Dia,
                FatiaFim = fatia.Dia,
                TipoRecurso = fatia.Tipo,
                Mensagem =
                    $"Mais de {teto} registros em {fatia.Dia:dd/MM/yyyy} ({fatia.Situacao}"
                    + (fatia.Tipo is { } t ? $", {t}" : string.Empty)
                    + $"). A tela {tela} do SER não devolve além disso — há registros NÃO lidos.",
                CriadoEm = DateTime.UtcNow,
            });
        }
    }

    private async Task AplicarGradeAsync(
        SerVarreduraExecucao execucao,
        SituacaoSer situacao,
        IReadOnlyList<SerLinhaGrade> linhas,
        HashSet<string> vistos,
        List<(string, SituacaoSer, string)> precisamHistorico,
        CancellationToken cancellationToken)
    {
        // Dedup dentro do lote: o SER pode repetir a mesma solicitação entre fatias vizinhas.
        var doLote = new Dictionary<string, SerLinhaGrade>(StringComparer.Ordinal);
        foreach (var l in linhas)
        {
            if (!string.IsNullOrWhiteSpace(l.IdSer)) doLote[l.IdSer] = l;
        }
        if (doLote.Count == 0) return;

        var ids = doLote.Keys.ToList();
        var existentes = await db.SerSolicitacoes
            .Where(x => ids.Contains(x.IdSer) && x.ExcluidoEm == null)
            .ToDictionaryAsync(x => x.IdSer, cancellationToken);

        var agora = DateTime.UtcNow;

        foreach (var (idSer, linha) in doLote)
        {
            vistos.Add(idSer);

            if (!existentes.TryGetValue(idSer, out var atual))
            {
                var nova = new SerSolicitacao
                {
                    Id = Guid.NewGuid(),
                    IdSer = idSer,
                    CriadoEm = agora,
                    SincronizadoEm = agora,
                    Situacao = situacao,
                };
                PreencherDaGrade(nova, linha, situacao);
                db.SerSolicitacoes.Add(nova);

                execucao.SolicitacoesNovas++;
                RegistrarGatilho(execucao, nova, TipoGatilhoSer.NovaSolicitacao, situacao.ToString(),
                    null, situacao, new { linha.Recurso, linha.Paciente, linha.DataSolicitacao });

                precisamHistorico.Add((idSer, situacao, "nova"));
                continue;
            }

            var situacaoAnterior = atual.Situacao;
            var agendadoAnterior = atual.AgendadoParaTexto;

            PreencherDaGrade(atual, linha, situacao);
            atual.SincronizadoEm = agora;
            atual.AtualizadoEm = agora;
            execucao.SolicitacoesAtualizadas++;

            if (situacaoAnterior != situacao)
            {
                atual.SituacaoAnterior = situacaoAnterior;
                atual.SituacaoMudouEm = agora;
                execucao.MudancasSituacao++;

                RegistrarGatilho(execucao, atual, TipoGatilhoSer.MudancaSituacao, situacao.ToString(),
                    situacaoAnterior, situacao, new { de = situacaoAnterior.ToString(), para = situacao.ToString() });

                // Mudou de situação → relê o histórico e faz diff. É esta releitura que serve de
                // REDE DE SEGURANÇA para o FollowUP registrado enquanto a solicitação estava
                // cancelada: ao voltar para EmFila, a trilha inteira é relida e deduplicada.
                precisamHistorico.Add((idSer, situacao, "mudou_situacao"));
            }
            // Compara pela DATA, não pelo texto cru: as duas telas escrevem o mesmo agendamento de
            // formas diferentes ("28/01/2020 13:15 - HOSPITAL X" na de Solicitação, "28/01/2020"
            // na de Histórico). Comparando texto, toda linha lida por uma tela e relida pela outra
            // virava "remarcação" — 7.388 gatilhos falsos numa rodada só em 07/08/2026, cada um
            // ainda enfileirando uma releitura de histórico de ~0,6 s contra o SER. Quem decide se
            // houve remarcação é a data.
            else if (!string.Equals(
                         DataDoAgendamento(agendadoAnterior),
                         DataDoAgendamento(linha.AgendadoPara),
                         StringComparison.Ordinal)
                     && !string.IsNullOrWhiteSpace(linha.AgendadoPara))
            {
                RegistrarGatilho(execucao, atual, TipoGatilhoSer.MudancaAgendamento,
                    linha.AgendadoPara!, situacao, situacao,
                    new { de = agendadoAnterior, para = linha.AgendadoPara });

                precisamHistorico.Add((idSer, situacao, "remarcou"));
            }
            else if (situacao == SituacaoSer.EmFila && !atual.HistoricoIndisponivel)
            {
                // FollowUP é `Em fila -> Em fila`: não muda situação nem grade. A ÚNICA forma de
                // enxergá-lo é reler a trilha todo dia de quem está em fila.
                precisamHistorico.Add((idSer, situacao, "em_fila_diario"));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void PreencherDaGrade(SerSolicitacao alvo, SerLinhaGrade linha, SituacaoSer situacao)
    {
        alvo.Tipo = SerCodigos.DoTextoTipo(linha.Tipo) ?? alvo.Tipo;
        alvo.Recurso = linha.Recurso ?? alvo.Recurso;
        alvo.DataSolicitacao = ParseData(linha.DataSolicitacao) ?? alvo.DataSolicitacao;
        alvo.PacienteNome = linha.Paciente ?? alvo.PacienteNome;
        alvo.IdadeTexto = linha.Idade ?? alvo.IdadeTexto;
        alvo.Cpf = SoDigitos(linha.Cpf) ?? alvo.Cpf;
        alvo.Cns = SoDigitos(linha.Cns) ?? alvo.Cns;
        alvo.Cid = linha.Cid ?? alvo.Cid;
        alvo.SolicitanteNome = linha.Solicitante ?? alvo.SolicitanteNome;
        alvo.MunicipioSolicitante = linha.MunicipioSolicitante ?? alvo.MunicipioSolicitante;
        // Só a tela de Histórico traz executora; `??` para a varredura de ALTA (tela de
        // Solicitação, sem essa coluna) não apagar o que o export já tinha descoberto.
        alvo.UnidadeExecutora = linha.UnidadeExecutora ?? alvo.UnidadeExecutora;
        alvo.AgendadoParaTexto = linha.AgendadoPara;
        alvo.Situacao = situacao;
    }

    // ------------------------------------------------------------------ histórico

    private async Task AplicarHistoricosAsync(
        SerVarreduraExecucao execucao,
        ModoVarreduraSer modo,
        List<(string IdSer, SituacaoSer Situacao, string Motivo)> pedidos,
        CancellationToken cancellationToken)
    {
        var lista = await MontarFilaDeHistoricoAsync(execucao, modo, pedidos, cancellationToken);

        // Ordem NUMÉRICA e estável: o cursor de retomada é um IdSer, e "já passei por este" só faz
        // sentido se a fila sair na mesma ordem toda vez. Ordenar como texto colocaria 8.147.763
        // antes de 873.917 (7 dígitos contra 6) e a retomada pularia meia base.
        var fila = lista
            .DistinctBy(p => p.IdSer)
            .OrderBy(p => ChaveNumerica(p.IdSer), StringComparer.Ordinal)
            .ToList();

        if (execucao.CursorIdSer is { } cursor)
        {
            var antes = fila.Count;
            var chaveCursor = ChaveNumerica(cursor);
            fila = fila
                .Where(p => string.CompareOrdinal(ChaveNumerica(p.IdSer), chaveCursor) > 0)
                .ToList();

            logger.LogInformation(
                "SER: retomando a fase de histórico de {Execucao} depois de {Cursor} — "
                + "{Pulados} já lidos, {Faltam} pela frente.",
                execucao.Id, cursor, antes - fila.Count, fila.Count);
        }

        execucao.HistoricosPendentes = fila.Count;
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (idSer, situacao, motivo) in fila)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var solicitacao = await db.SerSolicitacoes
                .FirstOrDefaultAsync(x => x.IdSer == idSer && x.ExcluidoEm == null, cancellationToken);
            if (solicitacao is null) continue;

            try
            {
                var historico = await leitor.LerHistoricoPorIdAsync(idSer, situacao, cancellationToken);
                await AplicarHistoricoAsync(execucao, solicitacao, historico, cancellationToken);
                execucao.HistoricosLidos++;
            }
            catch (HistoricoSerIndisponivelException ex)
            {
                // Situação Alta não oferece histórico. Marcamos para não tentar de novo todo dia.
                solicitacao.HistoricoIndisponivel = true;
                execucao.HistoricosIndisponiveis++;
                db.SerVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaSer.HistoricoIndisponivel,
                    idSer, ex.Message, situacao));
            }
            catch (EscritaNoSerBloqueadaException ex)
            {
                // A trava recusou: é BUG DO MOTOR, não do SER. Precisa aparecer para quem opera.
                logger.LogError(ex, "SER: trava de leitura recusou a operação em {IdSer}.", idSer);
                db.SerVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaSer.EscritaBloqueada,
                    idSer, ex.Message, situacao));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SER: falha ao ler histórico de {IdSer} ({Motivo}).", idSer, motivo);
                db.SerVarreduraFalhas.Add(NovaFalha(execucao, TipoFalhaSer.ErroHistorico,
                    idSer, ex.Message, situacao));
            }

            // O cursor avança mesmo quando a leitura falhou: a falha ficou registrada em
            // `ser_varredura_falha` e retentar em loop travaria a rodada inteira num único registro.
            execucao.CursorIdSer = idSer;
            execucao.HistoricosPendentes = Math.Max(0, execucao.HistoricosPendentes - 1);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Quem tem o histórico relido nesta rodada.
    ///
    /// <para><b>Na retomada a lista é remontada do banco, não da memória.</b> A fase 1 marca os
    /// candidatos numa lista em memória; se o serviço cai durante a fase 2, essa lista se perde, e
    /// retomar com ela vazia faria a rodada terminar "concluída" sem ter lido histórico nenhum. A
    /// remontagem é de propósito um <b>superconjunto</b> (todo mundo em fila + tudo que nasceu ou
    /// mudou de situação depois do início da rodada): reler histórico é idempotente — os eventos
    /// deduplicam por (data, evento) —, então sobrar custa tempo, mas faltar deixa buraco.</para>
    /// </summary>
    private async Task<List<(string IdSer, SituacaoSer Situacao, string Motivo)>> MontarFilaDeHistoricoAsync(
        SerVarreduraExecucao execucao,
        ModoVarreduraSer modo,
        List<(string IdSer, SituacaoSer Situacao, string Motivo)> pedidos,
        CancellationToken cancellationToken)
    {
        // Na carga inicial lemos o histórico de TUDO.
        if (modo == ModoVarreduraSer.CargaInicial)
        {
            var todas = await db.SerSolicitacoes
                .Where(x => x.ExcluidoEm == null && !x.HistoricoIndisponivel)
                .Select(x => new { x.IdSer, x.Situacao })
                .ToListAsync(cancellationToken);

            return todas.Select(x => (x.IdSer, x.Situacao, "carga_inicial")).ToList();
        }

        if (pedidos.Count > 0) return pedidos;

        // Sem pedidos em memória numa execução que já foi retomada: remonta do banco.
        if (execucao.Retomadas == 0) return [];

        var candidatos = await db.SerSolicitacoes
            .Where(x => x.ExcluidoEm == null
                        && !x.HistoricoIndisponivel
                        && (x.Situacao == SituacaoSer.EmFila
                            || x.CriadoEm >= execucao.IniciadoEm
                            || (x.SituacaoMudouEm != null && x.SituacaoMudouEm >= execucao.IniciadoEm)))
            .Select(x => new { x.IdSer, x.Situacao })
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "SER: fila de histórico remontada do banco na retomada de {Execucao} — {Qtd} candidatas.",
            execucao.Id, candidatos.Count);

        return candidatos.Select(x => (x.IdSer, x.Situacao, "retomada")).ToList();
    }

    /// <summary>IdSer alinhado à direita para comparar como número usando comparação de texto.</summary>
    private static string ChaveNumerica(string idSer) =>
        idSer.Length >= 12 ? idSer : idSer.PadLeft(12, '0');

    private async Task AplicarHistoricoAsync(
        SerVarreduraExecucao execucao,
        SerSolicitacao solicitacao,
        SerHistorico historico,
        CancellationToken cancellationToken)
    {
        PreencherDadosDoPaciente(solicitacao, historico.Paciente);

        var jaTemos = await db.SerEventos
            .Where(e => e.SerSolicitacaoId == solicitacao.Id)
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

            // Único por (solicitação, data, evento) — reler a trilha inteira todo dia só insere
            // o que é novo.
            if (!conhecidos.Add(Chave(data.Value, lido.Evento!))) continue;

            db.SerEventos.Add(new SerEvento
            {
                Id = Guid.NewGuid(),
                SerSolicitacaoId = solicitacao.Id,
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
                RegistrarGatilho(execucao, solicitacao, TipoGatilhoSer.NovoFollowUp,
                    data.Value.ToString("O", CultureInfo.InvariantCulture),
                    solicitacao.SituacaoAnterior, solicitacao.Situacao,
                    new { data = lido.Data, usuario = lido.Usuario, observacao = lido.Observacao });
            }
        }

        solicitacao.HistoricoLidoEm = agora;
        solicitacao.EventosCount = conhecidos.Count;
        solicitacao.UltimoEventoEm = maisRecente;
    }

    /// <summary>O SER escreve "FollowUP"; toleramos variações de caixa e o hífen.</summary>
    private static bool EhFollowUp(string evento) =>
        evento.Replace("-", string.Empty).Replace(" ", string.Empty)
            .Contains("followup", StringComparison.OrdinalIgnoreCase);

    private static string Chave(DateTime data, string evento) =>
        $"{data:O}|{evento.Trim().ToLowerInvariant()}";

    private static void PreencherDadosDoPaciente(
        SerSolicitacao alvo, IReadOnlyDictionary<string, string> paciente)
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
        alvo.TelefoneWhatsapp = V("Telefone WhatsApp") ?? alvo.TelefoneWhatsapp;
        alvo.TelefoneContato = V("Telefone Contato") ?? alvo.TelefoneContato;

        // CNS/CPF da tela de histórico completam o que a grade não trouxe.
        alvo.Cns ??= SoDigitos(V("CNS"));
        alvo.Cpf ??= SoDigitos(V("CPF"));
    }

    // ------------------------------------------------------------------ gatilhos

    private void RegistrarGatilho(
        SerVarreduraExecucao execucao,
        SerSolicitacao solicitacao,
        TipoGatilhoSer tipo,
        string chaveEvento,
        SituacaoSer? anterior,
        SituacaoSer? atual,
        object? payload)
    {
        db.SerGatilhos.Add(new SerGatilho
        {
            Id = Guid.NewGuid(),
            SerSolicitacaoId = solicitacao.Id,
            IdSer = solicitacao.IdSer,
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

    private static SerVarreduraFalha NovaFalha(
        SerVarreduraExecucao execucao, TipoFalhaSer tipo, string idSer, string mensagem, SituacaoSer situacao) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExecucaoId = execucao.Id,
            Tipo = tipo,
            Situacao = situacao,
            IdSer = idSer,
            Mensagem = Truncar(mensagem, 1000),
            CriadoEm = DateTime.UtcNow,
        };

    private static string? SoDigitos(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var digitos = new string(v.Where(char.IsDigit).ToArray());
        return digitos.Length == 0 ? null : digitos;
    }

    /// <summary>
    /// A data <c>dd/MM/yyyy</c> de dentro do texto de agendamento, ou <c>null</c>.
    ///
    /// <para>Serve só para COMPARAR duas leituras do mesmo agendamento — o que é gravado continua
    /// sendo o texto que a tela devolveu, com hora e unidade quando ela os tiver.</para>
    /// </summary>
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
        // O SER grava wall-clock de Brasília; a coluna é `timestamp with time zone`. Sem
        // converter, o Npgsql recusa o Kind=Unspecified no SaveChanges.
        return DateTime.TryParseExact(t, formatos, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt)
            ? FusoBrasilia.DeBrasiliaParaUtc(dt)
            : null;
    }

    private static string Truncar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}
