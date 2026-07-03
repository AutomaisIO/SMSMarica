using System.Globalization;
using Microsoft.Extensions.Logging;

namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Varre "Agendados pela Regulação" (<c>cons_marcados_reg</c>) por período e enriquece
/// cada marcação com a ficha detalhe. Só leitura. A unidade (CNES) é a do operador logado
/// (solicitante); default CDT <c>3132358</c>.
/// </summary>
public interface IMarcadosRegScraper
{
    Task<IReadOnlyList<MarcacaoSisreg>> BuscarPorPeriodoAsync(
        DateOnly inicio, DateOnly fim, string? filtroProcedimento, string cnesUnidade, CancellationToken ct);
}

public sealed class MarcadosRegScraper(ISisregWebSessao sessao, ILogger<MarcadosRegScraper> logger) : IMarcadosRegScraper
{
    private const string Caminho = "/cgi-bin/cons_marcados_reg";
    public const string CnesPadrao = "3132358";

    public async Task<IReadOnlyList<MarcacaoSisreg>> BuscarPorPeriodoAsync(
        DateOnly inicio, DateOnly fim, string? filtroProcedimento, string cnesUnidade, CancellationToken ct)
    {
        var di = inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var df = fim.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var filtro = (filtroProcedimento ?? string.Empty).Trim().ToUpperInvariant();

        var html0 = await ListarPaginaAsync(cnesUnidade, di, df, 0, ct);
        var pag0 = MarcadosRegParser.ParseListagem(html0);
        logger.LogInformation("SISREG import: {Total} registros em {Paginas} páginas ({Ini}..{Fim})",
            pag0.TotalRegistros, pag0.TotalPaginas, di, df);

        // Coleta as linhas de todas as páginas (dedup por código).
        var linhas = new Dictionary<string, MarcadosRegParser.LinhaListagem>();
        void Acumular(MarcadosRegParser.Listagem l)
        {
            foreach (var ln in l.Linhas)
            {
                if (filtro.Length > 0 && !ln.Procedimento.ToUpperInvariant().Contains(filtro)) continue;
                linhas.TryAdd(ln.CodigoSolicitacao, ln);
            }
        }
        Acumular(pag0);
        for (var p = 1; p < pag0.TotalPaginas; p++)
        {
            ct.ThrowIfCancellationRequested();
            var html = await ListarPaginaAsync(cnesUnidade, di, df, p, ct);
            Acumular(MarcadosRegParser.ParseListagem(html));
        }

        // Enriquece cada uma com a ficha detalhe.
        var resultado = new List<MarcacaoSisreg>(linhas.Count);
        foreach (var ln in linhas.Values)
        {
            ct.ThrowIfCancellationRequested();
            var htmlFicha = await sessao.PostFormAsync(Caminho, new Dictionary<string, string>
            {
                ["etapa"] = "EXIBIR_FICHA",
                ["co_solicitacao"] = ln.CodigoSolicitacao,
                ["unidade"] = cnesUnidade,
            }, ct);

            var ficha = MarcadosRegParser.ParseFicha(htmlFicha, ln.CodigoSolicitacao);
            var dataHora = ParseDataHoraListagem(ln.DataExecucao, ln.HoraExecucao) ?? ficha?.DataHoraAtendimento;

            resultado.Add((ficha ?? MarcacaoVazia(ln.CodigoSolicitacao)) with
            {
                ProcedimentoTexto = ln.Procedimento,
                NomeUnidadeExecutante = ficha?.NomeUnidadeExecutante ?? ln.UnidadeExecutante,
                DataHoraAtendimento = dataHora,
            });
        }
        return resultado;
    }

    private Task<string> ListarPaginaAsync(string cnes, string di, string df, int pagina, CancellationToken ct) =>
        sessao.PostFormAsync(Caminho, new Dictionary<string, string>
        {
            ["etapa"] = "LISTAR_SOLICITACOES",
            ["cns_paciente"] = string.Empty,
            ["unidade"] = cnes,
            ["cod_procedimento"] = string.Empty,
            ["ds_procedimento"] = string.Empty,
            ["tp_periodo"] = "exe",
            ["dt_inicial"] = di,
            ["dt_final"] = df,
            ["ordenacao"] = string.Empty,
            ["pagina"] = pagina.ToString(CultureInfo.InvariantCulture),
            ["co_solicitacao"] = string.Empty,
        }, ct);

    private static MarcacaoSisreg MarcacaoVazia(string codigo) =>
        new(codigo, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

    private static DateTime? ParseDataHoraListagem(string data, string hora)
    {
        var d = (data ?? string.Empty).Trim();
        var h = (hora ?? string.Empty).Trim();
        if (h.Length >= 5) h = h[..5]; // "09:00:00" -> "09:00"
        return DateTime.TryParseExact($"{d} {h}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Unspecified)
            : null;
    }
}
