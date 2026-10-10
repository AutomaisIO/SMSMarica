using System.Globalization;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Indicadores;

/// <summary>Um item do cursor <c>sisreg_indicador_coleta</c>, como o coletor o executa.</summary>
public sealed record ItemColeta(Guid Id, ColetorIndicadorSisreg Coletor, DateOnly Inicio, DateOnly Fim, string Escopo);

public enum DesfechoPasso
{
    /// <summary>O item precisa de mais requisições — continua no próximo tick.</summary>
    Continuar = 1,
    Concluida = 2,
    Falha = 3,
    /// <summary>O SISREG pediu CAPTCHA: parar TUDO, o operador está bloqueado.</summary>
    Captcha = 4,
    /// <summary>504/corte de conexão: consulta pesada demais para a janela.</summary>
    TempoEsgotado = 5,
    /// <summary>
    /// O SISREG recusou o login: nenhuma leitura anda, e a culpa não é do item. Pausa curta — se for a
    /// senha, insistir a cada 30 s só piora.
    /// </summary>
    LoginRecusado = 6,
}

/// <param name="Linhas">Na conclusão: o total que a janela passa a registrar (o DECLARADO pela tela, quando há).</param>
/// <param name="Unidades">Só na leitura da lista de unidades: as unidades solicitantes que viram itens.</param>
public sealed record ResultadoPasso(
    DesfechoPasso Desfecho, int? Linhas = null, string? Mensagem = null,
    IReadOnlyList<UnidadeSolicitanteSisreg>? Unidades = null)
{
    public static ResultadoPasso Continuar() => new(DesfechoPasso.Continuar);
    public static ResultadoPasso Concluida(int linhas) => new(DesfechoPasso.Concluida, linhas);
    public static ResultadoPasso Falha(string mensagem) => new(DesfechoPasso.Falha, Mensagem: mensagem);
    public static ResultadoPasso TempoEsgotado(string mensagem) => new(DesfechoPasso.TempoEsgotado, Mensagem: mensagem);
}

/// <summary>
/// A execução de UM item, um passo (= uma requisição ao SISREG) por vez. O estado entre passos mora
/// no objeto — o agendador guarda o trabalho em curso e chama o próximo passo no tick seguinte, o
/// que deixa os outros motores intercalarem e mantém o ritmo sob o teto por hora.
///
/// <para><b>Nada é gravado sem prova de leitura completa.</b> Cada tipo tem a sua (ver
/// <see cref="IndicadoresSisregHtmlParser"/>); sem ela o passo termina em falha e a janela fica como
/// estava. As substituições passam pela <see cref="SubstituicaoDeJanela"/>.</para>
/// </summary>
public abstract class TrabalhoColeta(ItemColeta item)
{
    public ItemColeta Item { get; } = item;

    /// <summary>Descrição curta para a tela ("faltas 01/09–08/09/2026").</summary>
    public abstract string Descricao { get; }

    public abstract Task<ResultadoPasso> PassoAsync(
        ISisregWebSessao sessao, IArmazemIndicadoresSisreg armazem, CancellationToken ct);

    protected static string Br(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Converte a exceção do passo no desfecho — CAPTCHA e corte de conexão têm tratamento próprio.</summary>
    public static async Task<ResultadoPasso> ExecutarAsync(
        TrabalhoColeta trabalho, ISisregWebSessao sessao, IArmazemIndicadoresSisreg armazem, CancellationToken ct)
    {
        try
        {
            return await trabalho.PassoAsync(sessao, armazem, ct);
        }
        catch (Exception ex) when (SisregWebSessao.EhCaptcha(ex))
        {
            return new ResultadoPasso(DesfechoPasso.Captcha, Mensagem: ex.Message);
        }
        catch (Exception ex) when (SisregWebSessao.EhLoginRecusado(ex))
        {
            // Antes de 10/10/2026 esta exceção escapava do passo: o item voltava a pendente como órfão
            // a cada tick e, na madrugada de login recusado (09/10), 10 leituras de faltas gastaram as
            // 6 tentativas e ficaram paradas sem erro nenhum na tela.
            return new ResultadoPasso(DesfechoPasso.LoginRecusado, Mensagem: ex.Message);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested
                                   && ex is HttpRequestException or TaskCanceledException or IOException)
        {
            // O proxy do SISREG corta consulta pesada aos ~65 s (Automais.SISREG APRENDIZADOS, Rodada 4).
            return ResultadoPasso.TempoEsgotado($"o SISREG não respondeu a tempo ({ex.GetType().Name})");
        }
    }
}

/// <summary>
/// Lista oficial de faltas de uma janela (<c>rel_amb_faltas_sol.pl</c>). Dois passos: a consulta
/// paginada diz quantas páginas de 10 existem; a lista inteira (<c>imprimir_lista=1</c>) tem de caber
/// nesse número. Só então a janela é substituída.
///
/// <para><paramref name="recentes"/>: semana ainda nova, relida de hora em hora
/// (<see cref="ColetorIndicadorSisreg.FaltasRecentes"/>). A prova de leitura completa é a mesma, mas a
/// trava de encolhimento NÃO se aplica: nas primeiras semanas a unidade ainda aponta e corrige, e uma
/// releitura recusada por "encolheu" deixaria como falta quem a unidade já trocou para confirmado.</para>
///
/// <para><paramref name="substituiProvisoria"/>: leitura OFICIAL de uma semana que vinha sendo lida
/// como recente. O que está gravado é a lista provisória — se ela ficou velha (coletor pausado por
/// dias), a oficial vem bem menor, e a trava a recusaria para sempre.</para>
/// </summary>
public sealed class TrabalhoFaltas(
    ItemColeta item, double fracaoMinima, bool recentes = false, bool substituiProvisoria = false) : TrabalhoColeta(item)
{
    public const string Tela = "/cgi-bin/rel_amb_faltas_sol.pl";
    private int? _paginas;

    public override string Descricao =>
        $"faltas {(recentes ? "recentes " : string.Empty)}{Br(Item.Inicio)} a {Br(Item.Fim)}";

    public override async Task<ResultadoPasso> PassoAsync(
        ISisregWebSessao sessao, IArmazemIndicadoresSisreg armazem, CancellationToken ct)
    {
        var html = await sessao.GetAsync(Tela, Campos(lista: _paginas is not null), ct);
        if (IndicadoresSisregHtmlParser.PaginaDeGateway(html))
            return ResultadoPasso.TempoEsgotado("o SISREG devolveu a página de tempo esgotado (504)");
        if (!IndicadoresSisregHtmlParser.Inteira(html) || !IndicadoresSisregHtmlParser.TelaDeFaltas(html))
            return ResultadoPasso.Falha("a resposta não é a Consulta de Absenteísmo inteira — nada foi gravado");

        var faltas = IndicadoresSisregHtmlParser.Faltas(html);
        if (_paginas is null)
        {
            // Sem rodapé: ou não há falta nenhuma, ou cabe tudo numa página — o SISREG só escreve
            // "Mostrando Página de N" quando há mais de uma. Medido em 07/10/2026: 12/09 (9 faltas),
            // 27/09 (8) e 04/10 (3) ficavam em falha para sempre por exigir o rodapé.
            _paginas = IndicadoresSisregHtmlParser.PaginasDeFaltas(html)
                       ?? (faltas.Count <= IndicadoresSisregHtmlParser.FaltasPorPagina ? (faltas.Count == 0 ? 0 : 1) : null);
            if (_paginas is null)
                return ResultadoPasso.Falha("a consulta paginada não trouxe o rodapé \"Mostrando Página de N\"");
            return ResultadoPasso.Continuar();
        }

        // Prova: N páginas de 10 → entre 10·(N−1)+1 e 10·N linhas na lista inteira.
        var n = _paginas.Value;
        var minimo = n == 0 ? 0 : (n - 1) * IndicadoresSisregHtmlParser.FaltasPorPagina + 1;
        var maximo = n * IndicadoresSisregHtmlParser.FaltasPorPagina;
        if (faltas.Count < minimo - FolgaDaPaginacao || faltas.Count > maximo)
        {
            return ResultadoPasso.Falha(
                $"a lista trouxe {faltas.Count} falta(s) e a paginação indica entre {minimo} e {maximo} — nada foi gravado");
        }
        var aviso = faltas.Count < minimo
            ? $"a lista trouxe {faltas.Count} falta(s) e a paginação do SISREG indica a partir de {minimo} — gravada assim"
            : null;

        var anteriores = await armazem.ContarFaltasAsync(Item.Inicio, Item.Fim, ct);
        var unicas = faltas.Select(f => (f.Codigo, f.DataExecucao)).Distinct().Count();
        if (!recentes && !substituiProvisoria
            && SubstituicaoDeJanela.Recusar(anteriores, unicas, provaDeLeituraCompleta: true, fracaoMinima) is { } motivo)
        {
            return ResultadoPasso.Falha(motivo);
        }

        await armazem.SubstituirFaltasAsync(Item.Inicio, Item.Fim, faltas, ct);
        return new ResultadoPasso(DesfechoPasso.Concluida, unicas, Mensagem: aviso);
    }

    /// <summary>
    /// Quantas linhas a lista inteira pode ter A MENOS que a paginação indica. A contagem paginada da
    /// rede inteira às vezes conta uma a mais que a lista entrega: em 07/10/2026 o 14/09 veio com 310
    /// e a paginação dizia 32 páginas (311–320); lido unidade executante por unidade, as 13 listas
    /// bateram cada uma com a própria paginação e somaram exatamente 310 — o nosso leitor não perdeu
    /// linha, a sobra é do SISREG. Sem folga, o dia era recusado inteiro e ficava sem falta nenhuma
    /// (875 agendamentos como "Pendente"). A folga é pequena de propósito: corte de verdade no meio
    /// da lista perde muito mais que isso, e resposta sem <c>&lt;/html&gt;</c> já é recusada antes.
    /// </summary>
    public const int FolgaDaPaginacao = 2;

    private Dictionary<string, string> Campos(bool lista)
    {
        var campos = new Dictionary<string, string>
        {
            ["co_solicitacao"] = "", ["ETAPA"] = "", ["ordem"] = "1", ["offset"] = "0",
            ["cnes_solicitante"] = "", ["cnes_executante"] = "", ["cns"] = "", ["co_proc"] = "", ["no_proc"] = "",
            ["data1"] = Br(Item.Inicio), ["data2"] = Br(Item.Fim),
        };
        if (lista) campos["imprimir_lista"] = "1";
        return campos;
    }
}

/// <summary>Cotas da PPI de uma competência (<c>cons_ppi_cotas</c>), pela central do município.</summary>
public sealed class TrabalhoPpi(ItemColeta item, string? codigoMunicipio, double fracaoMinima) : TrabalhoColeta(item)
{
    public const string Tela = "/cgi-bin/cons_ppi_cotas";

    public override string Descricao => $"PPI {Item.Inicio:MM/yyyy}";

    public override async Task<ResultadoPasso> PassoAsync(
        ISisregWebSessao sessao, IArmazemIndicadoresSisreg armazem, CancellationToken ct)
    {
        // A central é o município (IBGE de 6 dígitos no SISREG) — vem da Instituição, nunca do código.
        if (string.IsNullOrWhiteSpace(codigoMunicipio) || codigoMunicipio.Length != 6)
            return ResultadoPasso.Falha("o código IBGE do município não está cadastrado em Sistema → Instituição");

        var html = await sessao.PostFormAsync(Tela, new Dictionary<string, string>
        {
            ["tipo"] = "2", // executante
            ["exec"] = codigoMunicipio,
            ["solic"] = codigoMunicipio,
            ["mes"] = Item.Inicio.Month.ToString(CultureInfo.InvariantCulture),
            ["ano"] = Item.Inicio.Year.ToString(CultureInfo.InvariantCulture),
            ["ETAPA"] = "EXIBIR_PPI",
        }, ct);
        if (IndicadoresSisregHtmlParser.PaginaDeGateway(html))
            return ResultadoPasso.TempoEsgotado("o SISREG devolveu a página de tempo esgotado (504)");
        if (!IndicadoresSisregHtmlParser.Inteira(html) || !IndicadoresSisregHtmlParser.TelaDePpi(html))
            return ResultadoPasso.Falha("a resposta não é a Consulta de PPI inteira — nada foi gravado");

        var cotas = IndicadoresSisregHtmlParser.Ppi(html);
        if (cotas.Count == 0) return ResultadoPasso.Falha("a Consulta de PPI não trouxe nenhuma cota para a competência");

        var anteriores = await armazem.ContarPpiAsync(Item.Inicio, ct);
        if (SubstituicaoDeJanela.Recusar(anteriores, cotas.Count, provaDeLeituraCompleta: true, fracaoMinima) is { } motivo)
            return ResultadoPasso.Falha(motivo);

        await armazem.SubstituirPpiAsync(Item.Inicio, cotas, ct);
        return ResultadoPasso.Concluida(cotas.Count);
    }
}

/// <summary>
/// Todas as marcações canceladas de um mês (pela data do cancelamento), página a página. Cada página é
/// gravada na hora (upsert, não apaga nada); a janela só fecha como concluída se o total lido bater
/// com o DECLARADO — é esse total que vira o número oficial do mês.
/// </summary>
public sealed class TrabalhoCanceladasMes(ItemColeta item) : TrabalhoColeta(item)
{
    public const string Tela = "/cgi-bin/cons_marcacao_cancelada";
    private const int TetoPaginas = 400;

    private int _pagina;
    private int? _declaradas;
    private int? _paginas;

    // O que conta é o cancelamento DISTINTO (código + quando): a lista às vezes repete a mesma linha
    // em páginas vizinhas — set/2026 declarou 1486, a soma crua deu 1487 e o mês nunca fechava,
    // embora as 1486 estivessem gravadas (a gravação já deduplica pela mesma chave).
    private readonly HashSet<(string Codigo, DateTime? CanceladoEm)> _lidas = [];

    public override string Descricao =>
        $"canceladas {Item.Inicio:MM/yyyy}" + (_paginas is { } p ? $" — página {Math.Min(_pagina + 1, p)} de {p}" : "");

    public override async Task<ResultadoPasso> PassoAsync(
        ISisregWebSessao sessao, IArmazemIndicadoresSisreg armazem, CancellationToken ct)
    {
        var html = await sessao.PostFormAsync(Tela, new Dictionary<string, string>
        {
            ["etapa"] = "LISTAR_MARCACOES",
            ["tp_periodo"] = "C", // pela data do CANCELAMENTO
            ["dt_inicial"] = Br(Item.Inicio),
            ["dt_final"] = Br(Item.Fim),
            ["co_cnes_ups"] = "",
            ["pagina"] = _pagina.ToString(CultureInfo.InvariantCulture),
        }, ct);
        if (IndicadoresSisregHtmlParser.PaginaDeGateway(html))
            return ResultadoPasso.TempoEsgotado("o SISREG devolveu a página de tempo esgotado (504)");
        if (!IndicadoresSisregHtmlParser.Inteira(html))
            return ResultadoPasso.Falha($"a página {_pagina + 1} veio incompleta — o mês não foi fechado");

        var linhas = IndicadoresSisregHtmlParser.Canceladas(html);
        if (_pagina == 0)
        {
            (_declaradas, _paginas) = IndicadoresSisregHtmlParser.TotalCanceladas(html);
            if (_declaradas is null)
            {
                return linhas.Count == 0 && IndicadoresSisregHtmlParser.NenhumRegistro(html)
                    ? ResultadoPasso.Concluida(0)
                    : ResultadoPasso.Falha("a tela não declarou o total de marcações — nada foi fechado");
            }
        }

        foreach (var l in linhas) _lidas.Add((l.Codigo, l.CanceladoEm));
        await armazem.GravarCanceladasAsync(linhas, ct);
        _pagina++;

        var ultima = _paginas ?? 1;
        if (_pagina < ultima && _pagina < TetoPaginas) return ResultadoPasso.Continuar();

        // A tela DIZ quantas existem. Fechar sem bater é concluir de leitura incompleta.
        return _lidas.Count == _declaradas
            ? ResultadoPasso.Concluida(_declaradas.Value)
            : ResultadoPasso.Falha(
                $"a tela declarou {_declaradas} marcação(ões) em {ultima} página(s) e foram lidas {_lidas.Count} distintas");
    }
}

/// <summary>
/// AMOSTRA de motivos de um mês de canceladas: a primeira página (que declara o total e o número de
/// páginas) e mais algumas espalhadas até o fim da listagem — o mesmo desenho da amostra do laboratório,
/// agora guardando o CÓDIGO (a tabela exige). As linhas são gravadas com upsert, como as da conciliação.
///
/// <para><b>Não mexe no total do mês.</b> O total oficial é o declarado pela tela, que já está na janela
/// mensal da coleta; a janela da amostra (escopo "amostra") registra só quantas linhas foram lidas, e o
/// cálculo a ignora ao somar as canceladas.</para>
/// </summary>
public sealed class TrabalhoCanceladasAmostra(ItemColeta item, int paginasPorMes) : TrabalhoColeta(item)
{
    private readonly Queue<int> _fila = new([0]);
    private int _lidas;
    private int? _paginas;

    public override string Descricao =>
        $"amostra de motivos {Item.Inicio:MM/yyyy}" + (_paginas is { } p ? $" (de {p} páginas)" : "");

    public override async Task<ResultadoPasso> PassoAsync(
        ISisregWebSessao sessao, IArmazemIndicadoresSisreg armazem, CancellationToken ct)
    {
        var pagina = _fila.Dequeue();
        var html = await sessao.PostFormAsync(TrabalhoCanceladasMes.Tela, new Dictionary<string, string>
        {
            ["etapa"] = "LISTAR_MARCACOES",
            ["tp_periodo"] = "C",
            ["dt_inicial"] = Br(Item.Inicio),
            ["dt_final"] = Br(Item.Fim),
            ["co_cnes_ups"] = "",
            ["pagina"] = pagina.ToString(CultureInfo.InvariantCulture),
        }, ct);
        if (IndicadoresSisregHtmlParser.PaginaDeGateway(html))
            return ResultadoPasso.TempoEsgotado("o SISREG devolveu a página de tempo esgotado (504)");
        if (!IndicadoresSisregHtmlParser.Inteira(html))
            return ResultadoPasso.Falha($"a página {pagina + 1} veio incompleta");

        var linhas = IndicadoresSisregHtmlParser.Canceladas(html);
        if (_paginas is null)
        {
            var (declaradas, paginas) = IndicadoresSisregHtmlParser.TotalCanceladas(html);
            if (declaradas is null)
            {
                return linhas.Count == 0 && IndicadoresSisregHtmlParser.NenhumRegistro(html)
                    ? ResultadoPasso.Concluida(0)
                    : ResultadoPasso.Falha("a tela não declarou o total de marcações — não dá para espalhar a amostra");
            }
            _paginas = paginas ?? 1;
            foreach (var p in PlanoColetaIndicadores.PaginasDaAmostra(_paginas.Value, paginasPorMes).Where(p => p > 0))
                _fila.Enqueue(p);
        }
        else if (linhas.Count == 0)
        {
            // Página do meio sem linha nenhuma: a listagem mudou ou a resposta veio errada.
            return ResultadoPasso.Falha($"a página {pagina + 1} de {_paginas} voltou sem linhas");
        }

        _lidas += linhas.Count;
        await armazem.GravarCanceladasAsync(linhas, ct);
        return _fila.Count > 0 ? ResultadoPasso.Continuar() : ResultadoPasso.Concluida(_lidas);
    }
}

/// <summary>
/// Solicitações de uma unidade solicitante, num mês de solicitação, que saíram da fila sem agendamento:
/// devolvidas (4), negadas (6) e canceladas antes de agendar (3). Uma requisição por situação, com o
/// filtro de unidade — a rede inteira estoura o tempo do proxy.
/// </summary>
public sealed class TrabalhoDesfechos(ItemColeta item) : TrabalhoColeta(item)
{
    public const string Tela = "/cgi-bin/gerenciador_solicitacao";
    private static readonly SituacaoDesfechoSisreg[] Situacoes =
        [SituacaoDesfechoSisreg.Devolvida, SituacaoDesfechoSisreg.Negada, SituacaoDesfechoSisreg.CanceladaAntesDeAgendar];

    private int _indice;
    private readonly List<(DesfechoLidoSisreg, SituacaoDesfechoSisreg)> _lidos = [];

    public override string Descricao => $"desfechos da unidade {Item.Escopo}, {Item.Inicio:MM/yyyy}";

    public override async Task<ResultadoPasso> PassoAsync(
        ISisregWebSessao sessao, IArmazemIndicadoresSisreg armazem, CancellationToken ct)
    {
        var situacao = Situacoes[_indice];
        var html = await sessao.GetAsync(Tela, new Dictionary<string, string>
        {
            ["etapa"] = "LISTAR_SOLICITACOES", ["co_solicitacao"] = "", ["cns_paciente"] = "", ["no_usuario"] = "",
            ["cnes_solicitante"] = Item.Escopo, ["cnes_executante"] = "", ["co_proc_unificado"] = "",
            ["co_pa_interno"] = "", ["ds_procedimento"] = "",
            ["tipo_periodo"] = "S", // pela data da SOLICITAÇÃO
            ["dt_inicial"] = Br(Item.Inicio), ["dt_final"] = Br(Item.Fim),
            ["cmb_situacao"] = ((int)situacao).ToString(CultureInfo.InvariantCulture),
            ["qtd_itens_pag"] = "0", // tudo numa página (medido: até ~1.500 linhas batem com o declarado)
            ["co_seq_solicitacao"] = "", ["ordenacao"] = "2", ["pagina"] = "0",
        }, ct);
        if (IndicadoresSisregHtmlParser.PaginaDeGateway(html))
            return ResultadoPasso.TempoEsgotado("o SISREG devolveu a página de tempo esgotado (504)");
        if (!IndicadoresSisregHtmlParser.Inteira(html))
            return ResultadoPasso.Falha("a resposta do gerenciador veio incompleta — nada foi gravado");

        var linhas = IndicadoresSisregHtmlParser.Desfechos(html);
        var declaradas = IndicadoresSisregHtmlParser.Retornadas(html);
        if (declaradas is null && !(linhas.Count == 0 && IndicadoresSisregHtmlParser.NenhumRegistro(html)))
            return ResultadoPasso.Falha($"o gerenciador não declarou o total (situação {(int)situacao}) — nada foi gravado");
        if (declaradas is { } n && n != linhas.Count)
            return ResultadoPasso.Falha($"situação {(int)situacao}: a tela declarou {n} e vieram {linhas.Count} — nada foi gravado");

        _lidos.AddRange(linhas.Select(l => (l, situacao)));
        _indice++;
        if (_indice < Situacoes.Length) return ResultadoPasso.Continuar();

        await armazem.GravarDesfechosAsync(Item.Escopo, _lidos, ct);
        return ResultadoPasso.Concluida(_lidos.Count);
    }
}

/// <summary>
/// A lista de unidades solicitantes do município (o <c>select</c> da tela de negados/devolvidos),
/// uma vez por mês — cada unidade vira um item de desfechos daquele mês.
/// </summary>
public sealed class TrabalhoUnidades(ItemColeta item) : TrabalhoColeta(item)
{
    public const string Tela = "/cgi-bin/cons_negados_reg";
    public const string Escopo = "unidades";

    public override string Descricao => $"unidades solicitantes para {Item.Inicio:MM/yyyy}";

    public override async Task<ResultadoPasso> PassoAsync(
        ISisregWebSessao sessao, IArmazemIndicadoresSisreg armazem, CancellationToken ct)
    {
        var html = await sessao.GetAsync(Tela, null, ct);
        if (IndicadoresSisregHtmlParser.PaginaDeGateway(html))
            return ResultadoPasso.TempoEsgotado("o SISREG devolveu a página de tempo esgotado (504)");
        var unidades = IndicadoresSisregHtmlParser.UnidadesSolicitantes(html);
        if (!IndicadoresSisregHtmlParser.Inteira(html) || unidades.Count == 0)
            return ResultadoPasso.Falha("a tela não trouxe a lista de unidades solicitantes");
        return new ResultadoPasso(DesfechoPasso.Concluida, unidades.Count, Unidades: unidades);
    }
}
