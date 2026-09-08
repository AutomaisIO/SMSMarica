using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Worklist;

/// <param name="ParesEncontrados">Pares (unidade × tipo) que existem no histórico de exames.</param>
/// <param name="Criados">Quantos viraram linha nova de escopo.</param>
/// <param name="JaExistiam">Quantos já estavam lá — o que torna a rotina repetível sem medo.</param>
/// <param name="ComEnvioLigado">Dos criados, quantos nasceram enviando (herdando a flag do tipo).</param>
public sealed record ResultadoBackfillEscopo(
    int ParesEncontrados, int Criados, int JaExistiam, int ComEnvioLigado);

/// <summary>
/// Popula <see cref="TipoExameUnidade"/> a partir do que as unidades <b>já executaram</b>, herdando
/// a flag global do tipo. É o que faz o dia seguinte ao deploy ser idêntico ao anterior: sem isto,
/// a régua nova (sem escopo ⇒ não envia) cortaria todo mundo de uma vez.
///
/// <para><b>Por que não é migration.</b> Migration é imutável e roda igual em instância nova, onde
/// não há histórico do qual derivar — o backfill precisa ser repetível e olhar dados que só existem
/// nesta instância. Fica como rotina administrativa idempotente, chamada pelo endpoint.</para>
///
/// <para>Medido em 08/09/2026: <b>298</b> pares reais, dos quais 62 em unidades com equipamento.</para>
/// </summary>
public interface IBackfillEscopoExameUnidade
{
    Task<ResultadoBackfillEscopo> ExecutarAsync(bool simular, CancellationToken cancellationToken = default);
}

internal sealed class BackfillEscopoExameUnidade(
    SmsMaisDbContext db,
    ILogger<BackfillEscopoExameUnidade> logger) : IBackfillEscopoExameUnidade
{
    public async Task<ResultadoBackfillEscopo> ExecutarAsync(
        bool simular, CancellationToken cancellationToken = default)
    {
        // Os pares que a operação criou de fato, com a flag do tipo junto: é ela que cada par herda,
        // para o comportamento não mudar no cutover.
        var pares = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.TipoExameId != null && e.ExcluidoEm == null)
            .Join(db.Solicitacoes.AsNoTracking(),
                  e => e.SolicitacaoId, s => s.Id,
                  (e, s) => new { TipoExameId = e.TipoExameId!.Value, s.UnidadeExecutanteId })
            .Distinct()
            .Join(db.TiposExame.AsNoTracking(),
                  p => p.TipoExameId, t => t.Id,
                  (p, t) => new { p.TipoExameId, p.UnidadeExecutanteId, t.EnviarParaWorklist })
            .ToListAsync(cancellationToken);

        var existentes = await db.TiposExameUnidade.AsNoTracking()
            .Where(a => a.ExcluidoEm == null)
            .Select(a => new { a.TipoExameId, a.UnidadeId })
            .ToListAsync(cancellationToken);

        var jaTem = existentes
            .Select(x => (x.TipoExameId, x.UnidadeId))
            .ToHashSet();

        var novos = new List<TipoExameUnidade>();
        var agora = DateTime.UtcNow;

        foreach (var par in pares)
        {
            if (jaTem.Contains((par.TipoExameId, par.UnidadeExecutanteId))) continue;

            novos.Add(new TipoExameUnidade
            {
                Id = Guid.CreateVersion7(),
                TipoExameId = par.TipoExameId,
                UnidadeId = par.UnidadeExecutanteId,
                EnviarParaWorklist = par.EnviarParaWorklist,
                EquipamentoId = null,
                Ativo = true,
                CriadoEm = agora,
            });
            // Guarda para o caso de o mesmo par aparecer duas vezes na lista.
            jaTem.Add((par.TipoExameId, par.UnidadeExecutanteId));
        }

        var resultado = new ResultadoBackfillEscopo(
            ParesEncontrados: pares.Count,
            Criados: novos.Count,
            JaExistiam: pares.Count - novos.Count,
            ComEnvioLigado: novos.Count(n => n.EnviarParaWorklist));

        if (simular)
        {
            logger.LogInformation(
                "BACKFILL_ESCOPO (simulação): {Pares} pares, {Criados} seriam criados ({Ligados} enviando).",
                resultado.ParesEncontrados, resultado.Criados, resultado.ComEnvioLigado);
            return resultado;
        }

        if (novos.Count > 0)
        {
            db.TiposExameUnidade.AddRange(novos);
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "BACKFILL_ESCOPO: {Pares} pares, {Criados} criados ({Ligados} enviando), {Existiam} já existiam.",
            resultado.ParesEncontrados, resultado.Criados, resultado.ComEnvioLigado, resultado.JaExistiam);

        return resultado;
    }
}
