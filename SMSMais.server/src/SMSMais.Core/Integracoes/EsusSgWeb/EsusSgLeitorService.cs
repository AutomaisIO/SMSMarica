using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Core.Integracoes.EsusSgWeb;

/// <summary>Resultado de uma leitura paginada.
/// <para><b>Semântica medida (30/09/2026):</b> o "total" do ESUS conta registros ÚNICOS
/// (agendamentos distintos), mas o offset/limite conta linhas BRUTAS — e a lista de agendados
/// repete linhas (junção interna do ESUS: 2019 veio com 1.405 brutas para 1.352 declaradas).
/// Por isso <see cref="Completa"/> compara os ÚNICOS com o declarado, e a paginação vai até uma
/// página vir incompleta, nunca até "offset ≥ total" (isso perdeu 3 agendamentos de mar/2019).</para></summary>
public sealed record LeituraEsusSg<T>(IReadOnlyList<T> Linhas, int Declarado, int Recebidas, int Requisicoes)
{
    public bool Completa => Linhas.Count == Declarado;
}

/// <summary>
/// Lê as telas do ESUS SG que a conta de Maricá enxerga, com os <b>payloads exatos do front</b>
/// (medidos em 30/09/2026, copiados de <c>Automais.esus_saocongalo/esus/telas.py</c>).
///
/// <para>Chave desconhecida no <c>arrFormData</c> não dá erro no legado — dá 0 linhas, que parece
/// "não há dado". Por isso nada aqui é montado de cabeça.</para>
/// </summary>
public interface IEsusSgLeitorService
{
    Task<LeituraEsusSg<EsusSgLinhaFila>> LerFilaAsync(TipoRecursoEsusSg tipo, CancellationToken cancellationToken);

    /// <summary>Agendados cuja DATA DO AGENDAMENTO cai em [de, ate]. Sem período o ESUS devolve 0.</summary>
    Task<LeituraEsusSg<EsusSgLinhaAgendado>> LerAgendadosAsync(
        TipoRecursoEsusSg tipo, DateOnly de, DateOnly ate, CancellationToken cancellationToken);

    /// <summary>Procedimentos que a unidade pode pedir ao SG (combo "reguláveis por solicitante").</summary>
    Task<IReadOnlyList<EsusSgRecursoCatalogo>> LerCatalogoExameAsync(CancellationToken cancellationToken);
}

public sealed class EsusSgLeitorService(IEsusSgSessao sessao, ILogger<EsusSgLeitorService> logger)
    : IEsusSgLeitorService
{
    /// <summary>Página grande: a fila inteira (648) vem numa requisição de 1,4 s e o ano de 2019
    /// inteiro de agendados (1.405 linhas) em 4,3 s — menos requisições e nenhuma fronteira de
    /// página para escorregar.</summary>
    private const int TamanhoPagina = 1000;

    /// <summary>Teto de segurança da paginação (1000 × 50 = 50 mil linhas por leitura).</summary>
    private const int MaximoPaginas = 50;

    private const string CaminhoFilaConsulta = "consultas/controller-fila-consulta/buscar";
    private const string CaminhoFilaExame = "exames2/controller-fila-exame/buscar";
    private const string CaminhoAgendadosConsulta = "consultas/controller-paciente-agendado-fila-consulta/buscar";
    private const string CaminhoAgendadosExame = "exames2/controller-paciente-agendado-fila-exames/buscar";
    private const string CaminhoCatalogoExame = "exames2/controller-fila-exame/combo-box-procedimentos-regulaveis-por-solicitante";

    /// <summary>Permissões de tela que a busca de agendados manda junto (smo_id).</summary>
    private const int SmoAgendadosConsulta = 447;
    private const int SmoAgendadosExame = 449;

    public async Task<LeituraEsusSg<EsusSgLinhaFila>> LerFilaAsync(
        TipoRecursoEsusSg tipo, CancellationToken cancellationToken)
    {
        var unidade = await sessao.UnidadeAsync(cancellationToken);
        var exame = tipo == TipoRecursoEsusSg.Exame;
        var caminho = exame ? CaminhoFilaExame : CaminhoFilaConsulta;
        return await PaginarAsync(caminho, (ini, fim) => FormFila(unidade, exame, ini, fim), EsusSgLinhaFila.De,
            l => l.IdEsusSg, cancellationToken);
    }

    public async Task<LeituraEsusSg<EsusSgLinhaAgendado>> LerAgendadosAsync(
        TipoRecursoEsusSg tipo, DateOnly de, DateOnly ate, CancellationToken cancellationToken)
    {
        var unidade = await sessao.UnidadeAsync(cancellationToken);
        var exame = tipo == TipoRecursoEsusSg.Exame;
        var caminho = exame ? CaminhoAgendadosExame : CaminhoAgendadosConsulta;
        // Chave do AGENDAMENTO (não do pedido): pedido com várias sessões fica com todas; só a linha
        // idêntica repetida pelo ESUS é descartada.
        return await PaginarAsync(caminho, (ini, fim) => FormAgendados(unidade, exame, de, ate, ini, fim),
            EsusSgLinhaAgendado.De, l => $"{l.IdEsusSg}|{l.AgendamentoIdEsus}|{l.DataHoraAgendadaTexto}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<EsusSgRecursoCatalogo>> LerCatalogoExameAsync(CancellationToken cancellationToken)
    {
        var unidade = await sessao.UnidadeAsync(cancellationToken);
        var dados = await sessao.PostarLegadoAsync(
            CaminhoCatalogoExame, new JsonObject { ["idUnidadeSolicitante"] = unidade }, cancellationToken);

        var lista = new List<EsusSgRecursoCatalogo>();
        if (dados.ValueKind != JsonValueKind.Array) return lista;
        foreach (var item in dados.EnumerateArray())
        {
            var valor = EsusSgJson.Texto(item, "data");
            var rotulo = EsusSgJson.Texto(item, "nome") ?? EsusSgJson.Texto(item, "label");
            if (valor is not null && rotulo is not null) lista.Add(new EsusSgRecursoCatalogo(valor, rotulo));
        }
        return lista;
    }

    // ------------------------------------------------------------------ paginação

    private async Task<LeituraEsusSg<T>> PaginarAsync<T>(
        string caminho,
        Func<int, int, JsonObject> montar,
        Func<JsonElement, T> mapear,
        Func<T, string> chave,
        CancellationToken cancellationToken)
    {
        var vistos = new Dictionary<string, T>();
        var declarado = 0;
        var recebidas = 0;
        var requisicoes = 0;
        for (var inicio = 0; ; inicio += TamanhoPagina)
        {
            var dados = await sessao.PostarLegadoAsync(caminho, montar(inicio, TamanhoPagina), cancellationToken);
            requisicoes++;
            var (linhas, total) = LinhasETotal(dados, caminho);
            declarado = total;
            recebidas += linhas.Count;
            foreach (var bruta in linhas)
            {
                try
                {
                    var l = mapear(bruta);
                    vistos.TryAdd(chave(l), l);
                }
                catch (FormatException ex)
                {
                    logger.LogWarning("ESUS SG {Caminho}: linha ignorada ({Motivo}).", caminho, ex.Message);
                }
            }
            // Para quando a página vem INCOMPLETA (acabou), não quando offset ≥ total: o total conta
            // únicos e o offset conta brutas com repetição.
            if (linhas.Count < TamanhoPagina || requisicoes >= MaximoPaginas) break;
        }

        if (vistos.Count != declarado)
        {
            logger.LogWarning(
                "ESUS SG {Caminho}: {Unicos} únicos ({Recebido} linhas) ≠ declarado {Declarado}.",
                caminho, vistos.Count, recebidas, declarado);
        }
        return new LeituraEsusSg<T>(vistos.Values.ToList(), declarado, recebidas, requisicoes);
    }

    /// <summary>As duas formas medidas de busca paginada do legado:
    /// fila = <c>[[linhas...], "total"]</c>; agendados = <c>{"recordSet": [...], "total": "N"}</c>.
    /// Forma desconhecida é ERRO — devolver vazio escondeu 61 agendados na primeira medição.</summary>
    public static (List<JsonElement> Linhas, int Total) LinhasETotal(JsonElement dados, string caminho)
    {
        if (dados.ValueKind == JsonValueKind.Array && dados.GetArrayLength() == 2
            && dados[0].ValueKind == JsonValueKind.Array)
        {
            return (dados[0].EnumerateArray().ToList(), Total(dados[1]));
        }
        if (dados.ValueKind == JsonValueKind.Object
            && dados.TryGetProperty("recordSet", out var rs) && rs.ValueKind == JsonValueKind.Array)
        {
            return (rs.EnumerateArray().ToList(),
                dados.TryGetProperty("total", out var t) ? Total(t) : rs.GetArrayLength());
        }
        throw new FormatException($"ESUS SG {caminho}: forma de resposta paginada desconhecida ({dados.ValueKind}).");
    }

    private static int Total(JsonElement t) => t.ValueKind switch
    {
        JsonValueKind.Number => t.GetInt32(),
        JsonValueKind.String when int.TryParse(t.GetString(), out var i) => i,
        _ => 0,
    };

    // ------------------------------------------------------------------ payloads (copiados do front)

    /// <summary>Fila de Regulação vista pela unidade atual. Na de EXAME o front manda
    /// <c>uns_id=&lt;unidade&gt;</c> + <c>permissaoUnidadeSolicitante=0</c>; na de CONSULTA manda
    /// <c>uns_id=null</c> + <c>permissaoUnidadeSolicitante=1</c>. Copiado como veio.</summary>
    internal static JsonObject FormFila(int unidade, bool exame, int inicio, int tamanho)
    {
        var f = new JsonObject();
        if (exame)
        {
            f["fle_nome_procedimento"] = "TODOS";
            f["esu_nome_exames_procedimentos_filho"] = "";
        }
        f["uns_id"] = exame ? unidade : null;
        f["uns_atual"] = unidade;
        f["pfi_id"] = null;
        f["situacao"] = null;
        f["pendencia"] = null;
        f["requestingProfessionalId"] = null;
        f["est_id"] = null;
        f["mun_id"] = null;
        f["bai_nome"] = "";
        f["periodoInicial"] = "";
        f["periodoInicialFila"] = "";
        f["periodoFinal"] = "";
        f["periodoFinalFila"] = "";
        f["permissaoRegular"] = 0;
        f["permissaoUnidadeSolicitante"] = exame ? 0 : 1;
        f["somenteRegulados"] = false;
        if (exame) f["rgb_agendado"] = null;
        f["pes_id"] = null;
        f["pes_nome"] = null;
        f["minAge"] = 0;
        f["maxAge"] = 0;
        f["minAgeType"] = null;
        f["maxAgeType"] = null;
        f["orderbyAge"] = null;
        f["orderbyDate"] = null;
        f["limiteInicio"] = inicio;
        f["limiteFim"] = tamanho;
        return Envelope(f);
    }

    /// <summary>Pacientes Agendados pela Fila — o período é a DATA DO AGENDAMENTO, em
    /// <c>dd/mm/aaaa</c> (o que a máscara da tela manda).</summary>
    internal static JsonObject FormAgendados(int unidade, bool exame, DateOnly de, DateOnly ate, int inicio, int tamanho)
    {
        var f = new JsonObject
        {
            ["pfi_id"] = null,
            ["set_id"] = null,
            ["lca_id"] = null,
            ["ocp_id"] = null,
            ["usu_id_agendamento"] = 0,
            ["pes_id"] = null,
            ["pes_base"] = null,
            ["periodoFinal"] = ate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["periodoInicial"] = de.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["periodoFilaFinal"] = "",
            ["periodoFilaInicial"] = "",
            ["periodoCadastroFilaFinal"] = "",
            ["periodoCadastroFilaInicial"] = "",
            ["fun_id_solicitante"] = 0,
            ["fun_id"] = 0,
            ["uns_id"] = null,
            ["uns_id_destino"] = null,
            ["uns_solicitante"] = unidade,
            ["fil_id_agendado_por"] = 0,
            ["fil_comprovante_impresso"] = 2, // 2 = impresso e não impresso
            ["smo_id"] = exame ? SmoAgendadosExame : SmoAgendadosConsulta,
            ["ilt_id"] = null,
            ["not_resposta"] = "",
            ["not_resposta_extensao"] = "",
            ["limiteInicio"] = inicio,
            ["limiteFim"] = tamanho,
        };
        return Envelope(f);
    }

    private static JsonObject Envelope(JsonObject form) => new()
    {
        ["arrFormData"] = form,
        ["toPrint"] = false,
        ["toCsv"] = false,
        ["toExcel"] = false,
    };
}
