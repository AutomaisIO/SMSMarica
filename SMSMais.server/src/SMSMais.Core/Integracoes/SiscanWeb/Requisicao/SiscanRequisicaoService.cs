using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SiscanWeb.Requisicao.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Siscan.Sessao;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Integracoes.SiscanWeb.Requisicao;

public interface ISiscanRequisicaoService
{
    /// <summary>Percorre o assistente até a lista de responsáveis, SEM gravar nada.</summary>
    Task<SiscanPreparoDto> PrepararAsync(Guid exameImagemId, CancellationToken cancellationToken);

    /// <summary>Gera a requisição e carimba os números no exame. ESCREVE em produção federal.</summary>
    Task<SiscanRequisicaoDto> GerarAsync(
        Guid exameImagemId, SiscanGerarRequest corpo, CancellationToken cancellationToken);
}

/// <summary>
/// Gera, a partir da nossa anamnese, a requisição de mamografia no SISCAN — e traz de volta os
/// números para a médica seguir com o laudo.
///
/// <para><b>A ordem não é escolha nossa, é imposição da tela</b> (medida em 22/09/2026):
/// CNS → tipo de exame (só então a Unidade Requisitante tem opções) → Avançar → tipo de
/// mamografia (só então o combo Responsável tem opções, e a lista MUDA entre diagnóstica e
/// rastreamento) → Responsável (o servidor deriva o Conselho) → Salvar.</para>
///
/// <para><b>Nada de id fixo.</b> Os <c>j_idNN</c> das perguntas são auto-gerados; aqui cada um é
/// resolvido pela legenda do fieldset, que é o texto que a paciente lê. Se a legenda não for
/// encontrada, isto falha dizendo que a tela mudou — em vez de postar num campo chutado.</para>
///
/// <para><b>A releitura é parte do fluxo.</b> O modal do Salvar devolve só o protocolo; o Nº do
/// Exame só existe na grade. E "Registro salvo com sucesso" está no HTML antes de haver
/// requisição — a prova é reler.</para>
/// </summary>
public sealed class SiscanRequisicaoService(
    SmsMaisDbContext db,
    ISiscanSessaoOperadorStore sessoes,
    IPacienteResolver pacienteResolver,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<SiscanRequisicaoService> logger) : ISiscanRequisicaoService
{
    /// <summary>De-para entre o nome constante que o mapper usa e a legenda que o acha na tela.</summary>
    private static readonly (string Constante, string Legenda)[] PerguntasPorLegenda =
    [
        (SiscanRequisicaoMapper.CampoNodulo, "TEM NODULO OU CAROCO NA MAMA"),
        (SiscanRequisicaoMapper.CampoRiscoElevado, "APRESENTA RISCO ELEVADO"),
        (SiscanRequisicaoMapper.CampoMamasExaminadas, "TEVE AS MAMAS EXAMINADAS"),
        (SiscanRequisicaoMapper.CampoFezMamografia, "FEZ MAMOGRAFIA ALGUMA VEZ"),
        (SiscanRequisicaoMapper.CampoRadioterapia, "FEZ RADIOTERAPIA"),
        (SiscanRequisicaoMapper.CampoCirurgia, "FEZ CIRURGIA DE MAMA"),
        (SiscanRequisicaoMapper.CampoTipoMamografia, "TIPO DE MAMOGRAFIA"),
    ];

    public async Task<SiscanPreparoDto> PrepararAsync(
        Guid exameImagemId, CancellationToken cancellationToken)
    {
        var caso = await CarregarAsync(exameImagemId, cancellationToken);

        if (caso.Exame.SiscanProtocolo is { Length: > 0 } protocolo)
        {
            return new SiscanPreparoDto(
                true, protocolo, caso.Exame.SiscanNumeroExame, caso.PacienteNome,
                caso.CnesUnidade, caso.UnidadeNome, caso.TipoMamografia,
                RotuloTipo(caso.TipoMamografia), [], null, caso.SolicitanteDaFicha, [], []);
        }

        var sessao = sessoes.Exigir(SessaoId());

        // 1) A requisição DESTE pedido já está lá? Acontece quando ela nasceu fora do painel.
        //    Não se cria outra — a tela oferece vincular, e os números vêm junto.
        var nossa = await ProcurarPeloProntuarioAsync(sessao, caso, cancellationToken);
        if (nossa is not null)
        {
            return new SiscanPreparoDto(
                false, null, null, caso.PacienteNome, caso.CnesUnidade, caso.UnidadeNome,
                caso.TipoMamografia, RotuloTipo(caso.TipoMamografia), [], null,
                caso.SolicitanteDaFicha, [], [], EncontradaPeloProntuario: nossa);
        }

        // 2) A paciente já tem OUTRA requisição na janela? Aqui o sistema para.
        var duplicidades = await ProcurarPorCnsAsync(sessao, caso, cancellationToken);
        if (duplicidades.Count > 0)
        {
            return new SiscanPreparoDto(
                false, null, null, caso.PacienteNome, caso.CnesUnidade, caso.UnidadeNome,
                caso.TipoMamografia, RotuloTipo(caso.TipoMamografia), [], null,
                caso.SolicitanteDaFicha, [], [], Duplicidades: duplicidades);
        }

        var percurso = await PercorrerAsync(sessao, caso, cancellationToken);

        var campos = SiscanRequisicaoMapper.Montar(
            caso.ConteudoAnamnese, caso.Exame.AccessionNumber, caso.DataSolicitacao, caso.TipoMamografia);

        var responsaveis = Responsaveis(percurso.Doc);
        var sugerido = Sugerir(responsaveis, caso.SolicitanteDaFicha);

        return new SiscanPreparoDto(
            false, null, null, caso.PacienteNome, caso.CnesUnidade, caso.UnidadeNome,
            caso.TipoMamografia, RotuloTipo(caso.TipoMamografia),
            responsaveis, sugerido?.Cns, caso.SolicitanteDaFicha,
            Resumir(campos.Campos), campos.Lacunas);
    }

    public async Task<SiscanRequisicaoDto> GerarAsync(
        Guid exameImagemId, SiscanGerarRequest corpo, CancellationToken cancellationToken)
    {
        var caso = await CarregarAsync(exameImagemId, cancellationToken);

        if (caso.Exame.SiscanProtocolo is { Length: > 0 })
        {
            throw new ConflitoException(
                "siscan.requisicao_ja_existe",
                "Esta solicitação já tem requisição no SISCAN "
                + $"(protocolo {caso.Exame.SiscanProtocolo}). Gerar de novo criaria uma requisição "
                + "duplicada para a mesma paciente.");
        }

        var sessao = sessoes.Exigir(SessaoId());

        // A requisição DESTE pedido já está lá? Vem ANTES da crítica de lacunas de propósito:
        // vincular o que já existe não pode depender de a anamnese estar completa — o dado já
        // está no SISCAN de qualquer jeito, e o que falta aqui é só o carimbo.
        //
        // Cobre dois casos: o POST que gravou e caiu antes de carimbar, e a requisição que nasceu
        // fora do painel (foi o que aconteceu com as criadas pelo laboratório).
        var nossa = await ProcurarPeloProntuarioAsync(sessao, caso, cancellationToken);
        if (nossa is not null)
        {
            logger.LogWarning(
                "SISCAN: requisição do accession {Accession} JÁ existia (protocolo {Protocolo}). "
                + "Vinculando em vez de criar outra.", caso.Exame.AccessionNumber, nossa.Protocolo);

            return await CarimbarAsync(
                caso.Exame, nossa.Protocolo, nossa.NumeroExame, "(já existia no SISCAN)",
                cancellationToken);
        }

        // A paciente já tem OUTRA requisição na janela? O sistema não decide qual vale.
        var duplicidades = await ProcurarPorCnsAsync(sessao, caso, cancellationToken);
        if (duplicidades.Count > 0)
        {
            var lista = string.Join(" · ", duplicidades.Take(3).Select(
                d => $"protocolo {d.Protocolo} ({d.Status}, {d.Unidade})"));

            throw new ConflitoException(
                "siscan.paciente_ja_tem_requisicao",
                $"Esta paciente já tem {duplicidades.Count} requisição(ões) de mamografia no "
                + $"SISCAN no último ano: {lista}. Resolva lá qual delas vale antes de criar outra.");
        }

        var campos = SiscanRequisicaoMapper.Montar(
            caso.ConteudoAnamnese, caso.Exame.AccessionNumber, caso.DataSolicitacao, caso.TipoMamografia);

        if (campos.Lacunas.Count > 0)
        {
            throw new ValidacaoException(
                "siscan.anamnese_incompleta",
                "A anamnese não respondeu tudo que o SISCAN exige: "
                + string.Join(" · ", campos.Lacunas.Select(l => l.Pergunta)));
        }

        var percurso = await PercorrerAsync(sessao, caso, cancellationToken);

        var responsaveis = Responsaveis(percurso.Doc);
        var responsavel = responsaveis.FirstOrDefault(r => r.Cns == corpo.CnsResponsavel?.Trim())
            ?? throw new ValidacaoException(
                "siscan.responsavel_indisponivel",
                "O responsável escolhido não está na lista do SISCAN para esta unidade e este tipo "
                + "de mamografia. A lista muda entre diagnóstica e rastreamento — escolha de novo.");

        var html = await EscolherResponsavelAsync(sessao, percurso.Html, responsavel, cancellationToken);
        html = await AbrirCondicionaisAsync(sessao, html, campos.Campos, cancellationToken);

        var doc = SiscanHtml.Documento(html);
        var envio = TraduzirParaATela(doc, campos.Campos);
        envio.Add(new KeyValuePair<string, string>(
            SiscanRequisicaoMapper.CampoResponsavel, responsavel.Indice));
        envio.Add(new KeyValuePair<string, string>("frm:btSalvar", "frm:btSalvar"));

        var resposta = await sessao.SubmeterEscritaAsync(
            html, SiscanHtml.FormPrincipal, envio,
            $"criar requisição de mamografia (accession {caso.Exame.AccessionNumber})",
            cancellationToken);

        var respostaDoc = SiscanHtml.Documento(resposta);
        var protocolo = SiscanHtml.ProtocoloDoModal(respostaDoc);
        if (protocolo is null)
        {
            var mensagens = SiscanHtml.Mensagens(respostaDoc);
            var motivo = mensagens.Count > 0 ? string.Join(" · ", mensagens.Take(3)) : "sem mensagem";
            await RegistrarErroAsync(caso.Exame, motivo, cancellationToken);

            throw new ValidacaoException(
                "siscan.requisicao_nao_confirmada",
                $"O SISCAN não devolveu o número do protocolo. Motivo informado: {motivo}.");
        }

        // O Nº do Exame NÃO vem no modal. Sem a releitura, a médica ficaria com metade do que
        // precisa para achar o exame lá.
        var naGrade = await ProcurarPeloProntuarioAsync(sessao, caso, cancellationToken);
        var numeroExame = naGrade?.NumeroExame ?? string.Empty;

        return await CarimbarAsync(caso.Exame, protocolo, numeroExame, responsavel.Nome, cancellationToken);
    }

    // ------------------------------------------------------------------ percurso

    private sealed record Percurso(string Html, IHtmlDocument Doc);

    private async Task<Percurso> PercorrerAsync(
        ISiscanWebSessao sessao, Caso caso, CancellationToken cancellationToken)
    {
        var html = await sessao.AbrirPorMenuAsync(SiscanWebSessao.MenuGerenciarExame, cancellationToken);

        html = await ClicarNovoExameAsync(sessao, html, cancellationToken);
        html = await DigitarCartaoSusAsync(sessao, html, caso.Cns, cancellationToken);
        html = await MarcarTipoExameAsync(sessao, html, cancellationToken);
        html = await AvancarAsync(sessao, html, caso, cancellationToken);
        html = await MarcarTipoMamografiaAsync(sessao, html, caso.TipoMamografia, cancellationToken);

        return new Percurso(html, SiscanHtml.Documento(html));
    }

    private static async Task<string> ClicarNovoExameAsync(
        ISiscanWebSessao sessao, string html, CancellationToken cancellationToken)
    {
        var doc = SiscanHtml.Documento(html);
        var botao = doc.GetElementById("frm:botaoNovoExame")
                    ?? throw new ValidacaoException(
                        "siscan.sem_novo_exame",
                        "O botão 'Novo Exame' não apareceu — esta conta do SISCAN pode não ter "
                        + "permissão para criar requisição.");

        return await sessao.SubmeterA4JAsync(
            html, SiscanHtml.FormPrincipal, new Dictionary<string, string>(),
            SiscanHtml.ParametrosA4JDoElemento(botao), cancellationToken,
            SiscanWebSessao.NavegacaoDaRequisicao);
    }

    private static async Task<string> DigitarCartaoSusAsync(
        ISiscanWebSessao sessao, string html, string cns, CancellationToken cancellationToken)
    {
        var doc = SiscanHtml.Documento(html);
        var campo = doc.QuerySelector("input[name='frm:cartaoSUS']")
                    ?? throw new ValidacaoException(
                        "siscan.tela_inesperada", "A tela de Novo Exame não trouxe o campo Cartão SUS.");

        // Só o `onblur` já traz o paciente do CADSUS inteiro — não é preciso clicar a lupa.
        var resultado = await sessao.SubmeterA4JAsync(
            html, SiscanHtml.FormPrincipal,
            new Dictionary<string, string> { ["frm:cartaoSUS"] = cns },
            SiscanHtml.ParametrosA4JDoElemento(campo), cancellationToken);

        var nome = SiscanHtml.ValorDoCampo(SiscanHtml.Documento(resultado), "frm:nome");
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ValidacaoException(
                "siscan.paciente_nao_encontrado",
                $"O CADSUS não encontrou o Cartão SUS {cns} pela tela do SISCAN.");
        }

        return resultado;
    }

    private static async Task<string> MarcarTipoExameAsync(
        ISiscanWebSessao sessao, string html, CancellationToken cancellationToken)
    {
        var doc = SiscanHtml.Documento(html);
        var radio = doc.QuerySelector("input[name='frm:tipoExame'][value='01']")
                    ?? throw new ValidacaoException(
                        "siscan.tela_inesperada", "A tela não ofereceu o tipo de exame Mamografia.");

        return await sessao.SubmeterA4JAsync(
            html, SiscanHtml.FormPrincipal,
            new Dictionary<string, string> { ["frm:tipoExame"] = "01" },
            SiscanHtml.ParametrosA4JDoElemento(radio), cancellationToken);
    }

    private static async Task<string> AvancarAsync(
        ISiscanWebSessao sessao, string html, Caso caso, CancellationToken cancellationToken)
    {
        var doc = SiscanHtml.Documento(html);

        // Unidade pelo CNES, nunca pelo índice: o `value` do option é posicional.
        var opcao = SiscanHtml.Opcoes(doc, "frm:unidadeSaude2")
            .FirstOrDefault(o => o.Texto.StartsWith(caso.CnesUnidade, StringComparison.Ordinal));

        if (string.IsNullOrEmpty(opcao.Valor))
        {
            throw new ValidacaoException(
                "siscan.unidade_indisponivel",
                $"A unidade CNES {caso.CnesUnidade} ({caso.UnidadeNome}) não está entre as unidades "
                + "requisitantes que esta conta do SISCAN enxerga.");
        }

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // O tipo de exame VAI DE NOVO, mesmo já tendo ido pro bean pelo A4J: o parcial não
            // devolve o radio marcado, e sem isto o SISCAN responde "O campo Tipo de Exame deve
            // ser informado" — mostrando o radio marcado na resposta, o que confunde ainda mais.
            ["frm:tipoExame"] = "01",
            ["frm:prestadorServico2"] = "0",
            ["frm:unidadeSaude2"] = opcao.Valor,
            ["frm:botaoAvancar"] = "frm:botaoAvancar",
        };

        var resultado = await sessao.SubmeterFormAsync(
            html, SiscanHtml.FormPrincipal, extras, cancellationToken,
            SiscanWebSessao.NavegacaoDaRequisicao);

        var titulos = SiscanHtml.Titulos(SiscanHtml.Documento(resultado));
        if (!titulos.Any(t => t.Contains("SOLICITAR", StringComparison.OrdinalIgnoreCase)))
        {
            var mensagens = SiscanHtml.Mensagens(SiscanHtml.Documento(resultado));
            throw new ValidacaoException(
                "siscan.avancar_recusado",
                "O SISCAN não abriu a tela da requisição. "
                + (mensagens.Count > 0 ? string.Join(" · ", mensagens.Take(2)) : $"Tela: {string.Join(", ", titulos)}"));
        }

        return resultado;
    }

    private static async Task<string> MarcarTipoMamografiaAsync(
        ISiscanWebSessao sessao, string html, string tipo, CancellationToken cancellationToken)
    {
        var doc = SiscanHtml.Documento(html);
        var nome = SiscanHtml.CampoPorLegenda(doc, "TIPO DE MAMOGRAFIA")
                   ?? throw new ValidacaoException(
                       "siscan.tela_mudou",
                       "Não achei a pergunta 'TIPO DE MAMOGRAFIA' na tela do SISCAN.");

        var radio = doc.QuerySelector($"input[name='{nome}'][value='{tipo}']")
                    ?? throw new ValidacaoException(
                        "siscan.tela_mudou", $"O tipo de mamografia '{tipo}' não existe na tela.");

        // É ESTE A4J que popula o combo Responsável — antes dele o <select> vem vazio, e um A4J
        // qualquer não serve (o de risco elevado deixa o combo vazio do mesmo jeito).
        return await sessao.SubmeterA4JAsync(
            html, SiscanHtml.FormPrincipal,
            new Dictionary<string, string> { [nome] = tipo },
            SiscanHtml.ParametrosA4JDoElemento(radio), cancellationToken);
    }

    private static async Task<string> EscolherResponsavelAsync(
        ISiscanWebSessao sessao, string html, SiscanResponsavelDto responsavel,
        CancellationToken cancellationToken)
    {
        var doc = SiscanHtml.Documento(html);
        var combo = doc.QuerySelector($"select[name='{SiscanRequisicaoMapper.CampoResponsavel}']")
                    ?? throw new ValidacaoException(
                        "siscan.tela_mudou", "O combo 'Responsável' sumiu da tela.");

        // O servidor DERIVA o conselho a partir do responsável e o devolve disabled — por isso
        // ele nunca é enviado por nós.
        return await sessao.SubmeterA4JAsync(
            html, SiscanHtml.FormPrincipal,
            new Dictionary<string, string> { [SiscanRequisicaoMapper.CampoResponsavel] = responsavel.Indice },
            SiscanHtml.ParametrosA4JDoElemento(combo), cancellationToken);
    }

    /// <summary>
    /// Abre as regiões condicionais que vamos preencher.
    ///
    /// <para>As perguntas com "Sim" abrem campos que <b>só existem depois do A4J</b> (ano da última
    /// mamografia, lado e ano da radioterapia, os 26 anos de cirurgia). Mandar o ano junto com o
    /// Salvar, sem ter aberto a região, é mandar campo que o servidor não conhece — ele ignora,
    /// sem erro.</para>
    /// </summary>
    private static async Task<string> AbrirCondicionaisAsync(
        ISiscanWebSessao sessao, string html, List<KeyValuePair<string, string>> campos,
        CancellationToken cancellationToken)
    {
        var abrir = new (string Constante, string Legenda, string Valor)[]
        {
            (SiscanRequisicaoMapper.CampoFezMamografia, "FEZ MAMOGRAFIA ALGUMA VEZ", "01"),
            (SiscanRequisicaoMapper.CampoRadioterapia, "FEZ RADIOTERAPIA", "01"),
            (SiscanRequisicaoMapper.CampoCirurgia, "FEZ CIRURGIA DE MAMA", "S"),
        };

        foreach (var (constante, legenda, valorQueAbre) in abrir)
        {
            var resposta = campos.FirstOrDefault(c => c.Key == constante).Value;
            if (resposta != valorQueAbre) continue;

            html = await DispararRadioAsync(sessao, html, legenda, valorQueAbre, cancellationToken);
        }

        // Terceiro nível: a localização da radioterapia só existe depois do "Sim", e é ela que
        // abre o ano de cada lado. Uma sondagem de um nível só para na localização.
        var lado = campos.FirstOrDefault(c => c.Key == SiscanRequisicaoMapper.CampoLocalRadioterapia).Value;
        if (!string.IsNullOrEmpty(lado))
        {
            var doc = SiscanHtml.Documento(html);
            var radio = doc.QuerySelector(
                $"input[name='{SiscanRequisicaoMapper.CampoLocalRadioterapia}'][value='{lado}']");
            if (radio is not null)
            {
                html = await sessao.SubmeterA4JAsync(
                    html, SiscanHtml.FormPrincipal,
                    new Dictionary<string, string> { [SiscanRequisicaoMapper.CampoLocalRadioterapia] = lado },
                    SiscanHtml.ParametrosA4JDoElemento(radio), cancellationToken);
            }
        }

        return html;
    }

    private static async Task<string> DispararRadioAsync(
        ISiscanWebSessao sessao, string html, string legenda, string valor,
        CancellationToken cancellationToken)
    {
        var doc = SiscanHtml.Documento(html);
        var nome = SiscanHtml.CampoPorLegenda(doc, legenda);
        if (nome is null) return html;

        var radio = doc.QuerySelector($"input[name='{nome}'][value='{valor}']");
        if (radio is null) return html;

        return await sessao.SubmeterA4JAsync(
            html, SiscanHtml.FormPrincipal,
            new Dictionary<string, string> { [nome] = valor },
            SiscanHtml.ParametrosA4JDoElemento(radio), cancellationToken);
    }

    /// <summary>
    /// Troca os nomes constantes das perguntas pelos nomes REAIS da tela, resolvidos por legenda.
    ///
    /// <para>É o que permite não ter <c>frm:j_id91</c> fixo no código. Quando uma legenda não é
    /// encontrada, isto falha — de propósito: postar num <c>j_idNN</c> chutado pode gravar a
    /// resposta na pergunta errada, e ninguém veria.</para>
    /// </summary>
    private static List<KeyValuePair<string, string>> TraduzirParaATela(
        IHtmlDocument doc, List<KeyValuePair<string, string>> campos)
    {
        var deParaNomes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (constante, legenda) in PerguntasPorLegenda)
        {
            var real = SiscanHtml.CampoPorLegenda(doc, legenda)
                       ?? throw new ValidacaoException(
                           "siscan.tela_mudou",
                           $"Não achei a pergunta '{legenda}' na tela da requisição. A tela do "
                           + "SISCAN mudou — o de-para precisa ser refeito antes de gravar.");
            deParaNomes[constante] = real;
        }

        // A localização da radioterapia é filha do "Sim" e não tem fieldset próprio: quando ela
        // for necessária, o nome real vem do que está renderizado agora.
        if (campos.Any(c => c.Key == SiscanRequisicaoMapper.CampoLocalRadioterapia)
            && doc.QuerySelector($"input[name='{SiscanRequisicaoMapper.CampoLocalRadioterapia}']") is null)
        {
            throw new ValidacaoException(
                "siscan.tela_mudou",
                "A localização da radioterapia não está na tela — o 'Sim' da radioterapia não abriu "
                + "a região condicional.");
        }

        return campos
            .Select(c => new KeyValuePair<string, string>(
                deParaNomes.TryGetValue(c.Key, out var real) ? real : c.Key, c.Value))
            .ToList();
    }

    // ------------------------------------------------------------------ releitura

    /// <summary>
    /// Os três status da pesquisa. Varrer os três não é zelo: <b>o Status é obrigatório</b> —
    /// medido em 22/09/2026, sem ele a tela responde "Selecione um Status" e devolve zero linhas.
    /// Perguntar só por "Requisitado" deixaria passar uma requisição que já tem resultado, que é
    /// justamente a que mais importa não duplicar.
    /// </summary>
    private static readonly string[] TodosOsStatus = ["01", "02", "03"];

    /// <summary>
    /// A janela da crítica de duplicidade: <b>10 dias à frente, recuando um ano</b>.
    ///
    /// <para>Os 10 dias existem porque a requisição pode ter sido lançada com data de solicitação
    /// à frente; o ano para trás é o intervalo em que uma segunda mamografia da mesma paciente é
    /// suspeita e merece olho humano.</para>
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fim) JanelaDeDuplicidade(DateOnly hoje)
    {
        var fim = hoje.AddDays(10);
        return (fim.AddYears(-1), fim);
    }

    /// <summary>
    /// Pesquisa em GERENCIAR EXAME e devolve as linhas da grade.
    ///
    /// <para>A data precisa de um round-trip A4J antes do Pesquisar: em JSF 1.2 a validação roda
    /// ANTES do Update Model, então o validador cruzado lê no bean o valor antigo e responde
    /// "Data para comparação não informada".</para>
    /// </summary>
    private static async Task<List<SiscanHtml.LinhaExame>> PesquisarAsync(
        ISiscanWebSessao sessao, DateOnly de, DateOnly ate,
        IReadOnlyDictionary<string, string> filtros, CancellationToken cancellationToken)
    {
        var inicio = de.ToString("dd/MM/yyyy");
        var fim = ate.ToString("dd/MM/yyyy");
        var achadas = new List<SiscanHtml.LinhaExame>();

        foreach (var status in TodosOsStatus)
        {
            // Refazer a pesquisa a cada status, e não reaproveitar o documento: o ViewState já
            // foi consumido, e reaproveitá-lo dá resultado inconsistente sem erro nenhum.
            var html = await sessao.AbrirPorMenuAsync(
                SiscanWebSessao.MenuGerenciarExame, cancellationToken);
            var doc = SiscanHtml.Documento(html);

            var campoData = doc.QuerySelector("input[name='frm:dataRequisicaoInputDate']");
            if (campoData is not null)
            {
                html = await sessao.SubmeterA4JAsync(
                    html, SiscanHtml.FormPrincipal,
                    new Dictionary<string, string> { ["frm:dataRequisicaoInputDate"] = inicio },
                    SiscanHtml.ParametrosA4JDoElemento(campoData), cancellationToken);
                doc = SiscanHtml.Documento(html);
            }

            var extras = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["frm:statusExame"] = status,
                ["frm:dataRequisicaoInputDate"] = inicio,
                ["frm:dataRequisicaoFinalInputDate"] = fim,
                ["frm:botaoPesquisarExame"] = "frm:botaoPesquisarExame",
            };
            foreach (var (k, v) in filtros) extras[k] = v;

            // O checkbox de tipo de exame tem id auto-gerado — resolvido pelo rótulo.
            var campoMamografia = SiscanHtml.CampoPorRotulo(doc, "Mamografia");
            if (campoMamografia is not null) extras[campoMamografia] = "01";

            var resultado = await sessao.SubmeterFormAsync(
                html, SiscanHtml.FormPrincipal, extras, cancellationToken);

            achadas.AddRange(SiscanHtml.Grade(SiscanHtml.Documento(resultado)));
        }

        return achadas;
    }

    /// <summary>
    /// A requisição DESTE pedido já está no SISCAN? Pergunta pelo Nº do Prontuário, onde gravamos
    /// o nosso AccessionNumber.
    ///
    /// <para>Responde "sim" também quando ela nasceu fora do painel — foi o que aconteceu com as
    /// requisições criadas pelo laboratório antes de existir o carimbo. Nesse caso não se cria
    /// outra: vincula-se esta.</para>
    /// </summary>
    private static async Task<RequisicaoEncontradaDto?> ProcurarPeloProntuarioAsync(
        ISiscanWebSessao sessao, Caso caso, CancellationToken cancellationToken)
    {
        var (de, ate) = JanelaDeDuplicidade(DateOnly.FromDateTime(DateTime.Today));

        // A data da solicitação pode ser bem anterior à janela (registro retroativo é o caso
        // comum) — então a busca pelo NOSSO prontuário recua até ela.
        if (caso.DataSolicitacao < de) de = caso.DataSolicitacao.AddDays(-1);

        var linhas = await PesquisarAsync(
            sessao, de, ate,
            new Dictionary<string, string> { ["frm:numeroProntuario"] = caso.Exame.AccessionNumber },
            cancellationToken);

        return linhas.Count == 0 ? null : Encontrada(linhas[0]);
    }

    /// <summary>
    /// A paciente já tem requisição de mamografia na janela? Pergunta pelo Cartão SUS.
    ///
    /// <para>Aqui o sistema <b>não decide</b>: se achar, para e manda resolver no SISCAN. Criar a
    /// segunda seria empurrar para a frente um problema que só uma pessoa sabe resolver — qual
    /// das duas vale, e o que fazer com a outra.</para>
    /// </summary>
    private static async Task<List<RequisicaoEncontradaDto>> ProcurarPorCnsAsync(
        ISiscanWebSessao sessao, Caso caso, CancellationToken cancellationToken)
    {
        var (de, ate) = JanelaDeDuplicidade(DateOnly.FromDateTime(DateTime.Today));

        var linhas = await PesquisarAsync(
            sessao, de, ate,
            new Dictionary<string, string> { ["frm:cartaoSUS"] = caso.Cns },
            cancellationToken);

        return linhas.Select(Encontrada).ToList();
    }

    private static RequisicaoEncontradaDto Encontrada(SiscanHtml.LinhaExame l) =>
        new(SiscanHtml.NormalizarProtocolo(l.Protocolo), l.NumeroExame, l.Datas, l.Unidade, l.Status);

    // ------------------------------------------------------------------ nosso lado

    private sealed record Caso(
        ExameImagem Exame, string Cns, string PacienteNome, string CnesUnidade, string UnidadeNome,
        DateOnly DataSolicitacao, string TipoMamografia, string? ConteudoAnamnese,
        string? SolicitanteDaFicha);

    private async Task<Caso> CarregarAsync(Guid exameImagemId, CancellationToken cancellationToken)
    {
        var exame = await db.ExamesImagem
            .Include(e => e.Solicitacao)
            .FirstOrDefaultAsync(e => e.Id == exameImagemId && e.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), exameImagemId.ToString());

        var solicitacao = exame.Solicitacao
            ?? throw new ValidacaoException(
                "siscan.sem_solicitacao", "Este exame não tem a regulação-pai carregada.");

        var unidade = await db.Unidades.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == solicitacao.UnidadeSolicitanteId, cancellationToken);

        if (unidade is null || string.IsNullOrWhiteSpace(unidade.Cnes))
        {
            throw new ValidacaoException(
                "siscan.unidade_sem_cnes",
                "A unidade solicitante deste pedido não tem CNES — e é o CNES que casa a unidade "
                + "no SISCAN.");
        }

        var paciente = await pacienteResolver.ResolverAsync(solicitacao.PacienteId, cancellationToken);
        if (paciente?.Cns is not { Length: > 0 } cns)
        {
            throw new ValidacaoException(
                "siscan.paciente_sem_cns",
                "A paciente não tem Cartão SUS no cadastro, e é por ele que o SISCAN identifica.");
        }

        if (paciente.DataNascimento is not { } nascimento)
        {
            throw new ValidacaoException(
                "siscan.paciente_sem_nascimento",
                "A paciente não tem data de nascimento — é ela que decide se a mamografia é de "
                + "rastreamento ou diagnóstica.");
        }

        var anamnese = await db.Anamneses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ExameImagemId == exame.Id && a.ExcluidoEm == null, cancellationToken);

        // Sem data de solicitação não dá para gerar: o campo é obrigatório lá, e inventar "hoje"
        // mudaria o que o dado federal diz sobre quando a paciente pediu o exame.
        var data = solicitacao.DataSolicitacao
            ?? throw new ValidacaoException(
                "siscan.sem_data_solicitacao",
                "O pedido não tem data de solicitação, e o SISCAN exige esse campo na requisição.");

        var tipo = SiscanRequisicaoMapper.TipoMamografiaPorIdade(
            nascimento, DateOnly.FromDateTime(DateTime.Today));

        return new Caso(
            exame, cns, paciente.Nome ?? string.Empty, unidade.Cnes!, unidade.Nome ?? string.Empty,
            data, tipo, anamnese?.ConteudoJson, solicitacao.SolicitanteNome);
    }

    private async Task<SiscanRequisicaoDto> CarimbarAsync(
        ExameImagem exame, string protocolo, string numeroExame, string responsavelNome,
        CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        exame.SiscanProtocolo = protocolo;
        exame.SiscanNumeroExame = string.IsNullOrEmpty(numeroExame) ? null : numeroExame;
        exame.SiscanRequisicaoEm = agora;
        exame.SiscanRequisicaoPor = usuarioAtual.UsuarioId;
        exame.SiscanErro = null;
        exame.AtualizadoEm = agora;
        exame.AtualizadoPor = usuarioAtual.UsuarioId;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "SISCAN: requisição {Protocolo} (exame {NumeroExame}) criada para o accession "
            + "{Accession} por {Usuario}.",
            protocolo, numeroExame, exame.AccessionNumber, usuarioAtual.UsuarioId);

        return new SiscanRequisicaoDto(protocolo, numeroExame, agora, responsavelNome);
    }

    private async Task RegistrarErroAsync(
        ExameImagem exame, string motivo, CancellationToken cancellationToken)
    {
        exame.SiscanErro = motivo.Length > 1000 ? motivo[..1000] : motivo;
        exame.AtualizadoEm = DateTime.UtcNow;
        exame.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ apoio

    private static List<SiscanResponsavelDto> Responsaveis(IHtmlDocument doc) =>
        SiscanHtml.Opcoes(doc, SiscanRequisicaoMapper.CampoResponsavel)
            .Where(o => o.Valor != "0" && o.Texto.Contains(" - ", StringComparison.Ordinal))
            .Select(o =>
            {
                var corte = o.Texto.LastIndexOf(" - ", StringComparison.Ordinal);
                return new SiscanResponsavelDto(
                    o.Valor, o.Texto[..corte].Trim(), o.Texto[(corte + 3)..].Trim());
            })
            .ToList();

    /// <summary>
    /// Sugere o responsável casando o nome do solicitante da ficha com a lista do SISCAN.
    ///
    /// <para>É sugestão, nunca escolha: os nomes não batem na forma ("FERNANDA SOUZA" na ficha ×
    /// "FERNANDA SOUZA LEITE" no SISCAN) e o CNS do profissional não existe do nosso lado. Quem
    /// confirma é gente.</para>
    /// </summary>
    private static SiscanResponsavelDto? Sugerir(
        List<SiscanResponsavelDto> responsaveis, string? solicitante)
    {
        if (string.IsNullOrWhiteSpace(solicitante)) return null;

        var alvo = solicitante.Trim().ToUpperInvariant();
        var exato = responsaveis.FirstOrDefault(r => r.Nome.ToUpperInvariant() == alvo);
        if (exato is not null) return exato;

        var candidatos = responsaveis
            .Where(r => r.Nome.ToUpperInvariant().StartsWith(alvo, StringComparison.Ordinal)
                        || alvo.StartsWith(r.Nome.ToUpperInvariant(), StringComparison.Ordinal))
            .ToList();

        // Ambíguo não sugere: sugerir a primeira de duas faz a pessoa confirmar sem olhar.
        return candidatos.Count == 1 ? candidatos[0] : null;
    }

    private static string RotuloTipo(string tipo) =>
        tipo == SiscanRequisicaoMapper.Rastreamento ? "Rastreamento" : "Diagnóstica";

    private static List<SiscanCampoEnvioDto> Resumir(List<KeyValuePair<string, string>> campos)
    {
        var rotulos = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SiscanRequisicaoMapper.CampoProntuario] = "Nº do Prontuário (nosso pedido)",
            [SiscanRequisicaoMapper.CampoDataSolicitacao] = "Data da Solicitação",
            [SiscanRequisicaoMapper.CampoTipoMamografia] = "Tipo de mamografia",
            [SiscanRequisicaoMapper.CampoNodulo] = "Tem nódulo ou caroço na mama?",
            [SiscanRequisicaoMapper.CampoRiscoElevado] = "Apresenta risco elevado?",
            [SiscanRequisicaoMapper.CampoMamasExaminadas] = "Mamas já examinadas antes?",
            [SiscanRequisicaoMapper.CampoFezMamografia] = "Fez mamografia alguma vez?",
            [SiscanRequisicaoMapper.CampoRadioterapia] = "Fez radioterapia?",
            [SiscanRequisicaoMapper.CampoLocalRadioterapia] = "Radioterapia — localização",
            [SiscanRequisicaoMapper.CampoCirurgia] = "Fez cirurgia de mama?",
            [SiscanRequisicaoMapper.CampoPopulacaoRastreamento] = "População do rastreamento",
            [SiscanRequisicaoMapper.CampoAnoUltimaMamografia] = "Ano da última mamografia",
        };

        var valores = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01"] = "Sim", ["02"] = "Não", ["03"] = "Não sabe", ["04"] = "Não",
            ["S"] = "Sim", ["N"] = "Não",
        };

        return campos.Select(c =>
        {
            var pergunta = rotulos.TryGetValue(c.Key, out var r) ? r : c.Key;

            var resposta = c.Key switch
            {
                SiscanRequisicaoMapper.CampoNodulo => c.Value switch
                {
                    "01" => "Sim, mama direita",
                    "02" => "Sim, mama esquerda",
                    _ => "Não",
                },
                SiscanRequisicaoMapper.CampoTipoMamografia =>
                    c.Value == SiscanRequisicaoMapper.Rastreamento ? "Rastreamento" : "Diagnóstica",
                SiscanRequisicaoMapper.CampoMamasExaminadas => c.Value switch
                {
                    "01" => "Sim",
                    "02" => "Nunca foram examinadas anteriormente",
                    _ => "Não sabe",
                },
                SiscanRequisicaoMapper.CampoLocalRadioterapia => c.Value switch
                {
                    "01" => "Mama esquerda",
                    "02" => "Mama direita",
                    _ => "Ambas",
                },
                SiscanRequisicaoMapper.CampoPopulacaoRastreamento => c.Value switch
                {
                    "02" => "Risco elevado (história familiar)",
                    "03" => "Já tratada de câncer de mama",
                    _ => "População alvo",
                },
                _ => valores.TryGetValue(c.Value, out var v) ? v : c.Value,
            };

            return new SiscanCampoEnvioDto(pergunta, resposta);
        }).ToList();
    }

    private string SessaoId() =>
        usuarioAtual.SessaoId
        ?? throw new ValidacaoException(
            "siscan.sem_operador", "Sessão sem identificação — entre no sistema de novo.");
}
