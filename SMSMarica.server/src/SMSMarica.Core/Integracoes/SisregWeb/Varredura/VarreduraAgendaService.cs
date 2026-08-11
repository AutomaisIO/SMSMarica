using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao;
using SMSMarica.Core.Integracoes.SisregWeb.Varredura.Background;
using SMSMarica.Core.Integracoes.SisregWeb.Varredura.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura;

public interface IVarreduraAgendaService
{
    Task<VarreduraAgendaDto> ObterAgendaAsync(CancellationToken ct);

    Task<VarreduraAgendaDto> SalvarAgendaAsync(SalvarVarreduraAgendaRequest request, CancellationToken ct);

    /// <summary>Dispara a varredura da unidade ativa, agora. ESCRITA no SISREG? Não — só leitura.</summary>
    Task<VarreduraAceitaDto> IniciarAsync(CancellationToken ct);

    /// <summary>Disparo pelo scheduler. Devolve null em colisão (não é erro: só reprograma).</summary>
    Task<Guid?> IniciarAgendadoAsync(Guid unidadeId, CancellationToken ct);

    /// <summary>Executa o job — chamado pelo runner, fora de qualquer request.</summary>
    Task ExecutarAsync(VarreduraJob job, CancellationToken ct);

    Task<StatusVarreduraVivo?> ObterStatusAsync(CancellationToken ct);

    bool Cancelar();

    Task<IReadOnlyList<VarreduraExecucaoDto>> ListarExecucoesAsync(int limite, CancellationToken ct);
}

/// <summary>
/// Varre a agenda do SISREG de uma unidade exportando o arquivo de agendamentos
/// (<c>expo_solicitacoes</c>) e entrega as marcações ao mesmo fluxo de importação do upload manual.
///
/// <para><b>Por que a exportação e não a raspagem da tela de agenda</b> (medido em 03/08/2026, no
/// CDT, mesmo par profissional × procedimento, 419 registros em julho): a exportação custa <b>1
/// requisição</b> contra <b>9</b> da tela paginada de 50 em 50 — e o orçamento de requisições é o
/// recurso escasso aqui. Além disso o arquivo traz o <b>código SIGTAP</b> (coluna 2), as datas de
/// solicitação e regulação e o endereço do paciente, que a tela de agenda simplesmente não
/// informa. E reusa o <see cref="AgendaTxtParser"/>, que já roda em produção.</para>
///
/// <para><b>Por que o produto cartesiano:</b> a exportação exige profissional e procedimento, como
/// a tela de agenda. Logo, varrer = percorrer os pares habilitados, e é por isso que os checkboxes
/// do mapeamento são a régua de custo.</para>
///
/// <para><b>Somente leitura:</b> <c>etapa=exportar</c> apenas gera o arquivo. Confirmar presença ou
/// registrar falta escreveria na agenda de verdade e não acontece em lugar nenhum daqui.</para>
///
/// <para><b>Bloqueio de horário:</b> o <c>expo_solicitacoes</c> é bloqueado pelo SISREG das 8h às
/// 15h. A janela de execução (22:00–06:00) já respeita isso — mas aqui isso deixa de ser só
/// cortesia com o operador humano e passa a ser requisito do próprio recurso.</para>
/// </summary>
public sealed class VarreduraAgendaService(
    SmsMaricaDbContext db,
    ISisregWebSessao sessao,
    ISisregUnidadeAtual unidadeAtual,
    IImportacaoSisregService importacao,
    IVarreduraSisregFila fila,
    VarreduraSisregEstadoVivo estadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    IUsuarioAtualAccessor usuarioAtual,
    IOptions<VarreduraSisregOpcoes> opcoes,
    ILogger<VarreduraAgendaService> logger) : IVarreduraAgendaService
{
    private const string Caminho = "/cgi-bin/expo_solicitacoes";

    /// <summary>Teto de registros por exportação, medido no SISREG. Ver ExportarComTetoAsync.</summary>
    private const int TetoRegistrosPorExportacao = 700;
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly VarreduraSisregOpcoes _opcoes = opcoes.Value;

    /// <summary>Par profissional × procedimento a consultar. Ordenado por (cpf, código) para o
    /// cursor de retomada ser determinístico.</summary>
    private sealed record Combinacao(string Cpf, string NomeProfissional, string Codigo, string NomeProcedimento);

    // ============================================================ agenda (config)

    public async Task<VarreduraAgendaDto> ObterAgendaAsync(CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);
        var agenda = await db.SisregVarreduraAgendas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UnidadeId == unidade.Id, ct);

        return await MontarAgendaDtoAsync(unidade, agenda, ct);
    }

    public async Task<VarreduraAgendaDto> SalvarAgendaAsync(SalvarVarreduraAgendaRequest request, CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);

        if (request.DiasAFrente < 1 || request.DiasAFrente > VarreduraSisregOpcoes.MaxDiasAFrente)
        {
            throw new ValidacaoException(
                "varredura.dias_invalidos",
                $"A janela tem que ficar entre 1 e {VarreduraSisregOpcoes.MaxDiasAFrente} dias — "
                + "o SISREG recusa consulta com intervalo maior que 31 dias.");
        }

        // A hora tem que cair na janela permitida, e isto é recusa, não aviso: o SISREG mantém uma
        // sessão por operador, então varrer no horário de expediente DERRUBA o atendente da unidade
        // que estiver usando a mesma credencial.
        if (request.Ativo && !DentroDaJanela(request.HoraLocal))
        {
            throw new ValidacaoException(
                "varredura.hora_fora_da_janela",
                $"A varredura só pode rodar entre {_opcoes.JanelaInicioLocal:HH\\:mm} e "
                + $"{_opcoes.JanelaFimLocal:HH\\:mm} (hora de Brasília). O SISREG aceita uma sessão por "
                + "operador: rodando no expediente, o motor derruba a sessão de quem estiver atendendo.");
        }

        var agenda = await db.SisregVarreduraAgendas.FirstOrDefaultAsync(x => x.UnidadeId == unidade.Id, ct);
        if (agenda is null)
        {
            agenda = new SisregVarreduraAgenda { UnidadeId = unidade.Id };
            db.SisregVarreduraAgendas.Add(agenda);
        }

        agenda.Ativo = request.Ativo;
        agenda.HoraLocal = request.HoraLocal;
        agenda.DiasAFrente = request.DiasAFrente;

        // PATCH, não PUT: quem manda payload mínimo não apaga em silêncio a decisão de não avisar
        // o paciente. Mesmo cuidado que a agenda do PEP toma com a janela noturna.
        if (request.EnviarConfirmacao is { } enviar) agenda.EnviarConfirmacao = enviar;

        agenda.AtualizadoEm = DateTime.UtcNow;

        // Ligar recalcula o próximo disparo; desligar zera. ProximoRunEm null significa NÃO
        // elegível (ver a entidade) — se ficasse null com a agenda ativa, o próximo boot dispararia
        // a varredura no meio do expediente.
        agenda.ProximoRunEm = request.Ativo
            ? DecididorVarreduraSisreg.ProximoDiario(request.HoraLocal, DateTime.UtcNow, Brasilia)
            : null;
        agenda.FalhasConsecutivas = 0;

        await db.SaveChangesAsync(ct);

        return await MontarAgendaDtoAsync(unidade, agenda, ct);
    }

    // ============================================================ disparo

    public async Task<VarreduraAceitaDto> IniciarAsync(CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);

        if (estadoVivo.ObterAtual() is not null)
        {
            throw new ConflitoException(
                "varredura.em_andamento",
                "Já há uma varredura em andamento. Espere ela terminar.");
        }

        if (importacaoEstadoVivo.ObterAtual() is not null)
        {
            throw new ConflitoException(
                "varredura.importacao_em_andamento",
                "Há uma importação de arquivo em andamento. Como as duas falam com o SISREG pela "
                + "mesma saída, espere a importação terminar.");
        }

        var (execucaoId, mensagem) = await CriarExecucaoAsync(
            unidade, DisparoSincronizacao.Manual, usuarioAtual.UsuarioId, ct);

        return new VarreduraAceitaDto(execucaoId, mensagem);
    }

    public async Task<Guid?> IniciarAgendadoAsync(Guid unidadeId, CancellationToken ct)
    {
        if (estadoVivo.ObterAtual() is not null || importacaoEstadoVivo.ObterAtual() is not null)
            return null;

        var unidade = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(u => u.Id == unidadeId, ct);
        if (unidade is null) return null;

        try
        {
            // CriadoPor null: não existe usuário-robô. A autoria é o enum Disparo.
            var (execucaoId, _) = await CriarExecucaoAsync(unidade, DisparoSincronizacao.Agendado, null, ct);
            return execucaoId;
        }
        catch (ConflitoException)
        {
            // Corrida com um disparo manual: não é erro, o scheduler só reprograma.
            return null;
        }
    }

    private async Task<(Guid ExecucaoId, string Mensagem)> CriarExecucaoAsync(
        Unidade unidade, DisparoSincronizacao disparo, Guid? usuarioId, CancellationToken ct)
    {
        var agenda = await db.SisregVarreduraAgendas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UnidadeId == unidade.Id, ct);

        var diasAFrente = agenda?.DiasAFrente ?? 21;
        var hoje = HojeBrasilia();

        var combinacoes = await CarregarCombinacoesAsync(unidade.Id, ct);
        if (combinacoes.Count == 0)
        {
            throw new ValidacaoException(
                "varredura.sem_combinacoes",
                "Nenhum par profissional × procedimento está habilitado COM código SIGTAP confirmado "
                + "nesta unidade. Habilite no mapeamento e confirme o SIGTAP dos procedimentos antes "
                + "de varrer.");
        }

        var execucao = new SisregVarreduraExecucao
        {
            Id = Guid.CreateVersion7(),
            UnidadeId = unidade.Id,
            UnidadeNome = unidade.Nome,
            Disparo = disparo,
            Status = StatusVarredura.Pendente,
            JanelaInicio = hoje,
            JanelaFim = hoje.AddDays(diasAFrente),
            CombinacoesTotal = combinacoes.Count,
            IniciadoEm = DateTime.UtcNow,
            CriadoPor = usuarioId,
            CriadoPorNome = usuarioId is null ? null : await NomeDoUsuarioAsync(usuarioId.Value, ct),
        };

        db.SisregVarreduraExecucoes.Add(execucao);
        await db.SaveChangesAsync(ct);

        if (!fila.TentarEnfileirar(new VarreduraJob(execucao.Id, unidade.Id, disparo, usuarioId)))
        {
            execucao.Status = StatusVarredura.Erro;
            execucao.MensagemErro = "A fila de varredura já estava ocupada.";
            execucao.FinalizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            throw new ConflitoException(
                "varredura.fila_cheia", "Já há uma varredura na fila. Tente de novo em instantes.");
        }

        return (execucao.Id,
            $"Varredura enfileirada: {combinacoes.Count} combinações, de {execucao.JanelaInicio:dd/MM/yyyy} "
            + $"a {execucao.JanelaFim:dd/MM/yyyy}.");
    }

    // ============================================================ execução

    public async Task ExecutarAsync(VarreduraJob job, CancellationToken ct)
    {
        var execucao = await db.SisregVarreduraExecucoes.FirstOrDefaultAsync(x => x.Id == job.ExecucaoId, ct);
        if (execucao is null) return;

        var unidade = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(u => u.Id == job.UnidadeId, ct);
        var agenda = await db.SisregVarreduraAgendas.FirstOrDefaultAsync(x => x.UnidadeId == job.UnidadeId, ct);

        if (unidade is null)
        {
            await FinalizarAsync(execucao, StatusVarredura.Erro, "Unidade não encontrada.", ct);
            return;
        }

        // O serviço roda no runner: sem isto, ResolverExecutanteAsync não acha a unidade executante
        // e TODA marcação falharia por "unidade não resolvida".
        importacao.DefinirContextoDeBackground(job.UsuarioId, unidade.Id);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var progresso = new ProgressoVarredura
        {
            ExecucaoId = execucao.Id,
            UnidadeId = unidade.Id,
            UnidadeNome = unidade.Nome,
            CombinacoesTotal = execucao.CombinacoesTotal,
        };
        estadoVivo.Iniciar(progresso, cts);

        execucao.Status = StatusVarredura.EmExecucao;
        await db.SaveChangesAsync(ct);

        try
        {
            await VarrerAsync(execucao, unidade, agenda, progresso, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            await FinalizarAsync(execucao, StatusVarredura.Cancelada, "Cancelada pelo operador.", ct, progresso);
        }
        catch (Exception ex) when (SisregWebSessao.EhCaptcha(ex))
        {
            // O CAPTCHA não é falha do nosso lado nem coisa que retentar resolva: relogar não
            // limpa. Para como PARCIAL, guardando onde parou — tudo que já entrou está gravado,
            // porque a importação acontece a cada combinação, não no fim.
            await TratarCaptchaAsync(execucao, agenda, progresso, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha na varredura da unidade {Unidade}.", unidade.Nome);
            await FinalizarAsync(execucao, StatusVarredura.Erro, ex.Message, ct, progresso);

            if (agenda is not null)
            {
                agenda.FalhasConsecutivas++;

                // Só no disparo agendado: um erro no botão manual não pode empurrar o horário
                // diário da unidade para longe.
                if (job.Disparo == DisparoSincronizacao.Agendado)
                    agenda.ProximoRunEm = DecididorVarreduraSisreg.ProximoAposErro(agenda, DateTime.UtcNow, Brasilia);
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            estadoVivo.Finalizar();

            if (agenda is not null)
            {
                agenda.UltimaExecucaoEm = DateTime.UtcNow;
                agenda.UltimaExecucaoId = execucao.Id;
                agenda.AtualizadoEm = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task VarrerAsync(
        SisregVarreduraExecucao execucao,
        Unidade unidade,
        SisregVarreduraAgenda? agenda,
        ProgressoVarredura progresso,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(unidade.Cnes))
        {
            await FinalizarAsync(execucao, StatusVarredura.Erro,
                $"A unidade '{unidade.Nome}' não tem CNES cadastrado.", ct, progresso);
            return;
        }

        // O CNES da unidade é o filtro da exportação (campo "unidade" abaixo) — é ele que
        // delimita a agenda varrida, e é por isso que não existe mais double-check de sessão
        // aqui: a credencial em uso enxerga todas as unidades, então o CNES que a sessão reporta
        // é sempre o mesmo e barraria todas as unidades menos uma.
        var cnes = SoDigitos(unidade.Cnes!);

        var combinacoes = await CarregarCombinacoesAsync(unidade.Id, ct);

        // Nada de resolver SIGTAP aqui: a exportação traz o código na coluna 2 de cada linha,
        // dito pelo próprio SISREG. É a razão principal de esta fonte ser melhor que raspar o HTML
        // da agenda, que não informa SIGTAP nenhum.

        // Retomada: o cursor só vale enquanto a janela for a mesma. Janela vencida é passado, e
        // refazer o passado gasta orçamento com dado que não muda mais.
        if (agenda is not null && DecididorVarreduraSisreg.CursorValido(agenda, execucao.JanelaFim))
        {
            var antes = combinacoes.Count;
            combinacoes = [.. combinacoes.Where(c => Depois(c, agenda!.CursorProfissionalCpf!, agenda.CursorProcedimentoCodigo))];
            logger.LogInformation(
                "SISREG_VARREDURA_RETOMADA: unidade {Unidade} retoma de {Cpf}/{Pa} — {Restantes} de {Total} combinações.",
                unidade.Nome, agenda!.CursorProfissionalCpf, agenda.CursorProcedimentoCodigo, combinacoes.Count, antes);
        }

        // Dedup global do run. Com o TXT o grupo não devolve nada (ver abaixo), então na prática
        // não há repetição — mas custa um HashSet e protege de reprocessar se isso mudar.
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var combinacao in combinacoes)
        {
            ct.ThrowIfCancellationRequested();

            if (progresso.Requisicoes >= _opcoes.TetoPorExecucao)
            {
                await PararParcialAsync(execucao, agenda, progresso, combinacao,
                    $"Teto de {_opcoes.TetoPorExecucao} requisições atingido — a varredura continua "
                    + "de onde parou na próxima execução.", ct);
                return;
            }

            progresso.ProfissionalAtual = combinacao.NomeProfissional;
            progresso.ProcedimentoAtual = combinacao.NomeProcedimento;
            progresso.CpfAtual = combinacao.Cpf;
            progresso.CodigoAtual = combinacao.Codigo;

            // Código de GRUPO é varrido normalmente. Houve uma versão que os pulava, por eu ter
            // generalizado de um único caso (GRUPO - MAMOGRAFIA devolveu 0 — mas naquele período a
            // agenda estava vazia de qualquer jeito). Medido em 05/08/2026:
            // GRUPO - ULTRASONOGRAFIA devolve 120 registros, cada linha com o SEU procedimento e o
            // SEU SIGTAP nas colunas 1 e 2. E há profissional cujo SISREG só lista códigos de
            // grupo — para ele, pular o grupo é descartar a agenda inteira.
            //
            // A preocupação de "o grupo carimbaria tudo com o procedimento do grupo" era da
            // RASPAGEM, onde o procedimento vinha da consulta. Na exportação vem da linha.

            var marcacoes = await ExportarComTetoAsync(
                cnes, execucao.JanelaInicio, execucao.JanelaFim, combinacao, progresso, ct);

            var novas = marcacoes.Where(m => vistos.Add(m.CodigoSolicitacao)).ToList();
            if (novas.Count > 0)
            {
                // Importa a cada combinação, não no fim: é isto que faz o parcial ser útil quando
                // a varredura é interrompida no meio.
                var resultado = await importacao.ImportarMarcacoesAsync(execucao.Id, novas, ct);
                Interlocked.Add(ref progresso.RegistrosEncontrados, novas.Count);
                Interlocked.Add(ref progresso.Validos, resultado.Validos);
                Interlocked.Add(ref progresso.Invalidos, resultado.Invalidos);
                execucao.JaExistiam += resultado.JaExistiam;
            }

            Interlocked.Increment(ref progresso.CombinacoesFeitas);

            // Cursor avança só depois da combinação INTEIRA (todas as páginas) — retomar no meio
            // de uma paginação perderia registros em silêncio.
            if (agenda is not null)
            {
                agenda.CursorProfissionalCpf = combinacao.Cpf;
                agenda.CursorProcedimentoCodigo = combinacao.Codigo;
                agenda.CursorJanelaFim = execucao.JanelaFim;
            }

            await SalvarProgressoAsync(execucao, progresso, ct);
        }

        // Varreu tudo: o cursor não serve mais para nada e ficaria pulando combinações amanhã.
        if (agenda is not null)
        {
            agenda.CursorProfissionalCpf = null;
            agenda.CursorProcedimentoCodigo = null;
            agenda.CursorJanelaFim = null;
            agenda.FalhasConsecutivas = 0;
        }

        await FinalizarAsync(execucao, StatusVarredura.Concluida, null, ct, progresso);

        logger.LogInformation(
            "SISREG_VARREDURA_OK: unidade {Unidade} — {Combinacoes} combinações, {Req} requisições, "
            + "{Registros} agendamentos, {Validos} importados, {Invalidos} pendências.",
            unidade.Nome, progresso.CombinacoesFeitas, progresso.Requisicoes,
            progresso.RegistrosEncontrados, progresso.Validos, progresso.Invalidos);
    }

    /// <summary>
    /// Exporta a agenda de UM par profissional × procedimento e devolve as marcações.
    ///
    /// <para><b>Teto de 700 registros por exportação</b>, medido em 03/08/2026: intervalos de 61 e
    /// de 212 dias devolveram exatamente 700 — é limite do SISREG, não coincidência. E é
    /// truncamento <b>silencioso</b>: o cabeçalho informa 700 e as linhas são 700, sem nada
    /// dizendo que faltou. Por isso, ao bater no teto, a janela é partida ao meio e reconsultada:
    /// perder agendamento sem avisar seria o pior desfecho possível aqui.</para>
    /// </summary>
    private async Task<List<MarcacaoSisreg>> ExportarComTetoAsync(
        string cnes,
        DateOnly inicio,
        DateOnly fim,
        Combinacao combinacao,
        ProgressoVarredura progresso,
        CancellationToken ct)
    {
        var texto = await ExportarAsync(cnes, inicio, fim, combinacao, ct);
        Interlocked.Increment(ref progresso.Requisicoes);

        var parsed = AgendaTxtParser.Parse(texto, NomeArquivoSintetico(combinacao, inicio, fim));

        var bateuNoTeto = parsed.Marcacoes.Count >= TetoRegistrosPorExportacao;
        if (!bateuNoTeto || inicio >= fim) return [.. parsed.Marcacoes];

        // Parte ao meio e reconsulta cada metade. A recursão termina porque a janela encolhe a cada
        // nível e para quando inicio == fim (um único dia).
        var meio = inicio.AddDays((fim.DayNumber - inicio.DayNumber) / 2);
        logger.LogWarning(
            "SISREG_EXPORT_TRUNCADA: {Proc} em {Ini}..{Fim} bateu o teto de {Teto} registros — "
            + "partindo a janela em {Ini}..{Meio} e {Meio2}..{Fim}.",
            combinacao.NomeProcedimento, inicio, fim, TetoRegistrosPorExportacao,
            inicio, meio, meio.AddDays(1), fim);

        var esquerda = await ExportarComTetoAsync(cnes, inicio, meio, combinacao, progresso, ct);
        var direita = await ExportarComTetoAsync(cnes, meio.AddDays(1), fim, combinacao, progresso, ct);

        esquerda.AddRange(direita);
        return esquerda;
    }

    private async Task<string> ExportarAsync(
        string cnes, DateOnly inicio, DateOnly fim, Combinacao combinacao, CancellationToken ct)
    {
        // Pausa ANTES da requisição. Fica aqui e não na sessão HTTP de propósito: atrasar a sessão
        // penalizaria as telas interativas (mapeamento, CADSUS), que não têm nada a ver com o
        // volume da varredura.
        if (_opcoes.PausaMs > 0) await Task.Delay(_opcoes.PausaMs, ct);

        return await sessao.PostFormAsync(Caminho, new Dictionary<string, string>
        {
            // Cultura invariante explícita: em "dd/MM/yyyy" a barra é o SEPARADOR DA CULTURA, não
            // um literal. Num host com locale que use "." ou "-", a data sairia deformada e o
            // SISREG responderia vazio — falha silenciosa, idêntica a uma agenda sem movimento.
            ["data1"] = inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["data2"] = fim.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["cpf"] = combinacao.Cpf,
            ["procedimento"] = combinacao.Codigo,
            ["tp_arquivo"] = "0", // 0 = TXT, 1 = CSV
            ["etapa"] = "exportar",
            ["unidade"] = cnes,
        }, ct);
    }

    /// <summary>
    /// O parser do TXT usa o nome do arquivo para derivar a unidade executante no formato CSV.
    /// Aqui o cabeçalho já traz o CNES, mas um nome estável ajuda a proveniência da pendência.
    /// </summary>
    private static string NomeArquivoSintetico(Combinacao combinacao, DateOnly inicio, DateOnly fim) =>
        $"sisreg-{combinacao.Codigo}-{inicio:yyyyMMdd}-{fim:yyyyMMdd}.txt";

    private async Task TratarCaptchaAsync(
        SisregVarreduraExecucao execucao,
        SisregVarreduraAgenda? agenda,
        ProgressoVarredura progresso,
        CancellationToken ct)
    {
        var pausaAte = DateTime.UtcNow.AddHours(_opcoes.CaptchaPausaHoras);

        if (agenda is not null) agenda.PausadoAte = pausaAte;

        await FinalizarAsync(execucao, StatusVarredura.Parcial,
            "O SISREG passou a exigir CAPTCHA (proteção anti-robô por volume de acessos) e a varredura "
            + "parou no meio. O que já entrou está salvo. Relogar não resolve: alguém precisa abrir o "
            + "SISREG no navegador com o operador desta unidade e resolver o CAPTCHA. A varredura "
            + "retoma de onde parou.", ct, progresso);

        logger.LogError(
            "SISREG_VARREDURA_CAPTCHA: unidade {Unidade} parou em {Prof}/{Proc} — {Feitas}/{Total} "
            + "combinações, {Req} requisições, {Validos} importados. Pausada até {Pausa}.",
            execucao.UnidadeNome, progresso.ProfissionalAtual, progresso.ProcedimentoAtual,
            progresso.CombinacoesFeitas, progresso.CombinacoesTotal, progresso.Requisicoes,
            progresso.Validos, pausaAte);
    }

    private async Task PararParcialAsync(
        SisregVarreduraExecucao execucao,
        SisregVarreduraAgenda? agenda,
        ProgressoVarredura progresso,
        Combinacao proxima,
        string motivo,
        CancellationToken ct)
    {
        if (agenda is not null) agenda.CursorJanelaFim = execucao.JanelaFim;

        await FinalizarAsync(execucao, StatusVarredura.Parcial, motivo, ct, progresso);

        logger.LogWarning(
            "SISREG_VARREDURA_PARCIAL: unidade {Unidade} parou antes de {Prof}/{Proc} — {Motivo}",
            execucao.UnidadeNome, proxima.NomeProfissional, proxima.NomeProcedimento, motivo);
    }

    private async Task FinalizarAsync(
        SisregVarreduraExecucao execucao,
        StatusVarredura status,
        string? mensagem,
        CancellationToken ct,
        ProgressoVarredura? progresso = null)
    {
        execucao.Status = status;
        execucao.MensagemErro = mensagem is null ? null : Truncar(mensagem, 2000);
        execucao.FinalizadoEm = DateTime.UtcNow;
        execucao.DuracaoSegundos = (int)(execucao.FinalizadoEm.Value - execucao.IniciadoEm).TotalSeconds;

        if (progresso is not null) CopiarProgresso(execucao, progresso);

        await db.SaveChangesAsync(ct);
    }

    private async Task SalvarProgressoAsync(
        SisregVarreduraExecucao execucao, ProgressoVarredura progresso, CancellationToken ct)
    {
        CopiarProgresso(execucao, progresso);
        await db.SaveChangesAsync(ct);
    }

    private static void CopiarProgresso(SisregVarreduraExecucao execucao, ProgressoVarredura progresso)
    {
        execucao.CombinacoesFeitas = progresso.CombinacoesFeitas;
        execucao.Requisicoes = progresso.Requisicoes;
        execucao.RegistrosEncontrados = progresso.RegistrosEncontrados;
        execucao.Validos = progresso.Validos;
        execucao.Invalidos = progresso.Invalidos;
        execucao.CursorProfissionalCpf = progresso.CpfAtual;
        execucao.CursorProcedimentoCodigo = progresso.CodigoAtual;
    }

    // ============================================================ consultas

    public Task<StatusVarreduraVivo?> ObterStatusAsync(CancellationToken ct) =>
        Task.FromResult(estadoVivo.ObterAtual());

    public bool Cancelar() => estadoVivo.Cancelar();

    public async Task<IReadOnlyList<VarreduraExecucaoDto>> ListarExecucoesAsync(int limite, CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);

        return await db.SisregVarreduraExecucoes.AsNoTracking()
            .Where(x => x.UnidadeId == unidade.Id)
            .OrderByDescending(x => x.IniciadoEm)
            .Take(Math.Clamp(limite, 1, 100))
            .Select(x => new VarreduraExecucaoDto(
                x.Id, x.UnidadeId, x.UnidadeNome, x.Disparo, x.Status, x.JanelaInicio, x.JanelaFim,
                x.CombinacoesTotal, x.CombinacoesFeitas, x.Requisicoes, x.RegistrosEncontrados,
                x.Validos, x.Invalidos, x.JaExistiam, x.MensagemErro, x.IniciadoEm, x.FinalizadoEm,
                x.DuracaoSegundos, x.CriadoPorNome))
            .ToListAsync(ct);
    }

    // ============================================================ apoio

    /// <summary>
    /// Pares habilitados, na ordem determinística do cursor.
    ///
    /// <para>O código do SISREG aqui é <b>só o filtro da varredura</b> — o que consultar. Ele não
    /// decide o que o exame é: isso vem do próprio agendamento, na importação. Por isso não há
    /// nenhum pré-requisito de SIGTAP aqui: exigir o de-para antes de varrer obrigaria a mapear
    /// procedimento que talvez nunca tenha agendamento nenhum.</para>
    /// </summary>
    private async Task<List<Combinacao>> CarregarCombinacoesAsync(Guid unidadeId, CancellationToken ct)
    {
        var candidatos = await db.SisregProfissionaisUnidade.AsNoTracking()
            .Where(p => p.UnidadeId == unidadeId && p.Habilitado && !p.Ausente)
            .SelectMany(p => p.Procedimentos
                .Where(x => x.Habilitado && !x.Ausente)
                .Select(x => new Combinacao(p.Cpf, p.Nome, x.Codigo, x.Nome)))
            .ToListAsync(ct);

        return [.. candidatos
            .OrderBy(c => c.Cpf, StringComparer.Ordinal)
            .ThenBy(c => c.Codigo, StringComparer.Ordinal)];
    }

    private async Task<VarreduraAgendaDto> MontarAgendaDtoAsync(
        Unidade unidade, SisregVarreduraAgenda? agenda, CancellationToken ct)
    {
        var prontas = await db.SisregProfissionaisUnidade.AsNoTracking()
            .Where(p => p.UnidadeId == unidade.Id && p.Habilitado && !p.Ausente)
            .SelectMany(p => p.Procedimentos.Where(x => x.Habilitado && !x.Ausente).Select(x => x.Codigo))
            .CountAsync(ct);

        return new VarreduraAgendaDto(
            unidade.Id,
            unidade.Nome,
            agenda?.Ativo ?? false,
            agenda?.HoraLocal ?? new TimeOnly(4, 30),
            agenda?.DiasAFrente ?? 21,
            agenda?.ProximoRunEm,
            agenda?.PausadoAte,
            agenda?.UltimaExecucaoEm,
            agenda?.FalhasConsecutivas ?? 0,
            prontas,
            // Uma exportação por combinação; páginas extras entram por cima.
            prontas,
            _opcoes.TetoPorExecucao,
            _opcoes.JanelaInicioLocal,
            _opcoes.JanelaFimLocal,
            // Unidade sem linha de configuração NÃO envia: o gatilho é opt-in.
            agenda?.EnviarConfirmacao ?? false);
    }

    private async Task<string?> NomeDoUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.NomeCompleto)
            .FirstOrDefaultAsync(ct);

    /// <summary>A combinação vem depois do cursor na ordem (cpf, código)?</summary>
    private static bool Depois(Combinacao c, string cursorCpf, string? cursorCodigo)
    {
        var porCpf = string.CompareOrdinal(c.Cpf, cursorCpf);
        if (porCpf != 0) return porCpf > 0;
        return string.CompareOrdinal(c.Codigo, cursorCodigo ?? string.Empty) > 0;
    }

    private bool DentroDaJanela(TimeOnly hora)
    {
        var inicio = _opcoes.JanelaInicioLocal;
        var fim = _opcoes.JanelaFimLocal;

        // Janela que cruza a meia-noite (22:00–06:00) é o caso normal aqui, não a exceção.
        return inicio <= fim
            ? hora >= inicio && hora <= fim
            : hora >= inicio || hora <= fim;
    }

    private static DateOnly HojeBrasilia() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Brasilia));

    private static string SoDigitos(string valor) => new([.. valor.Where(char.IsDigit)]);

    private static string Truncar(string valor, int max) =>
        valor.Length <= max ? valor : valor[..max];
}
