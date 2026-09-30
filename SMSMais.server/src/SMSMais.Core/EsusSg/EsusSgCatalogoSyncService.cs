using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Data;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Core.EsusSg;

public sealed record EsusSgCatalogoSyncResultado(int Lidos, int Novos, int Alterados, int Inativados)
{
    public bool Mudou => Novos + Alterados + Inativados > 0;
}

/// <summary>
/// Espelha o catálogo do ESUS SG — o combo "procedimentos reguláveis por solicitante" (14 de exame
/// medidos) — em <c>esussg_catalogo_recurso</c> e, quando algo muda, leva ao catálogo canônico
/// (ADR-0055). É uma requisição só; roda no começo de cada varredura, não precisa de fila própria.
/// Sem isto, os pedidos do SG não teriam procedimento canônico e as regras não os alcançariam.
/// </summary>
public interface IEsusSgCatalogoSyncService
{
    Task<EsusSgCatalogoSyncResultado> SincronizarAsync(CancellationToken cancellationToken);
}

public sealed class EsusSgCatalogoSyncService(
    SmsMaisDbContext db,
    IEsusSgLeitorService leitor,
    IRegulacaoCatalogoService catalogo,
    ILogger<EsusSgCatalogoSyncService> logger) : IEsusSgCatalogoSyncService
{
    public async Task<EsusSgCatalogoSyncResultado> SincronizarAsync(CancellationToken cancellationToken)
    {
        var lidos = await leitor.LerCatalogoExameAsync(cancellationToken);
        if (lidos.Count == 0)
        {
            // Lista vazia é mais provável falha de leitura do que o SG ter tirado tudo de Maricá.
            // Não inativa nada com base nisso.
            logger.LogWarning("ESUS SG: o combo de procedimentos veio vazio — catálogo mantido como estava.");
            return new EsusSgCatalogoSyncResultado(0, 0, 0, 0);
        }

        var existentes = await db.EsusSgCatalogoRecursos
            .Where(r => r.Tipo == TipoRecursoEsusSg.Exame)
            .ToDictionaryAsync(r => r.Valor, StringComparer.Ordinal, cancellationToken);

        var agora = DateTime.UtcNow;
        int novos = 0, alterados = 0, inativados = 0;
        var vivos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in lidos)
        {
            vivos.Add(r.Valor);
            if (!existentes.TryGetValue(r.Valor, out var atual))
            {
                db.EsusSgCatalogoRecursos.Add(new EsusSgCatalogoRecurso
                {
                    Id = Guid.NewGuid(),
                    Tipo = TipoRecursoEsusSg.Exame,
                    Valor = r.Valor,
                    Rotulo = r.Rotulo.Length <= 300 ? r.Rotulo : r.Rotulo[..300],
                    Ativo = true,
                    SincronizadoEm = agora,
                });
                novos++;
                continue;
            }

            if (atual.Rotulo != r.Rotulo || !atual.Ativo)
            {
                atual.Rotulo = r.Rotulo.Length <= 300 ? r.Rotulo : r.Rotulo[..300];
                atual.Ativo = true;
                alterados++;
            }
            atual.SincronizadoEm = agora;
        }

        foreach (var sumido in existentes.Values.Where(e => e.Ativo && !vivos.Contains(e.Valor)))
        {
            sumido.Ativo = false;
            sumido.SincronizadoEm = agora;
            inativados++;
        }

        await db.SaveChangesAsync(cancellationToken);
        var resultado = new EsusSgCatalogoSyncResultado(lidos.Count, novos, alterados, inativados);

        if (resultado.Mudou)
        {
            logger.LogInformation(
                "ESUS SG: catálogo mudou ({Novos} novos, {Alterados} alterados, {Inativados} inativados); "
                + "levando ao catálogo canônico.", novos, alterados, inativados);
            try
            {
                await catalogo.SincronizarAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // O espelho do ESUS está gravado; o canônico tenta de novo na próxima mudança ou
                // pelo botão "Sincronizar catálogo" da Configuração.
                logger.LogWarning(ex, "ESUS SG: catálogo canônico não sincronizou agora.");
            }
        }
        return resultado;
    }
}
