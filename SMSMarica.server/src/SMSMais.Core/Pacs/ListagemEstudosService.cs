using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Pacs;

/// <summary>
/// Listagem de estudos do PACS com o recorte que só o NOSSO banco sabe fazer.
///
/// <para>O QIDO responde bem sobre o que está no DICOM (nome, data, modalidade, AE de origem) e
/// nada sobre o que é nosso: de qual unidade é o pedido, qual procedimento do SISREG foi pedido.
/// A primeira tentativa foi traduzir unidade → AE de origem e empurrar tudo para o dcm4chee — o
/// que funciona, exceto para o estudo <b>reescrito</b>: toda associação manual reescreve o objeto
/// DICOM e o re-armazena por STOW-RS, que não tem AE chamador, e o dcm4chee grava a tag vazia.
/// Como o QIDO não expressa "AE no conjunto OU AE ausente", o recorte tinha de sair de lá.</para>
///
/// <para>Aqui ele sai daqui, sobre uma dicotomia limpa: <b>ou o estudo é conhecido</b> (tem
/// exame/associação, e então sabemos unidade e tipo — inclusive se foi reescrito), <b>ou é
/// órfão</b> (não tem pedido casado, logo não tem unidade e flutua para todo mundo até alguém
/// associar). O filtro é "órfão OU unidade no meu escopo".</para>
///
/// <para>Como isso não cabe numa query do QIDO, a paginação passa a ser NOSSA: varremos o PACS em
/// blocos na ordem que ele já devolve (mais novo primeiro), filtramos cada bloco contra o banco e
/// acumulamos até completar a página pedida. Custa over-fetch, limitado por
/// <see cref="TetoVarredura"/> — e na prática o primeiro bloco basta, porque quem abre a tela
/// costuma ser a unidade que executa a maior parte do que está lá.</para>
/// </summary>
public interface IListagemEstudosService
{
    Task<PaginaEstudosDto> ListarAsync(FiltroListagemEstudos filtro, CancellationToken cancellationToken = default);
}

/// <param name="QueryPacs">Query string do QIDO como o cliente montou, já sem os parâmetros que
/// são nossos (limite, offset, tipos) e sem os internos do visualizador.</param>
/// <param name="Limite">Tamanho da página pedida.</param>
/// <param name="Offset">Quantas linhas JÁ FILTRADAS pular — não é o offset do PACS.</param>
/// <param name="TipoExameIds">Tipos marcados. Vazio = todos. Com filtro de tipo, o órfão sai da
/// lista por definição: ele não tem pedido, logo não tem tipo.</param>
public sealed record FiltroListagemEstudos(
    string QueryPacs,
    int Limite,
    int Offset,
    IReadOnlyList<Guid> TipoExameIds);

/// <param name="Estudos">Datasets DICOM-JSON, como vieram do dcm4chee — o front já sabe mapeá-los.</param>
/// <param name="OrfaosOcultos">Órfãos descartados NESTA varredura pelo filtro de tipo. A tela avisa,
/// senão o exame que acabou de chegar sumiria sem explicação.</param>
/// <param name="Truncado">A varredura bateu no teto antes de completar a página.</param>
public sealed record PaginaEstudosDto(
    IReadOnlyList<JsonElement> Estudos,
    int OrfaosOcultos,
    bool Truncado);

internal sealed class ListagemEstudosService(
    SmsMaisDbContext db,
    IPacsProxyService pacs,
    IEscopoEstudosPacs escopoPacs,
    IUsuarioAtualAccessor usuarioAtual) : IListagemEstudosService
{
    /// <summary>Estudos pedidos ao PACS por vez. Igual ao do ConsultaStudyClient.</summary>
    private const int TamanhoBloco = 100;

    /// <summary>Teto de estudos varridos numa chamada — trava de segurança do over-fetch.</summary>
    private const int TetoVarredura = 3000;

    public async Task<PaginaEstudosDto> ListarAsync(
        FiltroListagemEstudos filtro, CancellationToken cancellationToken = default)
    {
        var limite = Math.Clamp(filtro.Limite, 1, 200);
        var pular = Math.Max(0, filtro.Offset);
        var tipos = filtro.TipoExameIds ?? [];

        var escopo = await ResolverEscopoUnidadesAsync(cancellationToken);
        if (escopo is { Restrito: true, Unidades.Length: 0 }) return new PaginaEstudosDto([], 0, false);

        var pagina = new List<JsonElement>(limite);
        var orfaosOcultos = 0;
        var varridos = 0;
        var offsetPacs = 0;
        var truncado = false;

        while (pagina.Count < limite)
        {
            if (varridos >= TetoVarredura)
            {
                truncado = true;
                break;
            }

            var bloco = await BuscarBlocoAsync(filtro.QueryPacs, offsetPacs, cancellationToken);
            if (bloco.Count == 0) break;

            offsetPacs += bloco.Count;
            varridos += bloco.Count;

            var contexto = await ResolverContextoAsync(bloco.Select(UidDoEstudo), cancellationToken);

            foreach (var estudo in bloco)
            {
                var uid = UidDoEstudo(estudo);
                if (uid.Length == 0) continue;

                var decisao = RecorteEstudo.Decidir(
                    contexto.GetValueOrDefault(uid), escopo.Restrito, escopo.Unidades, tipos);

                if (decisao == DecisaoRecorte.OrfaoOcultoPorTipo) { orfaosOcultos++; continue; }
                if (decisao == DecisaoRecorte.Descarta) continue;

                if (pular > 0) { pular--; continue; }

                pagina.Add(estudo);
                if (pagina.Count == limite) break;
            }

            if (bloco.Count < TamanhoBloco) break; // página curta do PACS = acabou o acervo
        }

        return new PaginaEstudosDto(pagina, orfaosOcultos, truncado);
    }

    /// <summary>
    /// Escopo em UNIDADES (não em AE). Reaproveita a mesma flag do recorte por AE: enquanto ela
    /// estiver desligada, a listagem não recorta nada — é o interruptor único da funcionalidade.
    /// </summary>
    private async Task<(bool Restrito, Guid[] Unidades)> ResolverEscopoUnidadesAsync(CancellationToken ct)
    {
        // O IEscopoEstudosPacs já carrega a flag e o fail-closed; aqui só precisamos saber SE
        // recorta. Sem restrição (flag off, acesso global, job sem usuário) => passa tudo.
        var porAe = await escopoPacs.ResolverAsync(ct);
        if (porAe.SemRestricao) return (false, []);

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        if (escopo.VeTudo) return (false, []);
        return (true, escopo.SemAcesso ? [] : escopo.Unidades);
    }

    private static string UidDoEstudo(JsonElement estudo)
    {
        if (estudo.ValueKind != JsonValueKind.Object) return string.Empty;
        if (!estudo.TryGetProperty("0020000D", out var campo)) return string.Empty;
        if (!campo.TryGetProperty("Value", out var valor)
            || valor.ValueKind != JsonValueKind.Array || valor.GetArrayLength() == 0) return string.Empty;
        return valor[0].GetString()?.Trim() ?? string.Empty;
    }

    private async Task<IReadOnlyList<JsonElement>> BuscarBlocoAsync(
        string queryPacs, int offset, CancellationToken ct)
    {
        var query = MontarQueryBloco(queryPacs, offset);
        using var resposta = await pacs.EncaminharAsync(
            HttpMethod.Get, "studies", query, "application/dicom+json", ct);

        // 204 = sem match (fim legítimo da varredura). Erro do PACS não vira lista vazia
        // silenciosa: a tela precisa distinguir "acabou" de "PACS fora do ar".
        if (resposta.StatusCode == System.Net.HttpStatusCode.NoContent) return [];
        resposta.EnsureSuccessStatusCode();

        await using var stream = await resposta.Content.ReadAsStreamAsync(ct);
        var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
        return json.ValueKind == JsonValueKind.Array ? [.. json.EnumerateArray()] : [];
    }

    /// <summary>Reaproveita o filtro do cliente trocando só a janela (limit/offset) pelo bloco.</summary>
    private static string MontarQueryBloco(string queryPacs, int offset)
    {
        var q = queryPacs ?? string.Empty;
        if (q.StartsWith('?')) q = q[1..];

        var pares = q.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(par =>
            {
                var chave = par.Split('=', 2)[0];
                return !chave.Equals("limit", StringComparison.OrdinalIgnoreCase)
                    && !chave.Equals("offset", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        pares.Add($"limit={TamanhoBloco}");
        pares.Add($"offset={offset}");
        return "?" + string.Join('&', pares);
    }

    /// <summary>Unidade e tipo de cada estudo conhecido — explícito (associação) ou implícito (worklist).</summary>
    private async Task<Dictionary<string, ContextoEstudo>> ResolverContextoAsync(
        IEnumerable<string> studyUids, CancellationToken ct)
    {
        var uids = studyUids.Where(u => u.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var mapa = new Dictionary<string, ContextoEstudo>(StringComparer.Ordinal);
        if (uids.Length == 0) return mapa;

        var explicitas = await db.ExameAssociacoes.AsNoTracking()
            .Where(a => uids.Contains(a.StudyInstanceUID) && a.ExcluidoEm == null)
            .Select(a => new
            {
                a.StudyInstanceUID,
                Exame = db.ExamesImagem
                    .Where(e => e.Id == a.ExameImagemId)
                    .Select(e => new
                    {
                        e.TipoExameId,
                        e.Solicitacao!.UnidadeExecutanteId,
                        e.Solicitacao!.UnidadeSolicitanteId,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        foreach (var a in explicitas)
        {
            if (a.Exame is null) continue;
            mapa[a.StudyInstanceUID] = new ContextoEstudo(
                a.Exame.UnidadeExecutanteId, a.Exame.UnidadeSolicitanteId, a.Exame.TipoExameId);
        }

        var faltam = uids.Where(u => !mapa.ContainsKey(u)).ToArray();
        if (faltam.Length == 0) return mapa;

        var implicitas = await db.ExamesImagem.AsNoTracking()
            .Where(e => faltam.Contains(e.StudyInstanceUID) && e.ExcluidoEm == null
                        && e.Status != StatusSolicitacaoExame.Cancelada)
            .Select(e => new
            {
                e.StudyInstanceUID,
                e.TipoExameId,
                e.Solicitacao!.UnidadeExecutanteId,
                e.Solicitacao!.UnidadeSolicitanteId,
            })
            .ToListAsync(ct);

        foreach (var i in implicitas)
        {
            mapa[i.StudyInstanceUID] = new ContextoEstudo(
                i.UnidadeExecutanteId, i.UnidadeSolicitanteId, i.TipoExameId);
        }

        return mapa;
    }
}
