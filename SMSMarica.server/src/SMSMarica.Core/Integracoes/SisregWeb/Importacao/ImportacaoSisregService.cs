using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Data;

namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Importação de agendamentos do SISREG para <c>SolicitacaoExame</c>, a partir do
/// "Arquivo Agendamento (TXT)" (<c>expo_solicitacoes</c>).
/// Fatia 1 (esta): PREVIEW — só leitura, monta o "diff" (novo vs já existe) e sinaliza o
/// que resolve/diverge. O EXECUTAR (escrita) vem na fatia 2.
/// </summary>
public interface IImportacaoSisregService
{
    /// <summary>Preview a partir do conteúdo de um TXT enviado (upload).</summary>
    Task<ImportacaoPreviewResultado> PreviewDeTextoAsync(string conteudoTxt, CancellationToken ct);
}

public sealed class ImportacaoSisregService(SmsMaricaDbContext db) : IImportacaoSisregService
{
    public async Task<ImportacaoPreviewResultado> PreviewDeTextoAsync(string conteudoTxt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(conteudoTxt))
            throw new ValidacaoException("importacao.arquivo_vazio", "Arquivo vazio ou ilegível.");

        var parsed = AgendaTxtParser.Parse(conteudoTxt);
        if (parsed.Marcacoes.Count == 0)
            throw new ValidacaoException(
                "importacao.sem_registros",
                "Não encontrei marcações no arquivo. Confirme que é o TXT de Arquivo Agendamento do SISREG.");

        var inicio = parsed.Cabecalho.Inicio ?? parsed.Marcacoes.Min(m => m.DataHoraAtendimento)?.ToDateOnly() ?? default;
        var fim = parsed.Cabecalho.Fim ?? parsed.Marcacoes.Max(m => m.DataHoraAtendimento)?.ToDateOnly() ?? default;
        return await MontarPreviewAsync(parsed.Marcacoes, inicio, fim, ct);
    }

    private async Task<ImportacaoPreviewResultado> MontarPreviewAsync(
        IReadOnlyList<MarcacaoSisreg> marcacoes, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        var codigos = marcacoes.Select(m => m.CodigoSolicitacao).Distinct().ToList();
        var existentes = (await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.ExcluidoEm == null && s.CodigoSolicitacao != null && codigos.Contains(s.CodigoSolicitacao))
            .Select(s => s.CodigoSolicitacao!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var cnesSet = marcacoes
            .SelectMany(m => new[] { m.CnesUnidadeSolicitante, m.CnesUnidadeExecutante })
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        var unidadesComCnes = (await db.Unidades.AsNoTracking()
            .Where(u => u.Cnes != null && cnesSet.Contains(u.Cnes))
            .Select(u => u.Cnes!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        // Códigos SIGTAP (só dígitos) que têm um tipo de exame ativo mapeado — o TXT já traz o SIGTAP.
        var sigtapComTipo = (await db.TiposExame.AsNoTracking()
            .Where(t => t.ExcluidoEm == null && t.Ativo && t.ProcedimentoSigtap != null)
            .Select(t => t.ProcedimentoSigtap!.Codigo)
            .ToListAsync(ct))
            .Select(c => new string([.. c.Where(char.IsDigit)]))
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var itens = new List<ImportacaoPreviewItem>(marcacoes.Count);
        foreach (var m in marcacoes)
        {
            var jaExiste = existentes.Contains(m.CodigoSolicitacao);
            var uSol = m.CnesUnidadeSolicitante is { } cs && unidadesComCnes.Contains(cs);
            var uExe = m.CnesUnidadeExecutante is { } ce && unidadesComCnes.Contains(ce);
            var procMapeia = m.CodigoSigtap is { } sig && sigtapComTipo.Contains(sig);

            var alertas = new List<string>();
            if (!procMapeia) alertas.Add("Procedimento/SIGTAP sem tipo de exame mapeado (cairia em divergência).");
            if (string.IsNullOrWhiteSpace(m.CnsPaciente)) alertas.Add("Sem CNS do paciente.");
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

        itens = [.. itens.OrderBy(i => i.JaExiste).ThenBy(i => i.DataHoraAtendimento)];
        var novos = itens.Count(i => !i.JaExiste);
        return new ImportacaoPreviewResultado(inicio, fim, itens.Count, novos, itens.Count - novos, itens);
    }
}

internal static class DataHoraExtensions
{
    public static DateOnly ToDateOnly(this DateTime dt) => DateOnly.FromDateTime(dt);
}
