using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Exames;
using SMSMais.Core.Institucional;
using SMSMais.Core.Midias;
using SMSMais.Core.Regulacao.Indicadores.Dtos;
using SMSMais.Data;

namespace SMSMais.Core.Regulacao.Indicadores;

/// <summary>
/// Indicadores de Regulação por sistema (SISREG, SER, SERNIT, ESUS de São Gonçalo): série mensal com selo de
/// origem em cada número. Mesmas definições do relatório em PDF de 30/09/2026
/// (docs/regulacao/relatorio-2025-2026) — a tela reproduz o relatório.
/// </summary>
public interface IIndicadoresRegulacaoService
{
    /// <summary>Recorte em meses fechados. Sem período: os 12 meses fechados até o mês anterior (Brasília).</summary>
    Task<IndicadoresRegulacaoDto> ObterAsync(
        FonteIndicadorRegulacao fonte, DateOnly? primeiroMes, DateOnly? ultimoMes, CancellationToken ct = default);

    /// <summary>Os mesmos indicadores em PDF (A4 paisagem, com a identidade visual da instituição).</summary>
    Task<byte[]> PdfAsync(
        FonteIndicadorRegulacao fonte, DateOnly? primeiroMes, DateOnly? ultimoMes, CancellationToken ct = default);
}

public sealed class IndicadoresRegulacaoService(
    SmsMaisDbContext db,
    IMemoryCache cache,
    IInstituicaoService instituicao,
    IMidiasService midias) : IIndicadoresRegulacaoService
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(10);

    public async Task<IndicadoresRegulacaoDto> ObterAsync(
        FonteIndicadorRegulacao fonte, DateOnly? primeiroMes, DateOnly? ultimoMes, CancellationToken ct = default)
    {
        var periodo = Periodo(primeiroMes, ultimoMes);
        var chave = $"indicadores-regulacao:{fonte}:{periodo.Meses[0]}:{periodo.Meses[^1]}";
        if (cache.TryGetValue(chave, out IndicadoresRegulacaoDto? pronto) && pronto is not null) return pronto;

        var agora = DateTime.UtcNow;
        var resultado = await ComConexaoAsync(sql => fonte switch
        {
            FonteIndicadorRegulacao.Sisreg => IndicadoresSisregCalculo.CalcularAsync(sql, periodo, agora, ct),
            FonteIndicadorRegulacao.Ser or FonteIndicadorRegulacao.Sernit => IndicadoresExternoCalculo.CalcularAsync(sql, periodo, fonte, agora, ct),
            FonteIndicadorRegulacao.EsusSg => IndicadoresEsusSgCalculo.CalcularAsync(sql, periodo, agora, ct),
            _ => throw new ValidacaoException("fonte", "Sistema de regulação desconhecido."),
        }, ct);
        cache.Set(chave, resultado, Validade);
        return resultado;
    }

    public async Task<byte[]> PdfAsync(
        FonteIndicadorRegulacao fonte, DateOnly? primeiroMes, DateOnly? ultimoMes, CancellationToken ct = default)
    {
        var dados = await ObterAsync(fonte, primeiroMes, ultimoMes, ct);
        var idv = await IdentidadeVisualPdf.ResolverAsync(instituicao, midias, ct);
        return new IndicadoresRegulacaoPdf(dados, idv).Gerar();
    }

    /// <summary>Valida e monta o recorte. Mês final nunca passa do mês anterior ao atual (mês aberto engana).</summary>
    public static PeriodoIndicadores Periodo(DateOnly? primeiroMes, DateOnly? ultimoMes)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var ultimoFechado = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-1);
        if (primeiroMes is null != ultimoMes is null)
            throw new ValidacaoException("periodo", "Informe o mês inicial e o mês final, ou nenhum dos dois.");

        var fim = ultimoMes is { } u ? new DateOnly(u.Year, u.Month, 1) : ultimoFechado;
        var ini = primeiroMes is { } pm ? new DateOnly(pm.Year, pm.Month, 1) : fim.AddMonths(-11);
        if (ini > fim)
            throw new ValidacaoException("periodo", "O mês inicial é posterior ao mês final.");
        if (fim > ultimoFechado)
            throw new ValidacaoException("fim", "O mês final precisa estar fechado (no máximo o mês anterior ao atual).");
        var meses = (fim.Year - ini.Year) * 12 + fim.Month - ini.Month + 1;
        if (meses > PeriodoIndicadores.MaximoMeses)
            throw new ValidacaoException("periodo", $"O período vai até {PeriodoIndicadores.MaximoMeses} meses.");
        return PeriodoIndicadores.De(ini, fim);
    }

    private async Task<T> ComConexaoAsync<T>(Func<Sql, Task<T>> acao, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        var abriu = conn.State != ConnectionState.Open;
        if (abriu) await conn.OpenAsync(ct);
        try
        {
            // Só leitura: a transação existe para o statement_timeout e as tabelas temporárias.
            await using var tx = await conn.BeginTransactionAsync(ct);
            await using (var cmd = new NpgsqlCommand("set local statement_timeout = '120s'", conn, tx))
                await cmd.ExecuteNonQueryAsync(ct);
            var r = await acao(new Sql(conn, tx));
            await tx.RollbackAsync(ct);
            return r;
        }
        finally
        {
            if (abriu) await conn.CloseAsync();
        }
    }
}
