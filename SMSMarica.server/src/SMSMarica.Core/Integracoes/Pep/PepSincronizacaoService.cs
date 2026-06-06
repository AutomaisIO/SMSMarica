using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes.Pep.Background;
using SMSMarica.Core.Integracoes.Pep.Dtos;
using SMSMarica.Core.Integracoes.Pep.Estrategias;
using SMSMarica.Core.Integracoes.Pep.Fhir;
using SMSMarica.Core.Integracoes.Pep.Progresso;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Pep;

namespace SMSMarica.Core.Integracoes.Pep;

public sealed class PepSincronizacaoService(
    SmsMaricaDbContext db,
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

        var tiposSuportados = estrategias.Select(e => e.Tipo).ToHashSet();

        return fontes.Select(f => new BasePepDto(
            f.Id, f.Nome, f.Tipo.ToString(), f.Ambiente.ToString(),
            tiposSuportados.Contains(f.Tipo),
            ultimas.GetValueOrDefault(f.Id))).ToList();
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

        var opcoes = new OpcoesImportacao(request.Modo, request.Escopo, request.MaxMedicos, request.MaxPacientes,
            request.ApagarAntes, Concorrencia: request.Concorrencia);
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
        estadoVivo.Iniciar(execucao.Id, execucao.FonteId, execucao.FonteNome, execucao.Modo, execucao.Escopo, execucao.IniciadoEm, progresso);

        try
        {
            if (fonte is null) throw new ValidacaoException("pep.base", "Base não encontrada.");
            if (estrategia is null) throw new ValidacaoException("pep.tipo_nao_suportado", $"Tipo '{fonte.Tipo}' não suportado.");
            if (string.IsNullOrWhiteSpace(fonte.Host) || string.IsNullOrWhiteSpace(fonte.Servico)
                || string.IsNullOrWhiteSpace(fonte.Usuario) || string.IsNullOrWhiteSpace(fonte.SenhaCifrada))
                throw new ValidacaoException("pep.base_incompleta", $"Base '{fonte.Nome}' sem host/serviço/usuário/senha configurados.");

            var estadoEntidade = await db.PepSincronizacaoEstados.FirstOrDefaultAsync(s => s.FonteId == fonte.Id, ct);
            var marca = new MarcaDagua
            {
                MedicoEm = estadoEntidade?.UltimoSyncMedicoEm,
                PacienteEm = estadoEntidade?.UltimoSyncPacienteEm,
                BaaEm = estadoEntidade?.UltimoSyncBaaEm,
                EdocEm = estadoEntidade?.UltimoSyncEdocEm,
            };

            var contexto = new ContextoImportacaoPep
            {
                Conexao = new ConexaoFonte(fonte.Host!, fonte.Porta ?? 1521, fonte.Servico!, fonte.Usuario!,
                    protetor.Revelar(fonte.SenhaCifrada!), _timeoutSegundos),
                Opcoes = job.Opcoes,
                Marca = marca,
                Escritor = escritor,
                Progresso = progresso,
            };

            await estrategia.ImportarAsync(contexto, ct);

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
            try { await db.SaveChangesAsync(ct); }
            catch (Exception ex) { logger.LogError(ex, "Falha ao salvar resultado da execução {Id}.", execucao.Id); }
        }
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
