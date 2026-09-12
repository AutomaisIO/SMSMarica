using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.SisregWeb.Escalas.Background;
using SMSMais.Core.Integracoes.SisregWeb.Escalas.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Escalas;

public interface IEscalasSincronizacaoService
{
    /// <summary>Dispara agora (botão). 202 — roda no servidor.</summary>
    Task<EscalasSincronizacaoAceitaDto> IniciarAsync(CancellationToken cancellationToken = default);

    /// <summary>Disparo do scheduler. <c>false</c> = colisão ou orçamento — não é erro.</summary>
    Task<bool> DispararAgendadoAsync(CancellationToken cancellationToken = default);

    /// <summary>Executa o job (chamado pelo runner, fora da request).</summary>
    Task ExecutarAsync(EscalasSincronizacaoJob job, CancellationToken cancellationToken = default);

    EscalasSincronizacaoStatusDto? ObterStatus();
    bool Cancelar();

    Task<IReadOnlyList<EscalasSincronizacaoExecucaoDto>> ListarExecucoesAsync(
        int limite = 10, CancellationToken cancellationToken = default);

    Task<EscalasAgendamentoDto> ObterAgendamentoAsync(CancellationToken cancellationToken = default);

    Task<EscalasAgendamentoDto> SalvarAgendamentoAsync(
        SalvarEscalasAgendamentoRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sincroniza a grade de ESCALAS do SISREG — a oferta de vagas da rede inteira.
///
/// <para><b>Uma requisição cobre tudo.</b> A tela <c>cons_escalas</c> aceita recorte sem critério
/// (unidade, profissional, procedimento e datas vazios), ao contrário do <c>cons_agendas</c>, que
/// exige os filtros no servidor. Medido em 04/09/2026: um POST devolveu 17.469 linhas. Por isso
/// este motor não tem rodízio, TTL nem fatiamento — o custo é de uma requisição por dia.</para>
///
/// <para><b>Upsert por código, nunca append.</b> O <c>COD. ESCALA AMBULATORIAL</c> é único no
/// arquivo e o SISREG edita a linha in place (59% têm alteração ≠ inserção). Versionar aqui criaria
/// oferta duplicada e dobraria a contagem de vagas de quem teve o horário corrigido.</para>
///
/// <para><b>Traz o arquivo INTEIRO, inclusive o passado</b> (15.529 das 17.469 linhas já venceram).
/// Não é generosidade: como o custo é de uma requisição independente do filtro, restringir a
/// <c>status=A</c> não economizaria nada e criaria uma ambiguidade cara — uma escala que passasse de
/// ATIVA para EXPIRADA sumiria do recorte e seria marcada <see cref="SisregEscala.Ausente"/>, ou
/// seja, o sistema diria "essa vaga desapareceu do SISREG" quando ela apenas venceu. Trazendo tudo,
/// <c>Ausente</c> volta a significar só uma coisa: a linha saiu do SISREG de verdade.</para>
///
/// <para>De quebra, o histórico entra de graça e sustenta a análise de ocupação do passado — que
/// sem escala vencida não teria denominador.</para>
/// </summary>
public sealed class EscalasSincronizacaoService(
    SmsMaisDbContext db,
    ISisregWebSessao sessao,
    IEscalasSincronizacaoFila fila,
    EscalasSincronizacaoEstadoVivo estadoVivo,
    Varredura.Background.VarreduraSisregEstadoVivo varreduraEstadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    MapeamentoLote.Background.MapeamentoLoteEstadoVivo loteEstadoVivo,
    SisregOrcamentoRequisicoes orcamento,
    IIntegracaoCredencialService credenciais,
    IUsuarioAtualAccessor usuarioAtual,
    IOptions<EscalasSincronizacaoOpcoes> opcoes,
    IOptions<SisregOrcamentoOpcoes> orcamentoOpcoes,
    Regulacao.Catalogo.IRegulacaoCatalogoService catalogoRegulacao,
    ILogger<EscalasSincronizacaoService> logger) : IEscalasSincronizacaoService
{
    private const string Caminho = "/cgi-bin/cons_escalas";
    private const string Ibge = "330270";

    /// <summary>Chaves no <c>ParametrosJson</c> da credencial <c>sisreg</c> — o mesmo lugar onde o
    /// lote de mapeamento guarda o dele. Prefixadas para não colidirem.</summary>
    public const string ChaveAtivo = "escalasAtivo";
    public const string ChaveHora = "escalasHoraLocal";

    /// <summary>
    /// Lista de horários do disparo diário. Substitui <see cref="ChaveHora"/>, que fica como
    /// leitura de compatibilidade — quem já tinha um horário só continua com ele até salvar de novo.
    ///
    /// <para><b>Por que mais de um por dia.</b> A escala é a OFERTA: vaga e agenda nova nascem no
    /// SISREG a qualquer hora do dia, e é justamente isso que a regulação precisa saber cedo — uma
    /// agenda de cardiologia que abre às 09:00 e só é vista às 02:30 do dia seguinte custa 17 horas
    /// de fila parada. Sincronizar de novo é barato: <b>1 requisição para a rede inteira</b> (o
    /// <c>cons_escalas</c> sem critério traz tudo), contra as ~223 de uma passada de agenda.</para>
    ///
    /// <para><b>Não há trava de horário aqui</b> — a trava 07:30–15:00 é do <c>expo_solicitacoes</c>,
    /// não do <c>cons_escalas</c>. Por isso 06/12/18 funciona para escalas e <b>não</b> funcionaria
    /// para a varredura de agenda, cujo horário do meio cairia no bloqueio.</para>
    /// </summary>
    public const string ChaveHorarios = "escalasHorariosLocais";

    /// <summary>
    /// Teto de disparos por dia. Cada um custa 1 requisição, então o limite não é orçamento: é não
    /// transformar o motor em enxurrada de execuções que ninguém lê, e deixar espaço entre eles para
    /// os outros motores (que se recusam mutuamente quando um está vivo).
    /// </summary>
    public const int MaxHorariosPorDia = 6;

    private readonly EscalasSincronizacaoOpcoes _opcoes = opcoes.Value;

    // ------------------------------------------------------------------ disparo

    public async Task<EscalasSincronizacaoAceitaDto> IniciarAsync(CancellationToken cancellationToken = default)
    {
        var usuarioId = usuarioAtual.UsuarioId;
        var id = await IniciarNucleoAsync(
            DisparoSincronizacao.Manual, usuarioId,
            usuarioId is null ? null : await NomeDoUsuarioAsync(usuarioId.Value, cancellationToken),
            lancar: true, cancellationToken);

        return new EscalasSincronizacaoAceitaDto(
            id!.Value,
            "Sincronização de escalas iniciada. Roda no servidor — pode fechar a tela.");
    }

    public async Task<bool> DispararAgendadoAsync(CancellationToken cancellationToken = default) =>
        await IniciarNucleoAsync(
            DisparoSincronizacao.Agendado, usuarioId: null, usuarioNome: null,
            lancar: false, cancellationToken) is not null;

    /// <summary>
    /// Núcleo comum. <paramref name="lancar"/> decide entre exceção (humano clicou, merece saber
    /// por quê) e <c>null</c> silencioso (robô — colisão é rotina, não incidente).
    /// </summary>
    private async Task<Guid?> IniciarNucleoAsync(
        DisparoSincronizacao disparo, Guid? usuarioId, string? usuarioNome,
        bool lancar, CancellationToken ct)
    {
        // Todos os motores falam com o SISREG pela mesma sessão de operador: se um está vivo,
        // entrar agora derruba a sessão dele no meio do caminho.
        if (estadoVivo.EmExecucao
            || varreduraEstadoVivo.ObterAtual() is not null
            || importacaoEstadoVivo.ObterAtual() is not null
            || loteEstadoVivo.EmExecucao)
        {
            if (lancar)
            {
                throw new ConflitoException(
                    "sisreg.trabalho_em_andamento",
                    "Já há um trabalho do SISREG em andamento. Espere terminar — todos usam a mesma "
                    + "sessão de operador.");
            }
            return null;
        }

        if (orcamento.Restante(orcamentoOpcoes.Value.TetoAutomatico) < _opcoes.OrcamentoMinimo)
        {
            if (lancar)
            {
                throw new ConflitoException(
                    "sisreg.orcamento_esgotado",
                    $"Orçamento de requisições do SISREG esgotado nesta hora. "
                    + $"Libera em {orcamento.EsperaAteLiberar():mm\\:ss}.");
            }
            return null;
        }

        await FecharOrfasAsync(ct);

        var execucao = new SisregEscalaSincronizacaoExecucao
        {
            Id = Guid.CreateVersion7(),
            Disparo = disparo,
            Status = StatusVarredura.Pendente,
            IniciadoEm = DateTime.UtcNow,
            CriadoPor = usuarioId,
            CriadoPorNome = usuarioNome,
        };
        db.SisregEscalaSincronizacaoExecucoes.Add(execucao);
        await db.SaveChangesAsync(ct);

        if (!fila.TentarEnfileirar(new EscalasSincronizacaoJob(disparo, usuarioId, usuarioNome)))
        {
            // A execução já existe no banco: fechar aqui evita deixá-la eternamente "Pendente".
            execucao.Status = StatusVarredura.Erro;
            execucao.MensagemErro = "Fila cheia — já havia uma sincronização aguardando.";
            execucao.FinalizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            if (lancar)
            {
                throw new ConflitoException(
                    "escalas.fila_cheia", "Já há uma sincronização de escalas aguardando.");
            }
            return null;
        }

        return execucao.Id;
    }

    /// <summary>Execuções de um processo que morreu ficariam "rodando" para sempre e bloqueariam
    /// o próximo disparo. Fecha na entrada, não na saída.</summary>
    private async Task FecharOrfasAsync(CancellationToken ct)
    {
        var orfas = await db.SisregEscalaSincronizacaoExecucoes
            .Where(e => e.Status == StatusVarredura.Pendente || e.Status == StatusVarredura.EmExecucao)
            .ToListAsync(ct);

        if (orfas.Count == 0) return;

        foreach (var orfa in orfas)
        {
            orfa.Status = StatusVarredura.Erro;
            orfa.MensagemErro = "Interrompida — o servidor reiniciou durante a execução.";
            orfa.FinalizadoEm = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ execução

    public async Task ExecutarAsync(EscalasSincronizacaoJob job, CancellationToken cancellationToken = default)
    {
        var execucao = await db.SisregEscalaSincronizacaoExecucoes
            .Where(e => e.Status == StatusVarredura.Pendente)
            .OrderByDescending(e => e.IniciadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        if (execucao is null)
        {
            logger.LogWarning("SISREG_ESCALAS: job sem execução pendente correspondente — ignorado.");
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = cts.Token;

        var progresso = new ProgressoEscalas
        {
            Disparo = job.Disparo,
            IniciadoEm = execucao.IniciadoEm,
            ExecucaoId = execucao.Id,
        };
        estadoVivo.Iniciar(progresso, cts);

        execucao.Status = StatusVarredura.EmExecucao;
        await db.SaveChangesAsync(ct);

        var status = StatusVarredura.Concluida;
        string? erro = null;

        try
        {
            await SincronizarAsync(execucao, progresso, ct);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            status = StatusVarredura.Cancelada;
            erro = "Cancelada pelo operador.";
        }
        catch (Exception ex) when (SisregWebSessao.EhCaptcha(ex))
        {
            // CAPTCHA não se resolve relogando: para e deixa registrado para um humano ver.
            status = StatusVarredura.Parcial;
            erro = "O SISREG exigiu CAPTCHA. A sincronização parou — um operador precisa resolver "
                   + "no navegador antes da próxima tentativa.";
            // Error: é o nível que leva o aviso ao celular — CAPTCHA só se resolve com gente.
            logger.LogError(ex, "SISREG_ESCALAS_CAPTCHA: sincronização interrompida.");
        }
        catch (Exception ex)
        {
            status = StatusVarredura.Erro;
            erro = ex.Message;
            logger.LogError(ex, "SISREG_ESCALAS_ERRO: falha na sincronização de escalas.");
        }
        finally
        {
            // Nada aqui pode lançar: uma exceção no finally deixaria a execução "Rodando" para sempre.
            estadoVivo.Finalizar();
            try
            {
                await FinalizarAsync(execucao, progresso, status, erro);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SISREG_ESCALAS: falha ao gravar o fim da execução {Id}.", execucao.Id);
            }

            // O catálogo canônico da Regulação (ADR-0052) pega carona na cadência diária desta
            // varredura. Não é aqui que nascem as origens SISREG — quem popula
            // `sisreg_procedimento_sigtap` é o MapeadorSigtapSisreg, na importação —, mas este é
            // o job diário de rede inteira, e o sincronismo do canônico é idempotente.
            // Fica no `finally` e engolindo exceção pela mesma razão do resto do bloco: nada
            // aqui pode deixar a execução pendurada em "Rodando".
            try
            {
                await catalogoRegulacao.SincronizarAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SISREG_ESCALAS: sincronismo do catálogo canônico da regulação falhou.");
            }
        }
    }

    private async Task SincronizarAsync(
        SisregEscalaSincronizacaoExecucao execucao, ProgressoEscalas progresso, CancellationToken ct)
    {
        progresso.Fase = ProgressoEscalas.FaseBaixando;

        // Filtros TODOS vazios = a rede inteira e o histórico completo, em uma requisição.
        // Ver a nota da classe sobre por que não se filtra por status.
        var campos = new Dictionary<string, string>
        {
            ["ups"] = string.Empty,
            ["radioFiltro"] = "cpf",
            ["cpf"] = string.Empty,
            ["pa"] = string.Empty,
            ["status"] = string.Empty,
            ["dataInicial"] = string.Empty,
            ["dataFinal"] = string.Empty,
            ["qtd_itens_pag"] = "50",
            ["pagina"] = "0",
            ["ibge"] = Ibge,
            ["ordenacao"] = string.Empty,
            ["clas_lista"] = "ASC",
            ["coluna"] = string.Empty,
            ["etapa"] = "EXPORTAR_ESCALAS",
        };

        var conteudo = await sessao.PostFormAsync(Caminho, campos, ct);
        execucao.Requisicoes = 1;

        progresso.Fase = ProgressoEscalas.FaseLendo;

        var assinatura = EscalasCsvParser.Reconhecer(conteudo);
        if (!assinatura.Reconhecido)
        {
            // Sem esta checagem, uma página de erro do SISREG viraria "0 escalas" e o operador leria
            // isso como "a rede não tem oferta hoje".
            throw new ValidacaoException(
                "escalas.resposta_inesperada",
                $"O SISREG não devolveu o arquivo de escalas: {assinatura.Motivo}");
        }

        var resultado = EscalasCsvParser.Parse(conteudo);
        progresso.EscalasLidas = resultado.Escalas.Count;
        progresso.LinhasRejeitadas = resultado.Rejeitadas.Count;

        foreach (var rejeitada in resultado.Rejeitadas.Take(20))
        {
            logger.LogWarning(
                "SISREG_ESCALAS_LINHA_REJEITADA: linha {Numero} — {Motivo}",
                rejeitada.Numero, rejeitada.Motivo);
        }

        progresso.Fase = ProgressoEscalas.FaseGravando;
        await GravarAsync(resultado.Escalas, progresso, ct);

        progresso.Fase = ProgressoEscalas.FaseAusentes;
        execucao.EscalasAusentes = await MarcarAusentesAsync(resultado.Escalas, ct);
    }

    private async Task GravarAsync(
        IReadOnlyList<EscalasCsvParser.LinhaEscala> linhas, ProgressoEscalas progresso, CancellationToken ct)
    {
        // Unidades por CNES, resolvidas de uma vez: 34 CNES contra ~17 mil linhas — buscar unidade
        // linha a linha seriam 17 mil consultas para 34 respostas distintas.
        var unidadesPorCnes = await db.Unidades
            .Where(u => u.Cnes != null && u.Cnes != string.Empty)
            .Select(u => new { u.Id, u.Cnes })
            .ToDictionaryAsync(u => u.Cnes!, u => u.Id, ct);

        var codigos = linhas.Select(l => l.CodigoEscala).ToHashSet();
        var existentes = await db.SisregEscalas
            .Where(e => codigos.Contains(e.CodigoEscala))
            .ToDictionaryAsync(e => e.CodigoEscala, ct);

        var agora = DateTime.UtcNow;
        var usuarioId = usuarioAtual.UsuarioId;
        var gravadasNoLote = 0;

        foreach (var linha in linhas)
        {
            ct.ThrowIfCancellationRequested();

            if (!unidadesPorCnes.TryGetValue(linha.Cnes, out var unidadeId))
            {
                // Sem unidade a oferta não tem onde pousar. Zero na medição (34 de 34 casaram):
                // qualquer valor aqui é unidade nova no SISREG que o catálogo ainda não descobriu.
                Interlocked.Increment(ref progresso.UnidadesNaoEncontradas);
                continue;
            }

            if (existentes.TryGetValue(linha.CodigoEscala, out var escala))
            {
                if (Aplicar(escala, linha, unidadeId, agora, usuarioId, nova: false))
                {
                    Interlocked.Increment(ref progresso.EscalasAtualizadas);
                }
                escala.VistoEm = agora;
                escala.Ausente = false;
            }
            else
            {
                escala = new SisregEscala { Id = Guid.CreateVersion7(), CriadoEm = agora, CriadoPor = usuarioId };
                Aplicar(escala, linha, unidadeId, agora, usuarioId, nova: true);
                escala.VistoEm = agora;
                db.SisregEscalas.Add(escala);
                existentes[linha.CodigoEscala] = escala;
                Interlocked.Increment(ref progresso.EscalasNovas);
            }

            Interlocked.Increment(ref progresso.EscalasGravadas);

            // Salva em lotes: 17 mil entidades num único SaveChanges incha o change tracker e faz
            // a tela ficar parada até o fim.
            if (++gravadasNoLote >= 500)
            {
                await db.SaveChangesAsync(ct);
                gravadasNoLote = 0;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Copia a linha para a entidade. Devolve <c>true</c> se algo mudou (só interessa na
    /// atualização — em escala nova tudo "mudou" por definição).</summary>
    private static bool Aplicar(
        SisregEscala e, EscalasCsvParser.LinhaEscala l, Guid unidadeId,
        DateTime agora, Guid? usuarioId, bool nova)
    {
        var mudou = !nova && (
            e.UnidadeId != unidadeId
            || e.ProfissionalCpf != l.ProfissionalCpf
            || e.ProcedimentoCodigo != l.ProcedimentoCodigo
            || e.DiaSemana != l.DiaSemana
            || e.HoraInicio != l.HoraInicio
            || e.HoraFim != l.HoraFim
            || e.VigenciaInicio != l.VigenciaInicio
            || e.VigenciaFim != l.VigenciaFim
            || e.VagasTotal != l.VagasTotal
            || e.VagasPrimeiraVez != l.VagasPrimeiraVez
            || e.VagasRetorno != l.VagasRetorno
            || e.VagasReserva != l.VagasReserva
            || e.Status != l.Status
            || e.Ausente);

        e.CodigoEscala = l.CodigoEscala;
        e.UnidadeId = unidadeId;
        e.Cnes = l.Cnes;
        e.UnidadeNomeSisreg = l.UnidadeNome;
        e.ProfissionalCpf = l.ProfissionalCpf;
        e.ProfissionalNome = l.ProfissionalNome;
        e.CboCodigo = l.CboCodigo;
        e.CboDescricao = l.CboDescricao;
        e.ProcedimentoCodigo = l.ProcedimentoCodigo;
        e.ProcedimentoNome = l.ProcedimentoNome;
        e.ProcedimentoSigtap = l.ProcedimentoSigtap;
        e.EhGrupo = l.EhGrupo;
        e.DiaSemana = l.DiaSemana;
        e.HoraInicio = l.HoraInicio;
        e.HoraFim = l.HoraFim;
        e.VigenciaInicio = l.VigenciaInicio;
        e.VigenciaFim = l.VigenciaFim;
        e.VagasPrimeiraVez = l.VagasPrimeiraVez;
        e.MinutosPrimeiraVez = l.MinutosPrimeiraVez;
        e.VagasRetorno = l.VagasRetorno;
        e.MinutosRetorno = l.MinutosRetorno;
        e.VagasReserva = l.VagasReserva;
        e.MinutosReserva = l.MinutosReserva;
        e.VagasTotal = l.VagasTotal;
        e.Status = l.Status;
        e.AgendaLocal = l.AgendaLocal;
        e.QuebraAutomatica = l.QuebraAutomatica;
        e.OperadorCriador = l.OperadorCriador;
        e.OperadorModificador = l.OperadorModificador;
        e.InseridaEmSisreg = l.InseridaEm;
        e.AlteradaEmSisreg = l.AlteradaEm;
        e.AtivadaEmSisreg = l.AtivadaEm;

        if (!nova)
        {
            e.AtualizadoEm = agora;
            e.AtualizadoPor = usuarioId;
        }

        return mudou;
    }

    /// <summary>
    /// Escala que existia aqui e não veio no arquivo saiu do SISREG. Marca como ausente em vez de
    /// apagar: o histórico de "esta vaga existia" é o que explica um agendamento antigo que hoje não
    /// teria oferta nenhuma.
    ///
    /// <para><b>Vale para qualquer status</b>, e só faz sentido porque o download traz o arquivo
    /// inteiro. Se o recorte fosse <c>status=A</c>, toda escala que vencesse entre duas execuções
    /// seria marcada ausente por engano — o sistema afirmaria que a vaga sumiu do SISREG quando ela
    /// só expirou.</para>
    ///
    /// <para>Compara por conjunto de códigos, não pelo carimbo <c>VistoEm</c>: duas execuções no
    /// mesmo tick de relógio tornariam a comparação por data indistinguível.</para>
    /// </summary>
    private async Task<int> MarcarAusentesAsync(
        IReadOnlyList<EscalasCsvParser.LinhaEscala> linhas, CancellationToken ct)
    {
        var vistos = linhas.Select(l => l.CodigoEscala).ToHashSet();

        var candidatas = await db.SisregEscalas
            .Where(e => !e.Ausente)
            .Select(e => new { e.Id, e.CodigoEscala })
            .ToListAsync(ct);

        var sumiram = candidatas.Where(c => !vistos.Contains(c.CodigoEscala)).Select(c => c.Id).ToList();
        if (sumiram.Count == 0) return 0;

        var marcadas = await db.SisregEscalas
            .Where(e => sumiram.Contains(e.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Ausente, true), ct);

        // `ExecuteUpdateAsync` escreve direto no banco e NÃO atualiza o change tracker: as
        // entidades já carregadas continuam com `Ausente = false` em memória. Se este contexto for
        // reusado, a próxima execução leria esse estado velho, concluiria que "nada mudou" e a
        // escala que voltou a aparecer no SISREG ficaria ausente para sempre.
        //
        // Desanexa SÓ as escalas — um `ChangeTracker.Clear()` levaria junto a própria
        // `SisregEscalaSincronizacaoExecucao`, e o `FinalizarAsync` gravaria em entidade
        // desanexada: a execução ficaria eternamente "Rodando" sem erro nenhum aparente.
        foreach (var entrada in db.ChangeTracker.Entries<SisregEscala>().ToList())
        {
            entrada.State = EntityState.Detached;
        }

        return marcadas;
    }

    private async Task FinalizarAsync(
        SisregEscalaSincronizacaoExecucao execucao, ProgressoEscalas progresso,
        StatusVarredura status, string? erro)
    {
        execucao.Status = status;
        execucao.MensagemErro = erro is { Length: > 2000 } ? erro[..2000] : erro;
        execucao.EscalasLidas = progresso.EscalasLidas;
        execucao.EscalasNovas = progresso.EscalasNovas;
        execucao.EscalasAtualizadas = progresso.EscalasAtualizadas;
        execucao.LinhasRejeitadas = progresso.LinhasRejeitadas;
        execucao.UnidadesNaoEncontradas = progresso.UnidadesNaoEncontradas;
        execucao.FinalizadoEm = DateTime.UtcNow;
        execucao.DuracaoSegundos = (int)(execucao.FinalizadoEm.Value - execucao.IniciadoEm).TotalSeconds;

        // CancellationToken.None: o fim tem de ser gravado mesmo quando a execução foi cancelada.
        await db.SaveChangesAsync(CancellationToken.None);

        logger.LogInformation(
            "SISREG_ESCALAS_FIM: {Status} — {Lidas} lidas, {Novas} novas, {Atualizadas} atualizadas, "
            + "{Ausentes} ausentes, {Rejeitadas} rejeitadas, {SemUnidade} sem unidade.",
            status, execucao.EscalasLidas, execucao.EscalasNovas, execucao.EscalasAtualizadas,
            execucao.EscalasAusentes, execucao.LinhasRejeitadas, execucao.UnidadesNaoEncontradas);
    }

    // ------------------------------------------------------------------ leitura

    public EscalasSincronizacaoStatusDto? ObterStatus() => estadoVivo.ObterAtual();

    public bool Cancelar() => estadoVivo.Cancelar();

    public async Task<IReadOnlyList<EscalasSincronizacaoExecucaoDto>> ListarExecucoesAsync(
        int limite = 10, CancellationToken cancellationToken = default) =>
        await db.SisregEscalaSincronizacaoExecucoes
            .AsNoTracking()
            .OrderByDescending(e => e.IniciadoEm)
            .Take(Math.Clamp(limite, 1, 50))
            .Select(e => new EscalasSincronizacaoExecucaoDto(
                e.Id, e.Disparo, e.Status, e.EscalasLidas, e.EscalasNovas, e.EscalasAtualizadas,
                e.EscalasAusentes, e.LinhasRejeitadas, e.UnidadesNaoEncontradas, e.Requisicoes,
                e.MensagemErro, e.IniciadoEm, e.FinalizadoEm, e.DuracaoSegundos, e.CriadoPorNome))
            .ToListAsync(cancellationToken);

    // ------------------------------------------------------------------ agendamento

    public async Task<EscalasAgendamentoDto> ObterAgendamentoAsync(CancellationToken cancellationToken = default)
    {
        var json = await LerParametrosAsync(cancellationToken);
        var horarios = LerHorarios(json, _opcoes.HoraPadrao);
        return new EscalasAgendamentoDto(
            json?[ChaveAtivo]?.GetValue<bool>() ?? false,
            // Primeiro da lista: mantém o contrato antigo de campo único vivo para quem ainda o lê.
            horarios[0],
            horarios,
            orcamento.Restante(orcamentoOpcoes.Value.TetoAutomatico));
    }

    /// <summary>
    /// Horários configurados, já normalizados e ordenados. Lê a lista nova; na ausência dela cai no
    /// campo único antigo; sem nenhum dos dois, no padrão. <b>Nunca devolve vazio</b> — o scheduler
    /// indexa o primeiro elemento e uma lista vazia o derrubaria a cada tick.
    /// </summary>
    internal static IReadOnlyList<string> LerHorarios(JsonObject? json, string padrao)
    {
        if (json?[ChaveHorarios] is JsonArray arr && arr.Count > 0)
        {
            var lista = new List<string>();
            foreach (var item in arr)
            {
                // Item inválido é PULADO, não derruba a leitura: um JSON editado à mão não pode
                // impedir o motor de rodar nos horários que estão certos.
                if (item?.GetValue<string>() is not { } bruto) continue;
                if (!TimeOnly.TryParseExact(bruto.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var t))
                {
                    continue;
                }
                lista.Add(t.ToString("HH:mm", CultureInfo.InvariantCulture));
            }

            if (lista.Count > 0) return [.. lista.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        }

        var unico = json?[ChaveHora]?.GetValue<string>();
        return [TimeOnly.TryParseExact(unico?.Trim(), "HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var u) ? u.ToString("HH:mm", CultureInfo.InvariantCulture) : padrao];
    }

    public async Task<EscalasAgendamentoDto> SalvarAgendamentoAsync(
        SalvarEscalasAgendamentoRequest request, CancellationToken cancellationToken = default)
    {
        var horarios = NormalizarHorarios(request);

        // Merge: baseUrl, autoLogin e o agendamento do lote de mapeamento vivem no MESMO
        // ParametrosJson — sobrescrever o JSON inteiro apagaria a credencial de acesso.
        var atual = await credenciais.ObterAsync(SisregWebSessao.Provedor, cancellationToken);
        var json = Parse(atual.ParametrosJson) ?? new JsonObject();
        json[ChaveAtivo] = request.Ativo;
        json[ChaveHorarios] = new JsonArray([.. horarios.Select(h => JsonValue.Create(h))]);
        // Espelha o primeiro no campo antigo: um rollback do binário volta a ler ChaveHora e
        // encontra um horário válido em vez do padrão de fábrica.
        json[ChaveHora] = horarios[0];

        await credenciais.AtualizarAsync(
            SisregWebSessao.Provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: null,
                ClientSecret: null,
                RedirectUri: atual.RedirectUri,
                ParametrosJson: json.ToJsonString(),
                Ativo: atual.Ativo),
            cancellationToken);

        return await ObterAgendamentoAsync(cancellationToken);
    }

    /// <summary>
    /// Horários do pedido, normalizados, sem repetição e ordenados. Aceita a lista nova ou o campo
    /// único antigo (nesta ordem) para o front poder migrar depois do backend.
    /// </summary>
    internal static IReadOnlyList<string> NormalizarHorarios(SalvarEscalasAgendamentoRequest request)
    {
        // `HoraLocal` é anulável no contrato antigo; `NormalizarHora` recusa nulo com mensagem
        // própria, então o caminho de erro continua sendo o dele, não um NullReference aqui.
        IReadOnlyList<string?> brutos = request.HorariosLocais is { Count: > 0 }
            ? [.. request.HorariosLocais]
            : [request.HoraLocal];

        var horarios = brutos
            .Select(NormalizarHora)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (horarios.Count == 0)
        {
            throw new ValidacaoException(
                "escalas.sem_horario", "Informe ao menos um horário para o sincronismo diário.");
        }

        if (horarios.Count > MaxHorariosPorDia)
        {
            throw new ValidacaoException(
                "escalas.horarios_demais",
                $"No máximo {MaxHorariosPorDia} horários por dia. Cada disparo custa 1 requisição ao "
                + "SISREG e os motores se recusam mutuamente quando um está vivo — mais que isso só "
                + "produz execuções que se atropelam.");
        }

        return horarios;
    }

    internal static string NormalizarHora(string? hora)
    {
        if (!TimeOnly.TryParseExact(hora?.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var t))
        {
            throw new ValidacaoException(
                "escalas.hora_invalida",
                $"Hora inválida: \"{hora}\". Use o formato HH:mm (ex.: 02:30).");
        }

        return t.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private async Task<JsonObject?> LerParametrosAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(SisregWebSessao.Provedor, cancellationToken);
            return Parse(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
            // Credencial ainda não configurada: a tela mostra o agendamento desligado, não um erro.
            return null;
        }
    }

    private async Task<string?> NomeDoUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.NomeCompleto)
            .FirstOrDefaultAsync(ct);

    private static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
