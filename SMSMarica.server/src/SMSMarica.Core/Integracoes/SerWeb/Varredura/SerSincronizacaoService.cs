using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ser;

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
        CancellationToken cancellationToken);
}

public sealed class SerSincronizacaoService(
    SmsMaricaDbContext db,
    ISerLeitorService leitor,
    VarredorSer varredor,
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
        CancellationToken cancellationToken)
    {
        var alvo = situacoes is { Count: > 0 } ? situacoes : TodasSituacoes;

        var execucao = new SerVarreduraExecucao
        {
            Id = Guid.NewGuid(),
            Modo = modo,
            Disparo = disparo,
            Status = StatusVarreduraSer.EmExecucao,
            JanelaInicio = inicio,
            JanelaFim = fim,
            SituacoesVarridas = string.Join(',', alvo.Select(s => s.ToString())),
            IniciadoEm = DateTime.UtcNow,
            CriadoPor = usuarioId,
            CriadoPorNome = usuarioNome,
        };
        db.SerVarreduraExecucoes.Add(execucao);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await leitor.PrepararAsync(cancellationToken);

            // ---- fase 1: grade (todas as situações) ----
            var precisamHistorico = new List<(string IdSer, SituacaoSer Situacao, string Motivo)>();

            foreach (var situacao in alvo)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resultado = await varredor.VarrerAsync(situacao, inicio, fim, cancellationToken);
                execucao.Buscas += resultado.Buscas;
                execucao.Paginas += resultado.Paginas;
                execucao.SolicitacoesEncontradas += resultado.Total;

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
                            $"Mais de 100 registros em {fatia.Dia:dd/MM/yyyy} ({fatia.Situacao}"
                            + (fatia.Tipo is { } t ? $", {t}" : string.Empty)
                            + "). A tela do SER não pagina além disso — há registros NÃO lidos.",
                        CriadoEm = DateTime.UtcNow,
                    });
                }

                await AplicarGradeAsync(execucao, situacao, resultado, precisamHistorico, cancellationToken);
            }

            // ---- fase 2: histórico ----
            if (modo != ModoVarreduraSer.SomenteGrade)
            {
                await AplicarHistoricosAsync(execucao, modo, precisamHistorico, cancellationToken);
            }

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

    private async Task AplicarGradeAsync(
        SerVarreduraExecucao execucao,
        SituacaoSer situacao,
        ResultadoVarreduraSer resultado,
        List<(string, SituacaoSer, string)> precisamHistorico,
        CancellationToken cancellationToken)
    {
        var ids = resultado.Solicitacoes.Keys.ToList();
        var existentes = await db.SerSolicitacoes
            .Where(x => ids.Contains(x.IdSer) && x.ExcluidoEm == null)
            .ToDictionaryAsync(x => x.IdSer, cancellationToken);

        var agora = DateTime.UtcNow;

        foreach (var (idSer, linha) in resultado.Solicitacoes)
        {
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
            else if (!string.Equals(agendadoAnterior, linha.AgendadoPara, StringComparison.Ordinal)
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
        // Na carga inicial lemos o histórico de TUDO; nos demais modos, só de quem a fase 1
        // marcou (nova, mudou de situação, remarcou, ou está em fila).
        IReadOnlyList<(string IdSer, SituacaoSer Situacao, string Motivo)> lista = modo == ModoVarreduraSer.CargaInicial
            ? await db.SerSolicitacoes
                .Where(x => x.ExcluidoEm == null && !x.HistoricoIndisponivel)
                .Select(x => new { x.IdSer, x.Situacao })
                .ToListAsync(cancellationToken)
                .ContinueWith(t => (IReadOnlyList<(string, SituacaoSer, string)>)
                    t.Result.Select(x => (x.IdSer, x.Situacao, "carga_inicial")).ToList(), cancellationToken)
            : pedidos.DistinctBy(p => p.IdSer).ToList();

        foreach (var (idSer, situacao, motivo) in lista)
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

            await db.SaveChangesAsync(cancellationToken);
        }
    }

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
