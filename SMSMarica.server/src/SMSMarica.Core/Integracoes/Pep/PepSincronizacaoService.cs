using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes.Pep.Background;
using SMSMarica.Core.Integracoes.Pep.Dtos;
using SMSMarica.Core.Integracoes.Pep.Estrategias;
using SMSMarica.Core.Integracoes.Pep.Falhas;
using SMSMarica.Core.Integracoes.Pep.Fhir;
using SMSMarica.Core.Integracoes.Pep.Progresso;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Pep;

namespace SMSMarica.Core.Integracoes.Pep;

public sealed class PepSincronizacaoService(
    SmsMaricaDbContext db,
    IDbContextFactory<SmsMaricaDbContext> dbFactory,
    IProtetorSegredos protetor,
    IEnumerable<IEstrategiaImportacaoPep> estrategias,
    IHubFhirEscritor escritor,
    IPepSincronizacaoFila fila,
    PepSincronizacaoEstadoVivo estadoVivo,
    IUsuarioAtualAccessor usuarioAtual,
    IConfiguration configuration,
    ILogger<PepSincronizacaoService> logger) : IPepSincronizacaoService
{
    private readonly int _timeoutSegundos = configuration.GetValue("Pep:TimeoutSegundos", 120);

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

        var tiposSuportados = estrategias.Select(e => e.Tipo).ToHashSet();

        return fontes.Select(f => new BasePepDto(
            f.Id, f.Nome, f.Tipo.ToString(), f.Ambiente.ToString(),
            tiposSuportados.Contains(f.Tipo),
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

        if (estrategias.All(e => e.Tipo != fonte.Tipo))
            throw new ValidacaoException("pep.tipo_nao_suportado",
                $"Importação ainda não suportada para o tipo de PEP '{fonte.Tipo}'.");

        if (string.IsNullOrWhiteSpace(fonte.Slug))
            throw new ValidacaoException("pep.base_sem_slug",
                $"Base '{fonte.Nome}' sem slug. Defina um slug curto e estável no cadastro da base (ex.: 'salux-hcml') antes de importar — ele identifica a origem e evita colisão entre hospitais.");

        if (request.Escopo == EscopoSincronizacao.Limitado &&
            request is { MaxMedicos: null or <= 0, MaxPacientes: null or <= 0 })
            throw new ValidacaoException("pep.limites",
                "No escopo Limitado informe ao menos um limite (médicos e/ou pacientes) maior que zero.");

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
            request.ApagarAntes, Concorrencia: request.Concorrencia, CursorPacienteInicial: cursorInicial);
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

    public Task CancelarAsync(CancellationToken ct = default)
    {
        if (estadoVivo.ObterAtual() is null)
            throw new ConflitoException("pep.sem_importacao", "Não há importação em andamento para parar.");

        if (!estadoVivo.Cancelar())
            throw new ConflitoException("pep.cancelamento_indisponivel",
                "Não foi possível solicitar o cancelamento — a importação pode já ter finalizado.");

        logger.LogInformation("Cancelamento de importação solicitado por {Usuario}.", usuarioAtual.UsuarioId);
        return Task.CompletedTask;
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
            e.IniciadoEm, e.FinalizadoEm, e.DuracaoSegundos, Contadores(e), e.TemposJson, e.MensagemErro)).ToList();
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

    public async Task ExecutarAsync(PepImportacaoJob job, CancellationToken ct = default)
    {
        var execucao = await db.PepSincronizacaoExecucoes.FirstOrDefaultAsync(e => e.Id == job.ExecucaoId, ct);
        if (execucao is null)
        {
            logger.LogWarning("Execução {Id} não encontrada — job ignorado.", job.ExecucaoId);
            return;
        }

        var fonte = await db.IaFontes.AsNoTracking().FirstOrDefaultAsync(f => f.Id == job.FonteId, ct);
        var estrategia = fonte is null ? null : estrategias.FirstOrDefault(e => e.Tipo == fonte.Tipo);

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
        try
        {
            if (fonte is null) throw new ValidacaoException("pep.base", "Base não encontrada.");
            if (estrategia is null) throw new ValidacaoException("pep.tipo_nao_suportado", $"Tipo '{fonte.Tipo}' não suportado.");
            if (string.IsNullOrWhiteSpace(fonte.Host) || string.IsNullOrWhiteSpace(fonte.Servico)
                || string.IsNullOrWhiteSpace(fonte.Usuario) || string.IsNullOrWhiteSpace(fonte.SenhaCifrada))
                throw new ValidacaoException("pep.base_incompleta", $"Base '{fonte.Nome}' sem host/serviço/usuário/senha configurados.");
            if (string.IsNullOrWhiteSpace(fonte.Slug))
                throw new ValidacaoException("pep.base_sem_slug", $"Base '{fonte.Nome}' sem slug — defina no cadastro da base.");

            var estadoEntidade = await db.PepSincronizacaoEstados.FirstOrDefaultAsync(s => s.FonteId == fonte.Id, ct);
            var marca = new MarcaDagua
            {
                MedicoEm = estadoEntidade?.UltimoSyncMedicoEm,
                PacienteEm = estadoEntidade?.UltimoSyncPacienteEm,
                BaaEm = estadoEntidade?.UltimoSyncBaaEm,
                EdocEm = estadoEntidade?.UltimoSyncEdocEm,
            };

            // Trilha durável de falhas (grava na hora, sobrevive a crash; alimenta o reimport por cd).
            registrador = new RegistradorFalhasPep(dbFactory, logger, execucao.Id, fonte.Id, fonte.Slug!);

            var contexto = new ContextoImportacaoPep
            {
                Conexao = new ConexaoFonte(fonte.Host!, fonte.Porta ?? 1521, fonte.Servico!, fonte.Usuario!,
                    protetor.Revelar(fonte.SenhaCifrada!), _timeoutSegundos),
                Opcoes = job.Opcoes,
                Marca = marca,
                // Decorator de retry-in-place: saturação transitória do hub vira reenvio
                // com backoff (farol p.Retentativas), não falha. Nunca descarta o registro.
                Escritor = new Fhir.EscritorComRetentativa(escritor, progresso, _hubRetryBudget, logger),
                Progresso = progresso,
                BaseSlug = fonte.Slug!,
                Falhas = registrador,
                // Persiste o cursor de retomada num contexto isolado (não interfere no
                // 'db' scoped que grava a execução). Chamado em série, um por bloco.
                SalvarCursorPaciente = (cursor, c) => SalvarCursorPacienteAsync(fonte.Id, cursor, c),
            };

            await estrategia.ImportarAsync(contexto, tokenRun);

            // Sucesso: persiste contadores, tempos e watermarks.
            AplicarContadores(execucao, progresso);
            execucao.TemposJson = JsonSerializer.Serialize(progresso.Tempos);
            execucao.FalhasJson = progresso.Falhas.Count == 0 ? null
                : JsonSerializer.Serialize(progresso.Falhas.Select(f => new { cd = f.Cd, mensagem = f.Mensagem }));
            execucao.Status = progresso.Falhas.Count > 0 && progresso.Pacientes == 0
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
            // Drena o que faltou da trilha de falhas antes de fechar a execução.
            if (registrador is not null) await registrador.DisposeAsync();
            try { await db.SaveChangesAsync(ct); }
            catch (Exception ex) { logger.LogError(ex, "Falha ao salvar resultado da execução {Id}.", execucao.Id); }
        }
    }

    /// <summary>
    /// Grava o cursor de retomada (cd_paciente do último bloco) num contexto próprio,
    /// fora da transação da execução — chamado a cada bloco do modo COMPLETO.
    /// </summary>
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
        estado.UltimoSyncMedicoEm = marca.MedicoEm;
        estado.UltimoSyncPacienteEm = marca.PacienteEm;
        estado.UltimoSyncBaaEm = marca.BaaEm;
        estado.UltimoSyncEdocEm = marca.EdocEm;
        estado.AtualizadoEm = DateTime.UtcNow;
    }

    private static void AplicarContadores(PepSincronizacaoExecucao e, ProgressoImportacao p)
    {
        e.Medicos = p.Medicos;
        e.Pacientes = p.Pacientes;
        e.Encounters = p.Encounters;
        e.Conditions = p.Conditions;
        e.MedicationRequests = p.MedicationRequests;
        e.DocumentReferences = p.DocumentReferences;
        e.Observations = p.Observations;
        e.Falhas = p.Falhas.Count;
    }

    private static ContadoresImportacaoDto Contadores(PepSincronizacaoExecucao e) => new(
        e.Medicos, e.Pacientes, e.Encounters, e.Conditions, e.MedicationRequests, e.DocumentReferences, e.Observations, e.Falhas);
}
