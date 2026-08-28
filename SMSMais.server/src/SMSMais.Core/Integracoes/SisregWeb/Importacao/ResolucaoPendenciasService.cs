using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao;

/// <summary>O que o clique em "Resolver todas" devolve na hora: o lote entrou na fila.</summary>
public sealed record ResolucaoPendenciasAceitaDto(Guid ExecucaoId, int Total);

/// <summary>
/// "Resolver todas as pendências" — o mesmo "Validar" de uma linha, aplicado à fila inteira.
///
/// <para><b>Por que em background e não no loop da request:</b> cada pendência custa uma ida ao
/// CADSUS, e a fila real tem centenas de linhas. Resolver 260 numa request seria minutos de HTTP
/// aberto, morto no primeiro timeout — e o operador teria de ficar com a aba aberta rezando.</para>
///
/// <para><b>Por que não reusa a fila do lote de arquivos:</b> são trabalhos diferentes na mesma
/// tela. Compartilhar o estado vivo faria "importei arquivos" e "resolvi pendências" aparecerem um
/// no lugar do outro, e um bloquearia o outro por acidente de implementação, não por decisão.</para>
/// </summary>
public interface IResolucaoPendenciasService
{
    /// <summary>Fotografa as pendências elegíveis, enfileira e devolve quantas entraram.</summary>
    Task<ResolucaoPendenciasAceitaDto> IniciarAsync(CancellationToken ct);

    /// <summary>Roda o lote inteiro, pendência a pendência. Chamado só pelo runner.</summary>
    Task ExecutarAsync(ResolucaoPendenciasJob job, CancellationToken ct);

    StatusResolucaoPendencias? ObterStatus();

    /// <summary>Para o lote em andamento. False = não havia nada rodando.</summary>
    bool Cancelar();
}

public sealed class ResolucaoPendenciasService(
    SmsMaisDbContext db,
    IResolucaoPendenciasFila fila,
    ResolucaoPendenciasEstadoVivo estadoVivo,
    IImportacaoSisregService importacao,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<ResolucaoPendenciasService> logger) : IResolucaoPendenciasService
{
    /// <summary>
    /// As causas que um replay tem chance real de resolver.
    ///
    /// <para>Ficam de fora, e não por descuido: <c>SemCns</c> e <c>LinhaInvalida</c> não têm o dado
    /// na origem (nenhuma tentativa muda isso); <c>ArquivoIncompativel</c> nem linha do SISREG é;
    /// e <c>SigtapNaoMapeado</c> tem ação própria — enquanto ninguém mapear o procedimento, cada
    /// tentativa aqui só gastaria a fonte de cadastro para falhar igual.</para>
    ///
    /// <para><c>CpfNaoResolvido</c> ENTRA porque a fonte pode ter mudado: um CNS que o CADSUS do
    /// SISREG não resolveu pode voltar com CPF pela porta do SER.</para>
    /// </summary>
    private static readonly CausaFalhaImportacao[] CausasElegiveis =
    [
        CausaFalhaImportacao.CadsusIndisponivel,
        CausaFalhaImportacao.CpfNaoResolvido,
        CausaFalhaImportacao.UnidadeNaoResolvida,
        CausaFalhaImportacao.Outro,
    ];

    /// <summary>
    /// Falhas de fonte SEGUIDAS que fazem o lote desistir.
    ///
    /// <para>Quando o SISREG dispara o CAPTCHA, ele não volta atrás sozinho — só um humano no
    /// navegador destrava, e cada nova tentativa é mais uma requisição no orçamento que já
    /// estourou. Insistir 260 vezes contra uma fonte morta piora exatamente o problema que este
    /// botão existe para resolver.</para>
    /// </summary>
    private const int FalhasDeFonteSeguidasParaDesistir = 10;

    public async Task<ResolucaoPendenciasAceitaDto> IniciarAsync(CancellationToken ct)
    {
        if (estadoVivo.EmExecucao)
        {
            throw new ConflitoException(
                "resolucao.em_andamento",
                "Já há uma resolução de pendências em andamento. Aguarde terminar ou pare a atual.");
        }

        var unidade = usuarioAtual.UnidadeAtivaId;

        // Snapshot dos IDs — ver a nota de ResolucaoPendenciasJob.
        var ids = await db.SisregImportacaoFalhas.AsNoTracking()
            .Where(f => f.ResolvidoEm == null
                        && CausasElegiveis.Contains(f.Causa)
                        && (unidade == null || f.UnidadeExecutanteId == unidade))
            // Mais antigas primeiro: são as que estão há mais tempo sem virar agendamento.
            .OrderBy(f => f.CriadoEm)
            .Select(f => f.Id)
            .ToListAsync(ct);

        if (ids.Count == 0)
        {
            throw new ValidacaoException(
                "resolucao.sem_pendencias",
                "Não há pendências que uma nova tentativa possa resolver. As que restam precisam de "
                + "ação específica (informar CPF, mapear o SIGTAP) ou não têm o dado na origem.");
        }

        var job = new ResolucaoPendenciasJob(
            Guid.CreateVersion7(), ids, usuarioAtual.UsuarioId, unidade);

        if (!fila.TentarEnfileirar(job))
        {
            throw new ConflitoException(
                "resolucao.fila_cheia", "Já há uma resolução na fila. Aguarde terminar.");
        }

        return new ResolucaoPendenciasAceitaDto(job.ExecucaoId, ids.Count);
    }

    public async Task ExecutarAsync(ResolucaoPendenciasJob job, CancellationToken ct)
    {
        var progresso = new ProgressoResolucao
        {
            ExecucaoId = job.ExecucaoId,
            Total = job.FalhaIds.Count,
            IniciadoEm = DateTime.UtcNow,
        };

        // Linked: separa "o operador parou" de "a aplicação está caindo".
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        estadoVivo.Iniciar(progresso, cts);

        // O contexto do operador viaja no job: sem ele a unidade executante não resolve e a
        // auditoria sairia sem autor.
        importacao.DefinirContextoDeBackground(job.UsuarioId, job.UnidadeAtivaId);

        var cancelado = false;
        var desistiu = false;
        var seguidasDeFonte = 0;

        try
        {
            foreach (var id in job.FalhaIds)
            {
                cts.Token.ThrowIfCancellationRequested();

                try
                {
                    var r = await importacao.ReprocessarFalhaAsync(id, cts.Token);

                    if (r.Resolvida)
                    {
                        progresso.Resolvidas++;
                        seguidasDeFonte = 0;
                    }
                    else
                    {
                        progresso.Continuam++;
                        seguidasDeFonte = r.Execucao?.Causa == CausaFalhaImportacao.CadsusIndisponivel
                            ? seguidasDeFonte + 1
                            : 0;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // Uma pendência ruim não derruba o lote — ela continua na lista, que é onde o
                    // operador a encontra de novo.
                    logger.LogError(ex, "Erro resolvendo a pendência {FalhaId} no lote {ExecucaoId}.", id, job.ExecucaoId);
                    progresso.Continuam++;
                }

                progresso.Feitas++;

                if (seguidasDeFonte >= FalhasDeFonteSeguidasParaDesistir)
                {
                    desistiu = true;
                    logger.LogWarning(
                        "Resolução {ExecucaoId} interrompida: {Qtd} falhas seguidas de consulta ao "
                        + "cadastro. A fonte está indisponível (CAPTCHA do SISREG?).",
                        job.ExecucaoId, seguidasDeFonte);
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            cancelado = true;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Resolução {ExecucaoId} interrompida pelo desligamento da aplicação.", job.ExecucaoId);
        }
        finally
        {
            estadoVivo.Finalizar(cancelado, Resumir(progresso, cancelado, desistiu));
        }
    }

    public StatusResolucaoPendencias? ObterStatus() => estadoVivo.ObterAtual();

    public bool Cancelar() => estadoVivo.Cancelar();

    private static string Resumir(ProgressoResolucao p, bool cancelado, bool desistiu)
    {
        var basico = $"{p.Resolvidas} de {p.Feitas} pendências resolvidas";
        var restam = p.Continuam > 0
            ? $"; {p.Continuam} continuam pendentes (veja o motivo na lista)"
            : string.Empty;

        if (cancelado)
            return $"Parado pelo operador — {basico}{restam}.";

        if (desistiu)
        {
            return $"Interrompido: a consulta de cadastro parou de responder — {basico}{restam}. "
                   + "Se a fonte é o SISREG, ele provavelmente passou a exigir CAPTCHA; troque a "
                   + "fonte para o SER na configuração do SISREG, ou destrave o operador no navegador.";
        }

        return p.Feitas < p.Total
            ? $"Encerrado antes do fim — {basico}{restam}."
            : $"Concluído: {basico}{restam}.";
    }
}
