using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities.Ouvidoria;

namespace SMSMais.Core.Ouvidoria;

/// <summary>
/// Seed idempotente do catálogo mínimo da ouvidoria (§1.6 do plano): os 22 assuntos do Manual
/// de Ouvidoria do SUS (MS 2014) e a configuração singleton com os defaults de D-4. Roda sob
/// demanda no service — <b>nunca</b> em migration (ADR-0043: migration é imutável e igual em
/// toda instância). Nenhum ponto de resposta é semeado: isso é cadastro do ouvidor.
/// </summary>
internal static class OuvidoriaCatalogoBase
{
    /// <summary>Os 22 assuntos principais do Manual MS 2014 (referencias/02 §A.5), na ordem do manual.</summary>
    internal static readonly string[] Assuntos =
    [
        "Alimento",
        "Assistência à Saúde",
        "Assistência Farmacêutica",
        "Assistência Odontológica",
        "Assuntos Não Pertinentes",
        "Cartão SUS",
        "Comunicação",
        "Conselho de Saúde",
        "ESF/PACS",
        "Financeiro",
        "Gestão",
        "Orientações em Saúde",
        "Ouvidoria do SUS",
        "Produtos para Saúde/Correlatos",
        "Programa Farmácia Popular",
        "Farmácia Popular – Copagamento",
        "PNCT (tabagismo)",
        "DST/AIDS",
        "SAMU",
        "Transporte",
        "Vigilância em Saúde",
        "Vigilância Sanitária",
    ];

    /// <summary>Cria os assuntos de 1º nível que ainda não existem (comparação por nome, sem pai).</summary>
    internal static async Task GarantirAssuntosAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        var existentes = await db.OuvidoriaAssuntos.AsNoTracking()
            .Where(a => a.PaiId == null)
            .Select(a => a.Nome)
            .ToListAsync(ct);

        if (existentes.Count >= Assuntos.Length) return;

        var faltantes = Assuntos
            .Select((nome, i) => (nome, ordem: i + 1))
            .Where(x => !existentes.Contains(x.nome, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (faltantes.Count == 0) return;

        foreach (var (nome, ordem) in faltantes)
        {
            db.OuvidoriaAssuntos.Add(new OuvidoriaAssunto
            {
                Id = Guid.CreateVersion7(),
                PaiId = null,
                Nome = nome,
                CodigoOuvidorSus = null,
                Ordem = ordem,
                Ativo = true,
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Duas requisições semearam ao mesmo tempo: o índice único (pai_id, nome) barrou a
            // segunda. Não é erro — o catálogo está lá. Solta o que ficou pendente no tracker.
            foreach (var entrada in db.ChangeTracker.Entries<OuvidoriaAssunto>().Where(e => e.State == EntityState.Added).ToList())
            {
                entrada.State = EntityState.Detached;
            }
        }
    }

    /// <summary>Devolve a configuração singleton (rastreada), criando-a com os defaults se não existir.</summary>
    internal static async Task<OuvidoriaConfiguracao> GarantirConfiguracaoAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        var config = await db.OuvidoriaConfiguracoes.FirstOrDefaultAsync(c => c.Id == OuvidoriaConfiguracao.IdSingleton, ct);
        if (config is not null) return config;

        config = new OuvidoriaConfiguracao { Id = OuvidoriaConfiguracao.IdSingleton };
        db.OuvidoriaConfiguracoes.Add(config);
        try
        {
            await db.SaveChangesAsync(ct);
            return config;
        }
        catch (DbUpdateException)
        {
            // Corrida na criação do singleton: outra requisição venceu. Lê o que ficou.
            db.Entry(config).State = EntityState.Detached;
            return await db.OuvidoriaConfiguracoes.FirstAsync(c => c.Id == OuvidoriaConfiguracao.IdSingleton, ct);
        }
    }
}
