using Microsoft.EntityFrameworkCore;
using SMSMarica.Data;

namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Importação de agendamentos do SISREG (scraping) para <c>SolicitacaoExame</c>.
/// Fatia 1 (esta): PREVIEW — só leitura, monta o "diff" (novo vs já existe) e sinaliza
/// o que resolve/diverge. O EXECUTAR (escrita) vem na fatia 2.
/// </summary>
public interface IImportacaoSisregService
{
    Task<ImportacaoPreviewResultado> PreviewAsync(DateOnly inicio, DateOnly fim, CancellationToken ct);
}

public sealed class ImportacaoSisregService(SmsMaricaDbContext db, IMarcadosRegScraper scraper) : IImportacaoSisregService
{
    /// <summary>Nosso escopo hoje é só mamografia (ver premissas de importação).</summary>
    private const string FiltroProcedimento = "MAMOGRAF";

    public async Task<ImportacaoPreviewResultado> PreviewAsync(DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        var marcacoes = await scraper.BuscarPorPeriodoAsync(inicio, fim, FiltroProcedimento, MarcadosRegScraper.CnesPadrao, ct);

        var codigos = marcacoes.Select(m => m.CodigoSolicitacao).Distinct().ToList();
        var existentes = (await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.ExcluidoEm == null && s.CodigoSolicitacao != null && codigos.Contains(s.CodigoSolicitacao))
            .Select(s => s.CodigoSolicitacao!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var cnesSet = marcacoes
            .SelectMany(m => new[] { m.CnesUnidadeSolicitante, m.CnesUnidadeExecutante })
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct()
            .ToList();
        var unidadesComCnes = (await db.Unidades.AsNoTracking()
            .Where(u => u.Cnes != null && cnesSet.Contains(u.Cnes))
            .Select(u => u.Cnes!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var tipos = await db.TiposExame.AsNoTracking()
            .Where(t => t.ExcluidoEm == null && t.Ativo)
            .Select(t => t.Nome.ToUpper())
            .ToListAsync(ct);

        var itens = new List<ImportacaoPreviewItem>(marcacoes.Count);
        foreach (var m in marcacoes)
        {
            var jaExiste = existentes.Contains(m.CodigoSolicitacao);
            var uSol = m.CnesUnidadeSolicitante is { } cs && unidadesComCnes.Contains(cs);
            var uExe = m.CnesUnidadeExecutante is { } ce && unidadesComCnes.Contains(ce);
            var procMapeia = ProcedimentoMapeia(m.ProcedimentoTexto, tipos);

            var alertas = new List<string>();
            if (!procMapeia) alertas.Add("Procedimento sem mapeamento para tipo de exame (cairia em divergência).");
            if (string.IsNullOrWhiteSpace(m.CnsPaciente)) alertas.Add("Ficha sem CNS do paciente.");
            if (m.DataHoraAtendimento is null) alertas.Add("Sem data/hora de atendimento.");

            itens.Add(new ImportacaoPreviewItem(
                CodigoSolicitacao: m.CodigoSolicitacao,
                NomePaciente: m.NomePaciente,
                CnsPaciente: m.CnsPaciente,
                ProcedimentoTexto: m.ProcedimentoTexto,
                DataHoraAtendimento: m.DataHoraAtendimento,
                NomeUnidadeSolicitante: m.NomeUnidadeSolicitante,
                CnesUnidadeSolicitante: m.CnesUnidadeSolicitante,
                NomeUnidadeExecutante: m.NomeUnidadeExecutante,
                CnesUnidadeExecutante: m.CnesUnidadeExecutante,
                NomeMedicoSolicitante: m.NomeMedicoSolicitante,
                JaExiste: jaExiste,
                UnidadeSolicitanteExiste: uSol,
                UnidadeExecutanteExiste: uExe,
                ProcedimentoMapeia: procMapeia,
                Alertas: alertas));
        }

        // Ordena: novos primeiro, depois por data.
        itens = [.. itens.OrderBy(i => i.JaExiste).ThenBy(i => i.DataHoraAtendimento)];
        var novos = itens.Count(i => !i.JaExiste);
        return new ImportacaoPreviewResultado(inicio, fim, itens.Count, novos, itens.Count - novos, itens);
    }

    private static bool ProcedimentoMapeia(string? procedimento, IReadOnlyList<string> tiposUpper)
    {
        if (string.IsNullOrWhiteSpace(procedimento)) return false;
        var p = procedimento.Trim().ToUpperInvariant();
        return tiposUpper.Any(t => t.Contains(p, StringComparison.Ordinal) || p.Contains(t, StringComparison.Ordinal));
    }
}
