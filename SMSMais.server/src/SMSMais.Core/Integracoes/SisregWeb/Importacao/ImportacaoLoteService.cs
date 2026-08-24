using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao;

/// <summary>Um arquivo já lido do upload (ou de dentro do zip), pronto pra ir ao lote.</summary>
public sealed record ArquivoRecebido(string NomeArquivo, string Conteudo, string? CaminhoNoZip);

public interface IImportacaoLoteService
{
    /// <summary>Enfileira o lote e devolve o LoteId. Os arquivos JÁ vêm filtrados por extensão.</summary>
    Task<Guid> IniciarAsync(IReadOnlyList<ArquivoRecebido> arquivos, CancellationToken ct);

    /// <summary>Roda o lote inteiro, arquivo a arquivo. Chamado só pelo runner.</summary>
    Task ExecutarAsync(SisregImportacaoJob job, CancellationToken ct);

    Task<StatusLote?> ObterStatusAsync(CancellationToken ct);

    /// <summary>Para o lote em andamento. False = não havia nada rodando.</summary>
    bool Cancelar();

    /// <summary>Aba de rastreio: uma linha por arquivo importado.</summary>
    Task<IReadOnlyList<ImportacaoExecucaoDto>> ListarExecucoesAsync(int limite, CancellationToken ct);
}

public sealed class ImportacaoLoteService(
    SmsMaisDbContext db,
    IDbContextFactory<SmsMaisDbContext> dbFactory,
    ISisregImportacaoFila fila,
    SisregImportacaoEstadoVivo estadoVivo,
    IImportacaoSisregService importacao,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<ImportacaoLoteService> logger) : IImportacaoLoteService
{
    public async Task<Guid> IniciarAsync(IReadOnlyList<ArquivoRecebido> arquivos, CancellationToken ct)
    {
        if (arquivos.Count == 0)
            throw new ValidacaoException("importacao.sem_arquivos",
                "Nenhum arquivo .txt ou .csv para importar.");

        if (estadoVivo.ObterAtual() is not null)
            throw new ConflitoException("importacao.em_andamento",
                "Já há uma importação em andamento. Aguarde terminar ou pare a atual.");

        await LimparOrfasAsync(ct);

        // Quem/onde tem que ser capturado AQUI: no runner não há request, e a unidade executante É
        // o contexto do operador.
        var usuarioId = usuarioAtual.UsuarioId;
        var unidadeId = usuarioAtual.UnidadeAtivaId;
        var nomeUsuario = await NomeDoUsuarioAsync(usuarioId, ct);

        var loteId = Guid.CreateVersion7();
        var agora = DateTime.UtcNow;
        var jobs = new List<SisregArquivoJob>(arquivos.Count);

        foreach (var a in arquivos)
        {
            var exec = new SisregImportacaoExecucao
            {
                Id = Guid.CreateVersion7(),
                LoteId = loteId,
                NomeArquivo = Truncar(a.NomeArquivo, 300),
                CaminhoNoZip = Truncar(a.CaminhoNoZip, 500),
                Status = StatusImportacaoArquivo.Pendente,
                IniciadoEm = agora,
                CriadoPor = usuarioId,
                CriadoPorNome = Truncar(nomeUsuario, 200),
                UnidadeExecutanteId = unidadeId,
            };
            db.SisregImportacaoExecucoes.Add(exec);
            jobs.Add(new SisregArquivoJob(exec.Id, a.NomeArquivo, a.Conteudo, usuarioId, unidadeId));
        }

        await db.SaveChangesAsync(ct);

        if (!fila.TentarEnfileirar(new SisregImportacaoJob(loteId, jobs)))
            throw new ConflitoException("importacao.fila_cheia",
                "Já há uma importação na fila. Aguarde terminar.");

        return loteId;
    }

    public async Task ExecutarAsync(SisregImportacaoJob job, CancellationToken ct)
    {
        var progresso = new ProgressoLote { LoteId = job.LoteId, TotalArquivos = job.Arquivos.Count };

        // Linked: separa "o operador parou" de "a aplicação está caindo" — o catch lá embaixo
        // discrimina os dois e grava status diferente.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        estadoVivo.Iniciar(progresso, cts);

        try
        {
            foreach (var arq in job.Arquivos)
            {
                cts.Token.ThrowIfCancellationRequested();
                progresso.ArquivoAtual = arq.NomeArquivo;

                // O contexto do operador viaja no job — sem isto, a unidade executante não resolve.
                importacao.DefinirContextoDeBackground(arq.UsuarioId, arq.UnidadeAtivaId);

                await MarcarAsync(arq.ExecucaoId, e => e.Status = StatusImportacaoArquivo.EmExecucao, ct);
                try
                {
                    var r = await importacao.ImportarArquivoAsync(arq.ExecucaoId, arq.NomeArquivo, arq.Conteudo, cts.Token);

                    progresso.Validos += r.Validos;
                    progresso.Invalidos += r.Invalidos;
                    await MarcarAsync(arq.ExecucaoId, e =>
                    {
                        e.Status = r.Incompativel ? StatusImportacaoArquivo.ArquivoIncompativel : StatusImportacaoArquivo.Concluida;
                        e.TotalRegistros = r.Total;
                        e.Validos = r.Validos;
                        e.Invalidos = r.Invalidos;
                        e.JaExistiam = r.JaExistiam;
                        e.Mensagem = r.Motivo;
                        e.ConcluidoEm = DateTime.UtcNow;
                    }, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // Um arquivo ruim não pode derrubar o lote inteiro.
                    logger.LogError(ex, "Erro importando o arquivo {Arquivo} do lote {LoteId}.", arq.NomeArquivo, job.LoteId);
                    await MarcarAsync(arq.ExecucaoId, e =>
                    {
                        e.Status = StatusImportacaoArquivo.Erro;
                        e.Mensagem = Truncar(ex.Message, 2000);
                        e.ConcluidoEm = DateTime.UtcNow;
                    }, ct);
                }

                progresso.ArquivosFeitos++;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Parada pedida pelo operador.
            await MarcarPendentesDoLoteAsync(job.LoteId, StatusImportacaoArquivo.Cancelada, "Importação parada pelo operador.", ct);
        }
        catch (OperationCanceledException)
        {
            // A aplicação está encerrando: o que não rodou vira órfão e é limpo no próximo início.
            logger.LogWarning("Lote {LoteId} interrompido pelo desligamento da aplicação.", job.LoteId);
        }
        finally
        {
            estadoVivo.Finalizar();
        }
    }

    public async Task<StatusLote?> ObterStatusAsync(CancellationToken ct)
    {
        // A verdade do "está rodando" é a MEMÓRIA — o banco não sabe se o processo morreu.
        if (estadoVivo.ObterAtual() is { } vivo) return vivo;

        var ultimo = await db.SisregImportacaoExecucoes.AsNoTracking()
            .OrderByDescending(e => e.IniciadoEm)
            .Select(e => new { e.LoteId })
            .FirstOrDefaultAsync(ct);
        if (ultimo is null) return null;

        var doLote = await db.SisregImportacaoExecucoes.AsNoTracking()
            .Where(e => e.LoteId == ultimo.LoteId)
            .Select(e => new { e.Validos, e.Invalidos })
            .ToListAsync(ct);

        return new StatusLote(ultimo.LoteId, false, doLote.Count, doLote.Count, null,
            doLote.Sum(x => x.Validos), doLote.Sum(x => x.Invalidos));
    }

    public bool Cancelar() => estadoVivo.Cancelar();

    public async Task<IReadOnlyList<ImportacaoExecucaoDto>> ListarExecucoesAsync(int limite, CancellationToken ct)
    {
        var q = db.SisregImportacaoExecucoes.AsNoTracking();
        if (usuarioAtual.UnidadeAtivaId is { } uid) q = q.Where(e => e.UnidadeExecutanteId == uid);

        return await q
            .OrderByDescending(e => e.IniciadoEm)
            .Take(Math.Clamp(limite, 1, 500))
            .Select(e => new ImportacaoExecucaoDto(
                e.Id, e.LoteId, e.NomeArquivo, e.CaminhoNoZip, e.Status, e.TotalRegistros,
                e.Validos, e.Invalidos, e.JaExistiam, e.Mensagem, e.IniciadoEm, e.ConcluidoEm,
                e.CriadoPorNome))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Execuções que ficaram Pendente/EmExecucao de um processo que morreu. Sem esta limpeza, um
    /// restart no meio de um lote deixaria linhas eternamente "em execução" no rastreio.
    /// </summary>
    private async Task LimparOrfasAsync(CancellationToken ct)
    {
        var orfas = await db.SisregImportacaoExecucoes
            .Where(e => e.Status == StatusImportacaoArquivo.Pendente || e.Status == StatusImportacaoArquivo.EmExecucao)
            .ToListAsync(ct);
        if (orfas.Count == 0) return;

        foreach (var o in orfas)
        {
            o.Status = StatusImportacaoArquivo.Erro;
            o.Mensagem ??= "Importação interrompida (a aplicação reiniciou no meio).";
            o.ConcluidoEm ??= DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task MarcarPendentesDoLoteAsync(Guid loteId, StatusImportacaoArquivo status, string msg, CancellationToken ct)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync(ct);
        var pendentes = await ctx.SisregImportacaoExecucoes
            .Where(e => e.LoteId == loteId &&
                        (e.Status == StatusImportacaoArquivo.Pendente || e.Status == StatusImportacaoArquivo.EmExecucao))
            .ToListAsync(ct);
        foreach (var p in pendentes)
        {
            p.Status = status;
            p.Mensagem ??= msg;
            p.ConcluidoEm ??= DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Context próprio: o <c>db</c> do escopo está sendo usado pela importação em si (que cria
    /// paciente/unidade/solicitação). Escrever o progresso por ele misturaria estado de tracking
    /// com a transação da linha em curso.
    /// </summary>
    private async Task MarcarAsync(Guid execucaoId, Action<SisregImportacaoExecucao> mutar, CancellationToken ct)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync(ct);
        var e = await ctx.SisregImportacaoExecucoes.FirstOrDefaultAsync(x => x.Id == execucaoId, ct);
        if (e is null) return;
        mutar(e);
        await ctx.SaveChangesAsync(ct);
    }

    private async Task<string?> NomeDoUsuarioAsync(Guid? usuarioId, CancellationToken ct) =>
        usuarioId is not { } id
            ? null
            : await db.Usuarios.AsNoTracking().Where(u => u.Id == id)
                .Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct);

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(s))]
    private static string? Truncar(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
