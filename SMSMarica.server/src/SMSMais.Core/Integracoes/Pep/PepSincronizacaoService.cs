using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.Pep.Background;
using SMSMais.Core.Integracoes.Pep.Divergencias;
using SMSMais.Core.Integracoes.Pep.Dtos;
using SMSMais.Core.Integracoes.Pep.Estrategias;
using SMSMais.Core.Integracoes.Pep.Falhas;
using SMSMais.Core.Integracoes.Pep.Fhir;
using SMSMais.Core.Integracoes.Pep.Progresso;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Pep;

namespace SMSMais.Core.Integracoes.Pep;

public sealed class PepSincronizacaoService(
    SmsMaisDbContext db,
    IDbContextFactory<SmsMaisDbContext> dbFactory,
    IProtetorSegredos protetor,
    IEnumerable<IEstrategiaImportacaoPep> estrategias,
    IHubFhirEscritor escritor,
    IPepSincronizacaoFila fila,
    PepSincronizacaoEstadoVivo estadoVivo,
    IUsuarioAtualAccessor usuarioAtual,
    IVerificadorDivergenciasPep verificadorDivergencias,
    Inteligencia.Fontes.Agente.IAgenteSqlRegistry agenteRegistry,
    IConfiguration configuration,
    ILogger<PepSincronizacaoService> logger) : IPepSincronizacaoService
{
    private readonly int _timeoutSegundos = configuration.GetValue("Pep:TimeoutSegundos", 120);

    /// <summary>
    /// Teto de linhas por consulta ao agente na IMPORTAÇÃO. Não dá para reusar o
    /// <c>Ia:RowLimit</c> (1.000): ele existe para uma pessoa fazendo pergunta na tela, e aqui
    /// truncaria a página em silêncio — o pior modo de falhar, porque o run termina "com
    /// sucesso" tendo pulado registros.
    /// </summary>
    private readonly int _agenteMaxLinhas = configuration.GetValue("Pep:Agente:MaxLinhas", 20_000);

    /// <summary>
    /// "Apagar antes" purga a base INTEIRA do hub e reescreve. Fica <b>desligado por padrão</b>:
    /// com todo recurso clínico entrando por upsert idempotente, ele não é necessário para
    /// reconciliar — e, se o run for interrompido no meio (a API reinicia a cada deploy), o que
    /// já foi apagado e ainda não reescrito simplesmente some do prontuário até alguém notar.
    ///
    /// <para>A capacidade continua existindo para o caso real de precisar reconstruir uma base
    /// do zero, mas ligar exige mudar <c>Pep:PermitirApagarAntes</c> e reiniciar o serviço —
    /// um ato deliberado e auditável, não um checkbox na tela.</para>
    /// </summary>
    private readonly bool _permitirApagarAntes =
        configuration.GetValue("Pep:PermitirApagarAntes", false);

    /// <summary>
    /// Teto de divergências por rodada de arbitragem MANUAL (pela tela). O job automático
    /// (<see cref="Background.VerificadorDivergenciasScheduler"/>) tem a própria configuração.
    /// </summary>
    private readonly int _maxArbitragensPorRodada =
        configuration.GetValue("Pep:Divergencias:MaxPorRodada", 50);

    /// <summary>Teto de TEMPO da rodada manual — protege a request de ficar pendurada.</summary>
    private readonly TimeSpan _tetoArbitragem = TimeSpan.FromSeconds(
        Math.Clamp(configuration.GetValue("Pep:Divergencias:TetoSegundos", 120), 10, 600));

    /// <summary>Tempo máximo reenviando um recurso por saturação transitória antes de desistir (vira falha).</summary>
    private readonly TimeSpan _hubRetryBudget =
        TimeSpan.FromSeconds(configuration.GetValue("Pep:HubRetryBudgetSegundos", 60));

    public async Task<IReadOnlyList<BasePepDto>> ListarBasesAsync(CancellationToken ct = default)
    {
        var fontes = await db.IaFontes.AsNoTracking()
            .Where(f => f.ExcluidoEm == null)
            .OrderBy(f => f.Nome)
            .ToListAsync(ct);

        var ultimas = await db.PepSincronizacaoExecucoes.AsNoTracking()
            .Where(e => e.Status == StatusSincronizacao.Concluido)
            .GroupBy(e => e.FonteId)
            .Select(g => new { FonteId = g.Key, Ultima = g.Max(e => e.FinalizadoEm) })
            .ToDictionaryAsync(x => x.FonteId, x => x.Ultima, ct);

        var cursores = await db.PepSincronizacaoEstados.AsNoTracking()
            .ToDictionaryAsync(e => e.FonteId, e => e.PacienteCursorCd, ct);

        return fontes.Select(f => new BasePepDto(
            f.Id, f.Nome, f.Tipo.ToString(), f.Ambiente.ToString(),
            estrategias.Any(e => e.Atende(f)),
            ultimas.GetValueOrDefault(f.Id),
            cursores.GetValueOrDefault(f.Id))).ToList();
    }

    public async Task<Guid> IniciarAsync(IniciarImportacaoRequest request, CancellationToken ct = default)
    {
        var fonte = await db.IaFontes.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FonteId && f.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("IaFonte", request.FonteId);

        if (!fonte.Ativo)
            throw new ValidacaoException("pep.base_inativa", $"Base '{fonte.Nome}' está inativa.");

        if (!estrategias.Any(e => e.Atende(fonte)))
            throw new ValidacaoException("pep.tipo_nao_suportado",
                $"Importação ainda não suportada para a base '{fonte.Nome}' (tipo '{fonte.Tipo}', família '{fonte.Familia ?? "—"}').");

        if (string.IsNullOrWhiteSpace(fonte.Slug))
            throw new ValidacaoException("pep.base_sem_slug",
                $"Base '{fonte.Nome}' sem slug. Defina um slug curto e estável no cadastro da base (ex.: 'salux-hcml') antes de importar — ele identifica a origem e evita colisão entre hospitais.");

        if (request.Escopo == EscopoSincronizacao.Limitado &&
            request is { MaxMedicos: null or <= 0, MaxPacientes: null or <= 0 })
            throw new ValidacaoException("pep.limites",
                "No escopo Limitado informe ao menos um limite (médicos e/ou pacientes) maior que zero.");

        // "Apagar antes" purga a base INTEIRA (do source) antes do laço de pacientes; se o laço
        // for parcial — retomado de um cursor ou limitado por N — tudo que ficou de fora seria
        // apagado e NUNCA reescrito (o incremental não repõe: as marcas seguem avançando).
        // Combinação proibida, não apenas desaconselhada.
        if (request.ApagarAntes && !_permitirApagarAntes)
            throw new ValidacaoException("pep.apagar_antes_bloqueado",
                "\"Apagar antes\" está bloqueado: ele apaga a base inteira do hub antes de reescrever, "
                + "e uma interrupção no meio (deploy, queda) deixaria o prontuário incompleto sem aviso. "
                + "O upsert já reconcilia sem apagar. Para um rebuild real, ligue Pep:PermitirApagarAntes no servidor.");

        if (request.ApagarAntes && request.Escopo == EscopoSincronizacao.Limitado)
            throw new ValidacaoException("pep.apagar_antes_limitado",
                "\"Apagar antes\" só pode ser usado com escopo Tudo: no escopo Limitado a purga apagaria a base inteira e a reescrita cobriria apenas parte dela.");

        if (request.ApagarAntes && request.CursorPacienteInicial is not null)
            throw new ValidacaoException("pep.apagar_antes_cursor",
                "\"Apagar antes\" não pode ser combinado com retomada por cursor: a purga apaga toda a base e a reescrita começaria do cursor, deixando sem dado clínico todos os pacientes anteriores a ele.");

        // No máximo uma importação por vez — mas só bloqueia se há run VIVO (em memória).
        if (estadoVivo.ObterAtual() is not null)
            throw new ConflitoException("pep.importacao_em_andamento", "Já existe uma importação em andamento. Aguarde concluir.");

        // Linhas EmExecucao/Pendente sem run vivo são órfãs (processo reiniciou ou crashou na
        // finalização) — encerra como Erro pra não travar novas importações.
        var orfas = await db.PepSincronizacaoExecucoes
            .Where(e => e.Status == StatusSincronizacao.Pendente || e.Status == StatusSincronizacao.EmExecucao)
            .ToListAsync(ct);
        foreach (var o in orfas)
        {
            o.Status = StatusSincronizacao.Erro;
            o.MensagemErro ??= "Execução interrompida (órfã) — encerrada ao iniciar nova importação.";
            o.FinalizadoEm ??= DateTime.UtcNow;
        }
        if (orfas.Count > 0) await db.SaveChangesAsync(ct);

        var execucao = new PepSincronizacaoExecucao
        {
            Id = Guid.CreateVersion7(),
            FonteId = fonte.Id,
            FonteNome = fonte.Nome,
            Modo = request.Modo,
            Escopo = request.Escopo,
            ApagarAntes = request.ApagarAntes,
            MaxMedicos = request.Escopo == EscopoSincronizacao.Limitado ? request.MaxMedicos : null,
            MaxPacientes = request.Escopo == EscopoSincronizacao.Limitado ? request.MaxPacientes : null,
            Status = StatusSincronizacao.Pendente,
            IniciadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.PepSincronizacaoExecucoes.Add(execucao);
        await db.SaveChangesAsync(ct);

        // Cursor de retomada só faz sentido em Completo + Tudo; ignora nos demais.
        var cursorInicial = request.Modo == ModoSincronizacao.Completo && request.Escopo == EscopoSincronizacao.Tudo
            ? request.CursorPacienteInicial
            : null;

        var opcoes = new OpcoesImportacao(request.Modo, request.Escopo, request.MaxMedicos, request.MaxPacientes,
            request.ApagarAntes, CdsPacientes: request.CdsPacientes,
            Concorrencia: request.Concorrencia, CursorPacienteInicial: cursorInicial,
            CodigosPacientes: request.CodigosPacientes);
        if (!fila.TentarEnfileirar(new PepImportacaoJob(execucao.Id, fonte.Id, opcoes, usuarioAtual.UsuarioId)))
        {
            execucao.Status = StatusSincronizacao.Erro;
            execucao.MensagemErro = "Fila ocupada — já há uma importação enfileirada.";
            execucao.FinalizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            throw new ConflitoException("pep.fila_ocupada", "Já existe uma importação enfileirada.");
        }

        return execucao.Id;
    }

    public async Task<Guid?> IniciarAgendadoAsync(Guid fonteId, bool forcarMedicos, CancellationToken ct = default)
    {
        // Disparo do scheduler: colisão e indisponibilidade NÃO são erro — devolve null e o
        // scheduler reprograma. Só configuração inválida merece log (alguém precisa agir).
        var fonte = await db.IaFontes.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fonteId && f.ExcluidoEm == null, ct);
        if (fonte is null || !fonte.Ativo || string.IsNullOrWhiteSpace(fonte.Slug)
            || estrategias.All(e => e.Tipo != fonte.Tipo))
        {
            logger.LogWarning("Agenda da fonte {Fonte}: base ausente/inativa/sem slug/não suportada — disparo agendado ignorado.", fonteId);
            return null;
        }

        if (estadoVivo.ObterAtual() is not null) return null;

        var orfas = await db.PepSincronizacaoExecucoes
            .Where(e => e.Status == StatusSincronizacao.Pendente || e.Status == StatusSincronizacao.EmExecucao)
            .ToListAsync(ct);
        foreach (var o in orfas)
        {
            o.Status = StatusSincronizacao.Erro;
            o.MensagemErro ??= "Execução interrompida (órfã) — encerrada ao iniciar nova importação.";
            o.FinalizadoEm ??= DateTime.UtcNow;
        }

        var execucao = new PepSincronizacaoExecucao
        {
            Id = Guid.CreateVersion7(),
            FonteId = fonte.Id,
            FonteNome = fonte.Nome,
            Modo = ModoSincronizacao.Incremental,
            Escopo = EscopoSincronizacao.Tudo,
            ApagarAntes = false,
            Status = StatusSincronizacao.Pendente,
            Disparo = DisparoSincronizacao.Agendado,
            IniciadoEm = DateTime.UtcNow,
            CriadoPor = null,
        };
        db.PepSincronizacaoExecucoes.Add(execucao);
        await db.SaveChangesAsync(ct);

        var opcoes = new OpcoesImportacao(ModoSincronizacao.Incremental, EscopoSincronizacao.Tudo,
            null, null, ApagarAntes: false, ForcarMedicos: forcarMedicos);
        if (!fila.TentarEnfileirar(new PepImportacaoJob(execucao.Id, fonte.Id, opcoes, null)))
        {
            execucao.Status = StatusSincronizacao.Erro;
            execucao.MensagemErro = "Fila ocupada — já há uma importação enfileirada.";
            execucao.FinalizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return null;
        }

        return execucao.Id;
    }

    public async Task<IReadOnlyList<AgendaPepDto>> ListarAgendasAsync(CancellationToken ct = default)
    {
        var agendas = await db.PepSincronizacaoAgendas.AsNoTracking().ToListAsync(ct);
        var nomes = await db.IaFontes.AsNoTracking()
            .Where(f => f.ExcluidoEm == null)
            .ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
        return agendas
            .OrderBy(a => nomes.GetValueOrDefault(a.FonteId, string.Empty))
            .Select(a => new AgendaPepDto(
                a.FonteId, nomes.GetValueOrDefault(a.FonteId, "(base excluída)"), a.Ativo, a.IntervaloMinutos,
                a.JanelaInicioLocal, a.JanelaFimLocal, a.MedicoRescanHoras, a.FalhasConsecutivas,
                a.ProximoRunEm, a.PausadoAte, a.AtualizadoEm))
            .ToList();
    }

    public async Task<AgendaPepDto> SalvarAgendaAsync(SalvarAgendaPepRequest request, CancellationToken ct = default)
    {
        var fonte = await db.IaFontes.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FonteId && f.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("IaFonte", request.FonteId);

        if (request.IntervaloMinutos < 5)
            throw new ValidacaoException("pep.agenda_intervalo", "O intervalo mínimo da agenda é 5 minutos.");
        if (request.MedicoRescanHoras is { } h && h < 1)
            throw new ValidacaoException("pep.agenda_medico_rescan", "O re-scan de médicos precisa de pelo menos 1 hora.");
        if (request.JanelaInicioLocal is null != request.JanelaFimLocal is null)
            throw new ValidacaoException("pep.agenda_janela", "Informe início E fim da janela, ou nenhum dos dois.");

        var agenda = await db.PepSincronizacaoAgendas.FirstOrDefaultAsync(a => a.FonteId == request.FonteId, ct);
        if (agenda is null)
        {
            agenda = new PepSincronizacaoAgenda { FonteId = request.FonteId };
            db.PepSincronizacaoAgendas.Add(agenda);
        }
        agenda.Ativo = request.Ativo;
        agenda.IntervaloMinutos = request.IntervaloMinutos;
        // Janela e pausa são PATCH, não PUT: a tela envia payload mínimo (ativo + intervalo) e,
        // se sobrescrevêssemos com null, salvar pela UI apagaria em silêncio a janela noturna e
        // a pausa administrativa configuradas por fora. Só muda quem foi informado — mesma regra
        // já usada em MedicoRescanHoras. Para LIMPAR a janela, mande início e fim vazios juntos
        // (o par é validado acima), e para tirar a pausa use PausadoAte no passado.
        if (request.JanelaInicioLocal is not null || request.JanelaFimLocal is not null)
        {
            agenda.JanelaInicioLocal = request.JanelaInicioLocal;
            agenda.JanelaFimLocal = request.JanelaFimLocal;
        }
        if (request.MedicoRescanHoras is { } rescan) agenda.MedicoRescanHoras = rescan;
        if (request.PausadoAte is not null) agenda.PausadoAte = request.PausadoAte;
        // Religar/editar zera o backoff — o operador acabou de mexer, quer ver rodando.
        agenda.FalhasConsecutivas = 0;
        agenda.ProximoRunEm = null;
        agenda.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new AgendaPepDto(agenda.FonteId, fonte.Nome, agenda.Ativo, agenda.IntervaloMinutos,
            agenda.JanelaInicioLocal, agenda.JanelaFimLocal, agenda.MedicoRescanHoras,
            agenda.FalhasConsecutivas, agenda.ProximoRunEm, agenda.PausadoAte, agenda.AtualizadoEm);
    }

    public async Task<DiagnosticoPepDto> ObterDiagnosticoAsync(Guid fonteId, CancellationToken ct = default)
    {
        var fonte = await db.IaFontes.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fonteId && f.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("IaFonte", fonteId);
        if (fonte.Tipo != TipoFonte.Salux || string.IsNullOrWhiteSpace(fonte.Slug))
            throw new ValidacaoException("pep.diagnostico", "Diagnóstico disponível apenas para bases Salux com slug.");

        var estado = await db.PepSincronizacaoEstados.AsNoTracking()
            .FirstOrDefaultAsync(s => s.FonteId == fonteId, ct);

        // Pendências na ORIGEM desde cada marca (queries de contagem baratas/indexáveis).
        long? pacPend = null, baaPend = null, fiaPend = null, edocLogPend = null;
        if (!string.IsNullOrWhiteSpace(fonte.Host) && !string.IsNullOrWhiteSpace(fonte.SenhaCifrada))
        {
            await using var oracle = new Integracoes.Pep.Leitura.LeitorOracleHis(
                fonte.Host!, fonte.Porta ?? 1521, fonte.Servico!, fonte.Usuario!,
                protetor.Revelar(fonte.SenhaCifrada!), _timeoutSegundos, tentativasConexao: 1);
            await oracle.AbrirAsync(ct, _ => { });

            async Task<long> Contar(string sql) =>
                (await oracle.LerAsync(sql, r => Col(r), ct)).FirstOrDefault();

            if (estado?.UltimoSyncPacienteEm is { } mp)
                pacPend = await Contar(Estrategias.Salux.SaluxImportacaoStrategy.SqlContagemPacientesPendentes(mp));
            if (estado?.UltimoSyncAtendimentoEm is { } mb)
                baaPend = await Contar(Estrategias.Salux.SaluxImportacaoStrategy.SqlContagemBaasPendentes(mb));
            if (estado?.UltimoSyncInternacaoEm is { } mf)
                fiaPend = await Contar(Estrategias.Salux.SaluxImportacaoStrategy.SqlContagemFiasPendentes(mf));
            if (estado?.UltimoSyncLogDocumentoId is { } ml)
                edocLogPend = await Contar(Estrategias.Salux.SaluxImportacaoStrategy.SqlContagemEdocLogPendentes(ml));
        }

        var source = $"{Estrategias.Salux.SaluxFhirMapper.SourceBase}/salux/{fonte.Slug}";
        var hubJson = await escritor.ObterEstatisticasAsync(source, ct);
        var hub = JsonSerializer.Deserialize<JsonElement>(hubJson);

        return new DiagnosticoPepDto(
            fonte.Id, fonte.Nome, fonte.Slug!,
            estado?.UltimoSyncProfissionalEm, estado?.UltimoSyncPacienteEm, estado?.UltimoSyncAtendimentoEm,
            estado?.UltimoSyncDocumentoEm, estado?.UltimoSyncInternacaoEm, estado?.UltimoSyncLogDocumentoId,
            pacPend, baaPend, fiaPend, edocLogPend, hub);

        static long Col(Oracle.ManagedDataAccess.Client.OracleDataReader r) =>
            r.IsDBNull(0) ? 0 : Convert.ToInt64(r.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Para o run em andamento e, opcionalmente, PAUSA o motor. Sem a pausa, parar não é
    /// backout: o cancelamento conta como sucesso no pós-run e o scheduler religa sozinho no
    /// próximo intervalo (com agenda de 30 min, a importação renasce em até 30 min). Em
    /// incidente é o oposto do que o operador espera.
    /// </summary>
    public async Task CancelarAsync(int? pausarHoras = null, CancellationToken ct = default)
    {
        var vivo = estadoVivo.ObterAtual()
            ?? throw new ConflitoException("pep.sem_importacao", "Não há importação em andamento para parar.");

        if (!estadoVivo.Cancelar())
            throw new ConflitoException("pep.cancelamento_indisponivel",
                "Não foi possível solicitar o cancelamento — a importação pode já ter finalizado.");

        logger.LogInformation("Cancelamento de importação solicitado por {Usuario}.", usuarioAtual.UsuarioId);

        if (pausarHoras is { } horas && horas > 0 && vivo.FonteId is { } fonteId)
        {
            await PausarMotorAsync(fonteId, horas, ct);
            logger.LogWarning(
                "Motor da base {Fonte} PAUSADO por {Horas}h junto com a parada, por {Usuario}.",
                fonteId, horas, usuarioAtual.UsuarioId);
        }
    }

    /// <summary>
    /// Pausa administrativa do motor de uma base: <paramref name="horas"/> &gt; 0 pausa até
    /// lá; null ou 0 RETOMA (limpa a pausa). Para desligar de vez, desative a agenda.
    /// </summary>
    public async Task<AgendaPepDto> PausarMotorAsync(Guid fonteId, int? horas, CancellationToken ct = default)
    {
        var agenda = await db.PepSincronizacaoAgendas.FirstOrDefaultAsync(a => a.FonteId == fonteId, ct)
            ?? throw new NaoEncontradoException("PepSincronizacaoAgenda", fonteId);

        agenda.PausadoAte = horas is { } h && h > 0 ? DateTime.UtcNow.AddHours(Math.Min(h, 24 * 30)) : null;
        agenda.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var nome = await db.IaFontes.AsNoTracking()
            .Where(f => f.Id == fonteId).Select(f => f.Nome).FirstOrDefaultAsync(ct) ?? string.Empty;
        return new AgendaPepDto(
            agenda.FonteId, nome, agenda.Ativo, agenda.IntervaloMinutos,
            agenda.JanelaInicioLocal, agenda.JanelaFimLocal, agenda.MedicoRescanHoras,
            agenda.FalhasConsecutivas, agenda.ProximoRunEm, agenda.PausadoAte, agenda.AtualizadoEm);
    }

    public async Task<StatusImportacaoDto> ObterStatusAsync(CancellationToken ct = default)
    {
        if (estadoVivo.ObterAtual() is { } vivo) return vivo;

        var ultima = await db.PepSincronizacaoExecucoes.AsNoTracking()
            .OrderByDescending(e => e.IniciadoEm)
            .FirstOrDefaultAsync(ct);

        if (ultima is null)
            return new StatusImportacaoDto(null, false, null, null, null, null, "Nenhuma", null, null, null, null,
                new ContadoresImportacaoDto(0, 0, 0, 0, 0, 0, 0, 0), null, []);

        return new StatusImportacaoDto(
            ultima.Id, false, ultima.FonteId, ultima.FonteNome, ultima.Modo.ToString(), ultima.Escopo.ToString(),
            ultima.Status.ToString(), null, ultima.IniciadoEm, ultima.FinalizadoEm, ultima.DuracaoSegundos,
            Contadores(ultima), ultima.MensagemErro, []);
    }

    public async Task<IReadOnlyList<ExecucaoImportacaoDto>> ListarExecucoesAsync(Guid? fonteId = null, CancellationToken ct = default)
    {
        var q = db.PepSincronizacaoExecucoes.AsNoTracking().AsQueryable();
        if (fonteId is { } id) q = q.Where(e => e.FonteId == id);
        var execs = await q.OrderByDescending(e => e.IniciadoEm).Take(50).ToListAsync(ct);
        return execs.Select(e => new ExecucaoImportacaoDto(
            e.Id, e.FonteId, e.FonteNome, e.Modo.ToString(), e.Escopo.ToString(), e.Status.ToString(),
            e.IniciadoEm, e.FinalizadoEm, e.DuracaoSegundos, Contadores(e), e.TemposJson, e.MensagemErro,
            e.Disparo.ToString())).ToList();
    }

    public async Task<IReadOnlyList<FalhaImportacaoDto>> ListarFalhasAsync(
        Guid? execucaoId = null, Guid? fonteId = null, bool somentePendentes = false, CancellationToken ct = default)
    {
        var q = db.PepSincronizacaoFalhas.AsNoTracking().AsQueryable();
        if (execucaoId is { } eid) q = q.Where(f => f.ExecucaoId == eid);
        if (fonteId is { } fid) q = q.Where(f => f.FonteId == fid);
        if (somentePendentes) q = q.Where(f => f.ResolvidoEm == null);

        var falhas = await q.OrderByDescending(f => f.CriadoEm).Take(1000).ToListAsync(ct);
        return falhas.Select(f => new FalhaImportacaoDto(
            f.Id, f.ExecucaoId, f.FonteId, f.FonteSlug, f.CdPaciente, f.Mensagem, f.CriadoEm, f.ResolvidoEm)).ToList();
    }

    /// <summary>
    /// Retrato das divergências já conhecidas da base: CPF → congelar? Carregado uma vez por run.
    /// Congelam as que ainda não têm veredicto (pendente/não conclusiva) e as que já apontaram
    /// contra a origem (hub correto / ambos negados). Não congelam — mas continuam conhecidas,
    /// para não re-registrar — "origem correta" (aí a origem deve mesmo corrigir o hub) e as
    /// ignoradas por um operador.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, bool>> CarregarDivergenciasConhecidasAsync(
        Guid fonteId, CancellationToken ct)
    {
        var linhas = await db.PepDivergenciasIdentidade.AsNoTracking()
            .Where(d => d.FonteId == fonteId)
            .Select(d => new { d.Cpf, d.Status, d.Veredicto })
            .ToListAsync(ct);

        var mapa = new Dictionary<string, bool>(linhas.Count, StringComparer.Ordinal);
        foreach (var l in linhas)
        {
            var congelar = l.Status switch
            {
                StatusDivergenciaIdentidade.Ignorada => false,
                StatusDivergenciaIdentidade.Verificada =>
                    l.Veredicto is VeredictoDivergenciaIdentidade.HubCorreto
                        or VeredictoDivergenciaIdentidade.AmbosNegados,
                _ => true, // pendente / não conclusiva → na dúvida, o hub prevalece
            };
            mapa[l.Cpf] = congelar;
        }
        return mapa;
    }

    public async Task<IReadOnlyList<DivergenciaIdentidadeDto>> ListarDivergenciasAsync(
        Guid? fonteId = null, StatusDivergenciaIdentidade? status = null, CancellationToken ct = default)
    {
        var q = db.PepDivergenciasIdentidade.AsNoTracking().AsQueryable();
        if (fonteId is { } fid) q = q.Where(d => d.FonteId == fid);
        if (status is { } st) q = q.Where(d => d.Status == st);

        var linhas = await q
            .OrderBy(d => d.Status)
            .ThenByDescending(d => d.AtualizadoEm)
            .Take(1000)
            .ToListAsync(ct);

        return linhas.Select(d => new DivergenciaIdentidadeDto(
            d.Id, d.FonteId, d.FonteSlug, d.CdPaciente, d.Cpf, d.Tipo.ToString(),
            d.ValorOrigem, d.ValorHub, d.NomeOrigem, d.NomeHub, d.PatientIdHub,
            d.Status.ToString(), d.Veredicto.ToString(), d.VeredictoMotor, d.ValorCorreto,
            d.NomeOficial, d.Detalhe, d.Ocorrencias, d.CriadoEm, d.AtualizadoEm,
            d.VerificadoEm, d.ResolvidoEm)).ToList();
    }

    public async Task<ResumoDivergenciasDto> ResumoDivergenciasAsync(
        Guid? fonteId = null, CancellationToken ct = default)
    {
        var q = db.PepDivergenciasIdentidade.AsNoTracking().AsQueryable();
        if (fonteId is { } fid) q = q.Where(d => d.FonteId == fid);

        var porStatus = await q.GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, N = g.Count() }).ToListAsync(ct);
        var porVeredicto = await q.Where(d => d.Status == StatusDivergenciaIdentidade.Verificada)
            .GroupBy(d => d.Veredicto)
            .Select(g => new { V = g.Key, N = g.Count() }).ToListAsync(ct);

        int St(StatusDivergenciaIdentidade s) => porStatus.FirstOrDefault(x => x.Status == s)?.N ?? 0;
        int Vr(VeredictoDivergenciaIdentidade v) => porVeredicto.FirstOrDefault(x => x.V == v)?.N ?? 0;

        return new ResumoDivergenciasDto(
            Total: porStatus.Sum(x => x.N),
            Pendentes: St(StatusDivergenciaIdentidade.Pendente),
            NaoConclusivas: St(StatusDivergenciaIdentidade.NaoConclusiva),
            Ignoradas: St(StatusDivergenciaIdentidade.Ignorada),
            OrigemCorreta: Vr(VeredictoDivergenciaIdentidade.OrigemCorreta),
            HubCorreto: Vr(VeredictoDivergenciaIdentidade.HubCorreto),
            AmbosNegados: Vr(VeredictoDivergenciaIdentidade.AmbosNegados),
            Congelados: St(StatusDivergenciaIdentidade.Pendente)
                + St(StatusDivergenciaIdentidade.NaoConclusiva)
                + Vr(VeredictoDivergenciaIdentidade.HubCorreto)
                + Vr(VeredictoDivergenciaIdentidade.AmbosNegados));
    }

    public async Task<ResultadoVerificacaoDivergencias> VerificarDivergenciasAsync(
        Guid? fonteId = null, int? max = null, CancellationToken ct = default) =>
        await verificadorDivergencias.VerificarPendentesAsync(
            fonteId, max ?? _maxArbitragensPorRodada, _tetoArbitragem, ct);

    /// <summary>
    /// Reprocessa da origem os pacientes cujas divergências já foram arbitradas como
    /// <see cref="VeredictoDivergenciaIdentidade.OrigemCorreta"/>.
    ///
    /// <para><b>Por que é preciso empurrar.</b> Quando o veredicto diz que a origem está
    /// certa, o congelamento cai — mas o valor só chega ao hub quando aquele paciente for
    /// tocado de novo, e quem não tem atendimento novo pode ficar meses com o dado errado.
    /// Este disparo relê os pacientes na origem e deixa o caminho normal escrever: mesmo
    /// merge, mesmo If-Match, mesma trilha. É o oposto de acertar a coluna na mão.</para>
    /// </summary>
    public async Task<Guid> ReprocessarDivergenciasResolvidasAsync(
        Guid fonteId, IReadOnlyList<Guid>? ids = null, CancellationToken ct = default)
    {
        var q = db.PepDivergenciasIdentidade.AsNoTracking()
            .Where(d => d.FonteId == fonteId
                     && d.Status == StatusDivergenciaIdentidade.Verificada
                     && d.Veredicto == VeredictoDivergenciaIdentidade.OrigemCorreta);
        if (ids is { Count: > 0 }) q = q.Where(d => ids.Contains(d.Id));

        // Sem código conhecido não há o que direcionar (divergência vinda do CDC, que não sabe
        // o paciente) — e reprocessar "todos" seria o oposto de direcionado.
        var alvos = await q.Where(d => d.CdPaciente > 0 || d.CodigoOrigem != null)
            .Select(d => new { d.CdPaciente, d.CodigoOrigem }).Distinct().ToListAsync(ct);

        if (alvos.Count == 0)
            throw new ConflitoException("pep.sem_divergencia_reprocessavel",
                "Não há divergência com veredicto \"origem correta\" e código de paciente conhecido para reprocessar.");

        // O código de TEXTO é a forma que serve a qualquer base; o numérico fica para as
        // divergências antigas do Salux, gravadas antes de a coluna existir.
        var codigos = alvos.Where(a => a.CodigoOrigem != null).Select(a => a.CodigoOrigem!).Distinct().ToList();
        var cds = alvos.Where(a => a.CodigoOrigem == null && a.CdPaciente > 0)
            .Select(a => a.CdPaciente).Distinct().ToList();

        return await IniciarAsync(new IniciarImportacaoRequest(
            FonteId: fonteId,
            Modo: ModoSincronizacao.Incremental,
            Escopo: EscopoSincronizacao.Tudo,
            MaxMedicos: null,
            MaxPacientes: null,
            ApagarAntes: false,
            Concorrencia: null,
            CursorPacienteInicial: null,
            CdsPacientes: cds.Count > 0 ? cds : null,
            CodigosPacientes: codigos.Count > 0 ? codigos : null), ct);
    }

    public async Task<DivergenciaIdentidadeDto> IgnorarDivergenciaAsync(
        Guid id, string? motivo = null, CancellationToken ct = default)
    {
        var d = await db.PepDivergenciasIdentidade.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NaoEncontradoException("Divergência", id);

        d.Status = StatusDivergenciaIdentidade.Ignorada;
        d.ResolvidoEm = DateTime.UtcNow;
        d.ResolvidoPor = usuarioAtual.UsuarioId;
        d.AtualizadoEm = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(motivo))
            d.Detalhe = motivo.Length <= 500 ? motivo : motivo[..500];
        await db.SaveChangesAsync(ct);

        return (await ListarDivergenciasAsync(d.FonteId, null, ct)).First(x => x.Id == id);
    }

    public async Task ExecutarAsync(PepImportacaoJob job, CancellationToken ct = default)
    {
        var execucao = await db.PepSincronizacaoExecucoes.FirstOrDefaultAsync(e => e.Id == job.ExecucaoId, ct);
        if (execucao is null)
        {
            logger.LogWarning("Execução {Id} não encontrada — job ignorado.", job.ExecucaoId);
            return;
        }

        var fonte = await db.IaFontes.AsNoTracking().FirstOrDefaultAsync(f => f.Id == job.FonteId, ct);
        var estrategia = fonte is null ? null : estrategias.FirstOrDefault(e => e.Atende(fonte));

        var progresso = new ProgressoImportacao();
        execucao.Status = StatusSincronizacao.EmExecucao;
        execucao.IniciadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // CTS encadeado ao token do runner: permite o usuário parar a importação
        // (botão) independentemente do shutdown da aplicação.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tokenRun = cts.Token;
        estadoVivo.Iniciar(execucao.Id, execucao.FonteId, execucao.FonteNome, execucao.Modo, execucao.Escopo, execucao.IniciadoEm, progresso, cts);

        RegistradorFalhasPep? registrador = null;
        RegistradorDivergenciasPep? divergencias = null;
        try
        {
            if (fonte is null) throw new ValidacaoException("pep.base", "Base não encontrada.");
            if (estrategia is null) throw new ValidacaoException("pep.tipo_nao_suportado", $"Tipo '{fonte.Tipo}' não suportado.");
            // A exigência de credencial vale só para quem o servidor alcança por rede. Uma base
            // atendida por agente (ADR-0023) NÃO tem host/usuário/senha aqui de propósito — a
            // credencial mora no servidor de destino e o smsmarica nunca a vê. Cobrar os campos
            // dela seria exigir justamente o que a arquitetura evita guardar.
            if (!fonte.ViaAgente
                && (string.IsNullOrWhiteSpace(fonte.Host) || string.IsNullOrWhiteSpace(fonte.Servico)
                    || string.IsNullOrWhiteSpace(fonte.Usuario) || string.IsNullOrWhiteSpace(fonte.SenhaCifrada)))
                throw new ValidacaoException("pep.base_incompleta", $"Base '{fonte.Nome}' sem host/serviço/usuário/senha configurados.");
            if (string.IsNullOrWhiteSpace(fonte.Slug))
                throw new ValidacaoException("pep.base_sem_slug", $"Base '{fonte.Nome}' sem slug — defina no cadastro da base.");

            var estadoEntidade = await db.PepSincronizacaoEstados.FirstOrDefaultAsync(s => s.FonteId == fonte.Id, ct);
            var marca = new MarcaDagua
            {
                ProfissionalEm = estadoEntidade?.UltimoSyncProfissionalEm,
                PacienteEm = estadoEntidade?.UltimoSyncPacienteEm,
                AtendimentoEm = estadoEntidade?.UltimoSyncAtendimentoEm,
                DocumentoEm = estadoEntidade?.UltimoSyncDocumentoEm,
                InternacaoEm = estadoEntidade?.UltimoSyncInternacaoEm,
                LogDocumentoId = estadoEntidade?.UltimoSyncLogDocumentoId,
                Ponteiros = DesserializarPonteiros(estadoEntidade?.PonteirosJson),
            };

            // Trilha durável de falhas (grava na hora, sobrevive a crash; alimenta o reimport por cd).
            registrador = new RegistradorFalhasPep(dbFactory, logger, execucao.Id, fonte.Id, fonte.Slug!);

            // Divergências de identidade: sink durável + o retrato do que JÁ é conhecido desta
            // base, para o upsert saber quando congelar sem consultar o banco no caminho quente.
            divergencias = new RegistradorDivergenciasPep(dbFactory, logger, execucao.Id, fonte.Id, fonte.Slug!);
            var conhecidas = await CarregarDivergenciasConhecidasAsync(fonte.Id, ct);

            var contexto = new ContextoImportacaoPep
            {
                // Um transporte OU o outro, nunca os dois — quem alcança por rede recebe
                // credencial; quem depende de agente recebe o canal de consulta (ADR-0023).
                Conexao = fonte.ViaAgente
                    ? null
                    : new ConexaoFonte(fonte.Host!, fonte.Porta ?? 1521, fonte.Servico!, fonte.Usuario!,
                        protetor.Revelar(fonte.SenhaCifrada!), _timeoutSegundos),
                Consulta = fonte.ViaAgente
                    ? new Inteligencia.Fontes.ProxyAgenteFonte(
                        agenteRegistry, fonte.Slug!, _timeoutSegundos, _agenteMaxLinhas)
                    : null,
                Opcoes = job.Opcoes,
                Marca = marca,
                // Decorator de retry-in-place: saturação transitória do hub vira reenvio
                // com backoff (farol p.Retentativas), não falha. Nunca descarta o registro.
                Escritor = new Fhir.EscritorComRetentativa(escritor, progresso, _hubRetryBudget, logger),
                Progresso = progresso,
                BaseSlug = fonte.Slug!,
                Falhas = registrador,
                Divergencias = divergencias,
                DivergenciasConhecidas = conhecidas,
                // Persiste o cursor de retomada num contexto isolado (não interfere no
                // 'db' scoped que grava a execução). Chamado em série, um por bloco.
                SalvarCursorPaciente = (cursor, c) => SalvarCursorPacienteAsync(fonte.Id, cursor, c),
                // Persiste a marca d'água ao fim de cada fase concluída — um run que morre
                // no meio preserva o avanço das fases anteriores (D2, ADR-0024).
                SalvarMarca = (m, c) => SalvarMarcaAsync(fonte.Id, m, c),
            };

            await estrategia.ImportarAsync(contexto, tokenRun);

            // Drena o sink de divergências: o que foi detectado agora fica no banco (e já
            // congelando o campo em disputa) antes de a execução fechar.
            //
            // A ARBITRAGEM NÃO RODA AQUI. Ela é job próprio (VerificadorDivergenciasScheduler):
            // depende de um serviço externo pago e de latência imprevisível, e em 02/08 segurou
            // o fechamento de um ciclo por mais de 11 minutos. O sincronismo não pode nem
            // atrasar nem falhar por causa do fornecedor de consulta de CPF.
            await divergencias.DisposeAsync();
            divergencias = null;

            // Sucesso: persiste contadores, tempos e watermarks.
            AplicarContadores(execucao, progresso);
            execucao.TemposJson = JsonSerializer.Serialize(progresso.Tempos);
            // O detalhe é AMOSTRA (teto em ProgressoImportacao.MaxDetalheFalhas); o total real
            // vai junto — 1,4 milhão de falhas idênticas num json de execução não informam mais
            // que quinhentas, e quase derrubaram o processo por memória em 04/08.
            execucao.FalhasJson = progresso.FalhasTotal == 0 ? null
                : JsonSerializer.Serialize(new
                {
                    total = progresso.FalhasTotal,
                    amostra = progresso.Falhas.Select(f => new { cd = f.Cd, mensagem = f.Mensagem }),
                });
            execucao.Status = progresso.FalhasTotal > 0 && progresso.Pacientes == 0
                ? StatusSincronizacao.Erro
                : StatusSincronizacao.Concluido;
            execucao.FinalizadoEm = DateTime.UtcNow;
            execucao.DuracaoSegundos = (execucao.FinalizadoEm.Value - execucao.IniciadoEm).TotalSeconds;

            await PersistirWatermarkAsync(fonte.Id, marca, ct);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Parada solicitada pelo usuário (não é shutdown da aplicação).
            logger.LogInformation("Importação {Id} (base {Fonte}) interrompida pelo usuário.", execucao.Id, execucao.FonteNome);
            AplicarContadores(execucao, progresso);
            execucao.Status = StatusSincronizacao.Cancelado;
            execucao.MensagemErro = "Importação interrompida pelo usuário.";
            execucao.FinalizadoEm = DateTime.UtcNow;
            execucao.DuracaoSegundos = (execucao.FinalizadoEm.Value - execucao.IniciadoEm).TotalSeconds;
            // Watermark NÃO é persistida (run incompleto). O cursor de retomada do modo
            // Completo já foi salvo por bloco — dá pra retomar de onde parou.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Importação {Id} (base {Fonte}) falhou.", execucao.Id, execucao.FonteNome);
            AplicarContadores(execucao, progresso);
            execucao.Status = StatusSincronizacao.Erro;
            execucao.MensagemErro = ex.Message.Split('\n')[0];
            execucao.FinalizadoEm = DateTime.UtcNow;
            execucao.DuracaoSegundos = (execucao.FinalizadoEm.Value - execucao.IniciadoEm).TotalSeconds;
        }
        finally
        {
            estadoVivo.Finalizar();
            // Drena o que faltou das trilhas antes de fechar a execução (em run cancelado/com
            // erro o sink de divergências ainda não passou pela drenagem da fase de arbitragem).
            if (registrador is not null) await registrador.DisposeAsync();
            if (divergencias is not null) await divergencias.DisposeAsync();
            try
            {
                if (execucao.Disparo == DisparoSincronizacao.Agendado)
                    await AtualizarAgendaPosRunAsync(execucao, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) { logger.LogError(ex, "Falha ao salvar resultado da execução {Id}.", execucao.Id); }
        }
    }

    /// <summary>
    /// Pós-run de execução AGENDADA: falha alimenta o backoff exponencial; sucesso (ou
    /// cancelamento manual) zera. O alerta de falhas consecutivas usa marcador estável no log.
    /// </summary>
    private async Task AtualizarAgendaPosRunAsync(PepSincronizacaoExecucao execucao, CancellationToken ct)
    {
        var agenda = await db.PepSincronizacaoAgendas.FirstOrDefaultAsync(a => a.FonteId == execucao.FonteId, ct);
        if (agenda is null) return;

        var agora = DateTime.UtcNow;
        if (execucao.Status == StatusSincronizacao.Erro)
        {
            agenda.FalhasConsecutivas++;
            agenda.ProximoRunEm = Background.DecididorAgendaPep.ProximoAposErro(agenda, agora);
            var limite = configuration.GetValue("Pep:Agenda:LimiteFalhasAlerta", 5);
            if (agenda.FalhasConsecutivas >= limite)
                logger.LogError("PEP_SYNC_FALHAS_CONSECUTIVAS: base {Fonte} falhou {N} execuções agendadas seguidas — próximo run em {Proximo:u}.",
                    execucao.FonteNome, agenda.FalhasConsecutivas, agenda.ProximoRunEm);
        }
        else
        {
            agenda.FalhasConsecutivas = 0;
            agenda.ProximoRunEm = Background.DecididorAgendaPep.ProximoAposSucesso(agenda, agora);
        }
        agenda.AtualizadoEm = agora;
    }

    /// <summary>
    /// Grava o cursor de retomada (cd_paciente do último bloco) num contexto próprio,
    /// fora da transação da execução — chamado a cada bloco do modo COMPLETO.
    /// </summary>
    /// <summary>
    /// Grava a marca d'água num contexto próprio, fora da transação da execução — chamado
    /// pela estratégia ao FIM de cada fase concluída (médicos/pacientes/atendimentos).
    /// </summary>
    private async Task SalvarMarcaAsync(Guid fonteId, MarcaDagua marca, CancellationToken ct)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync(ct);
        var estado = await ctx.PepSincronizacaoEstados.FirstOrDefaultAsync(s => s.FonteId == fonteId, ct);
        if (estado is null)
        {
            estado = new PepSincronizacaoEstado { FonteId = fonteId };
            ctx.PepSincronizacaoEstados.Add(estado);
        }
        estado.UltimoSyncProfissionalEm = marca.ProfissionalEm;
        estado.UltimoSyncPacienteEm = marca.PacienteEm;
        estado.UltimoSyncAtendimentoEm = marca.AtendimentoEm;
        estado.UltimoSyncDocumentoEm = marca.DocumentoEm;
        estado.UltimoSyncInternacaoEm = marca.InternacaoEm;
        estado.UltimoSyncLogDocumentoId = marca.LogDocumentoId;
        estado.PonteirosJson = SerializarPonteiros(marca.Ponteiros);
        estado.AtualizadoEm = DateTime.UtcNow;
        await ctx.SaveChangesAsync(ct);
    }

    private async Task SalvarCursorPacienteAsync(Guid fonteId, long? cursor, CancellationToken ct)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync(ct);
        var estado = await ctx.PepSincronizacaoEstados.FirstOrDefaultAsync(s => s.FonteId == fonteId, ct);
        if (estado is null)
        {
            estado = new PepSincronizacaoEstado { FonteId = fonteId };
            ctx.PepSincronizacaoEstados.Add(estado);
        }
        estado.PacienteCursorCd = cursor;
        estado.AtualizadoEm = DateTime.UtcNow;
        await ctx.SaveChangesAsync(ct);
    }

    private async Task PersistirWatermarkAsync(Guid fonteId, MarcaDagua marca, CancellationToken ct)
    {
        var estado = await db.PepSincronizacaoEstados.FirstOrDefaultAsync(s => s.FonteId == fonteId, ct);
        if (estado is null)
        {
            estado = new PepSincronizacaoEstado { FonteId = fonteId };
            db.PepSincronizacaoEstados.Add(estado);
        }
        estado.UltimoSyncProfissionalEm = marca.ProfissionalEm;
        estado.UltimoSyncPacienteEm = marca.PacienteEm;
        estado.UltimoSyncAtendimentoEm = marca.AtendimentoEm;
        estado.UltimoSyncDocumentoEm = marca.DocumentoEm;
        estado.UltimoSyncInternacaoEm = marca.InternacaoEm;
        estado.UltimoSyncLogDocumentoId = marca.LogDocumentoId;
        estado.PonteirosJson = SerializarPonteiros(marca.Ponteiros);
        estado.AtualizadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Ponteiros de CDC numéricos (§ <see cref="MarcaDagua.Ponteiros"/>). JSON ilegível não
    /// derruba o run — vale o mesmo que "nunca ancorado", e o ciclo re-varre. Perder tempo é
    /// aceitável; parar o sincronismo por um campo auxiliar corrompido, não.
    /// </summary>
    private static Dictionary<string, long> DesserializarPonteiros(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, long>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string? SerializarPonteiros(Dictionary<string, long> ponteiros) =>
        ponteiros.Count == 0 ? null : JsonSerializer.Serialize(ponteiros);

    private static void AplicarContadores(PepSincronizacaoExecucao e, ProgressoImportacao p)
    {
        e.Medicos = p.Medicos;
        e.Pacientes = p.Pacientes;
        e.Encounters = p.Encounters;
        e.Conditions = p.Conditions;
        e.MedicationRequests = p.MedicationRequests;
        e.DocumentReferences = p.DocumentReferences;
        e.Observations = p.Observations;
        e.Falhas = p.FalhasTotal;
        e.PacientesInalterados = p.PacientesInalterados;
        e.MedicosInalterados = p.MedicosInalterados;
    }

    private static ContadoresImportacaoDto Contadores(PepSincronizacaoExecucao e) => new(
        e.Medicos, e.Pacientes, e.Encounters, e.Conditions, e.MedicationRequests, e.DocumentReferences, e.Observations, e.Falhas,
        Retentativas: 0, PacientesInalterados: e.PacientesInalterados, MedicosInalterados: e.MedicosInalterados);
}
