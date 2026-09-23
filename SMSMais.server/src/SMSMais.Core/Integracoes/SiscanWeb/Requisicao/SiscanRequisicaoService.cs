using System.Diagnostics;
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

        var critica = await CriticarAsync(sessao, caso, cancellationToken);

        if (critica.Nossa is not null)
        {
            return new SiscanPreparoDto(
                false, null, null, caso.PacienteNome, caso.CnesUnidade, caso.UnidadeNome,
                caso.TipoMamografia, RotuloTipo(caso.TipoMamografia), [], null,
                caso.SolicitanteDaFicha, [], [], EncontradaPeloProntuario: critica.Nossa);
        }

        if (critica.DeOutroPedido.Count > 0)
        {
            return new SiscanPreparoDto(
                false, null, null, caso.PacienteNome, caso.CnesUnidade, caso.UnidadeNome,
                caso.TipoMamografia, RotuloTipo(caso.TipoMamografia), [], null,
                caso.SolicitanteDaFicha, [], [], Duplicidades: critica.DeOutroPedido);
        }

        var percurso = await PercorrerAsync(sessao, caso, critica.Html, cancellationToken);

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

        // A crítica vem ANTES do mapper de propósito: vincular o que já existe não pode depender
        // de a anamnese estar completa — o dado já está no SISCAN de qualquer jeito, e o que falta
        // do nosso lado é só o carimbo.
        var critica = await CriticarAsync(sessao, caso, cancellationToken);

        if (critica.Nossa is not null)
        {
            logger.LogWarning(
                "SISCAN[{Accession}]: requisição JÁ existia (protocolo {Protocolo}). Vinculando "
                + "em vez de criar outra.", caso.Exame.AccessionNumber, critica.Nossa.Protocolo);

            return await CarimbarAsync(
                caso.Exame, critica.Nossa.Protocolo, critica.Nossa.NumeroExame,
                "(já existia no SISCAN)", cancellationToken);
        }

        if (critica.DeOutroPedido.Count > 0)
        {
            var lista = string.Join(" · ", critica.DeOutroPedido.Take(3).Select(
                d => $"protocolo {d.Protocolo} ({d.Status}, {d.Unidade})"));

            throw new ConflitoException(
                "siscan.paciente_ja_tem_requisicao",
                $"Esta paciente já tem {critica.DeOutroPedido.Count} requisição(ões) de mamografia "
                + $"no SISCAN no último ano: {lista}. Resolva lá qual delas vale antes de criar "
                + "outra.");
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

        var percurso = await PercorrerAsync(sessao, caso, critica.Html, cancellationToken);

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
        var (de, ate) = JanelaDeDuplicidade(DateOnly.FromDateTime(DateTime.Today));
        if (caso.DataSolicitacao < de) de = caso.DataSolicitacao.AddDays(-1);

        // Aqui o menu é inevitável: estamos na tela do protocolo, não na de pesquisa.
        var (naGrade, _) = await PesquisarAsync(
            sessao, null, de, ate,
            new Dictionary<string, string> { ["frm:numeroProntuario"] = caso.Exame.AccessionNumber },
            cancellationToken);

        var numeroExame = naGrade.FirstOrDefault()?.NumeroExame ?? string.Empty;
        if (numeroExame.Length == 0)
        {
            logger.LogWarning(
                "SISCAN[{Accession}]: protocolo {Protocolo} criado, mas a releitura não achou a "
                + "linha na grade — o Nº do Exame fica em branco até alguém reabrir.",
                caso.Exame.AccessionNumber, protocolo);
        }

        return await CarimbarAsync(caso.Exame, protocolo, numeroExame, responsavel.Nome, cancellationToken);
    }

    // ------------------------------------------------------------------ percurso

    private sealed record Percurso(string Html, IHtmlDocument Doc);

    private async Task<Percurso> PercorrerAsync(
        ISiscanWebSessao sessao, Caso caso, string? htmlAberto,
        CancellationToken cancellationToken)
    {
        var relogio = Stopwatch.StartNew();

        async Task<string> Passo(string nome, Func<Task<string>> acao)
        {
            var antes = relogio.ElapsedMilliseconds;
            var html = await acao();
            var doc = SiscanHtml.Documento(html);

            // O título é o que revela em QUE tela o fluxo está. Foi a falta dele no log que fez o
            // bug do "Novo Exame" (resposta é tela inteira, não parcial) aparecer só lá na frente,
            // como "não consegui resolver o CNS".
            logger.LogInformation(
                "SISCAN[{Accession}]: {Passo} — {Ms} ms · tela {Titulos} · form frm? {TemForm}",
                caso.Exame.AccessionNumber, nome, relogio.ElapsedMilliseconds - antes,
                string.Join(" | ", SiscanHtml.Titulos(doc).Take(2)),
                doc.GetElementById("frm") is not null);

            return html;
        }

        // Clicar no menu custa de 14 a 32 segundos. Quando a crítica já deixou a tela de pesquisa
        // aberta, o assistente começa dali — o botão "Novo Exame" está nela.
        var html = htmlAberto ?? await Passo("abrir GERENCIAR EXAME",
            () => sessao.AbrirPorMenuAsync(SiscanWebSessao.MenuGerenciarExame, cancellationToken));

        html = await Passo("clicar Novo Exame",
            () => ClicarNovoExameAsync(sessao, html, cancellationToken));

        var htmlNovoExame = html;
        html = await Passo("digitar Cartão SUS",
            () => DigitarCartaoSusAsync(sessao, htmlNovoExame, caso, cancellationToken));

        var htmlCns = html;
        html = await Passo("marcar tipo de exame",
            () => MarcarTipoExameAsync(sessao, htmlCns, cancellationToken));

        var htmlTipo = html;
        html = await Passo("Avançar",
            () => AvancarAsync(sessao, htmlTipo, caso, cancellationToken));

        var htmlEtapa2 = html;
        html = await Passo($"marcar tipo de mamografia {caso.TipoMamografia}",
            () => MarcarTipoMamografiaAsync(sessao, htmlEtapa2, caso.TipoMamografia, cancellationToken));

        logger.LogInformation(
            "SISCAN[{Accession}]: percurso completo em {Ms} ms.",
            caso.Exame.AccessionNumber, relogio.ElapsedMilliseconds);

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

    private async Task<string> DigitarCartaoSusAsync(
        ISiscanWebSessao sessao, string html, Caso caso, CancellationToken cancellationToken)
    {
        var doc = SiscanHtml.Documento(html);

        // Conferir a TELA, não só o campo: a tela de pesquisa do Gerenciar Exame também tem um
        // `frm:cartaoSUS` (o filtro de busca). Sem esta checagem, estar na tela errada só aparece
        // lá na frente, como "não consegui resolver o CNS" — foi o bug de 23/09/2026.
        var titulos = SiscanHtml.Titulos(doc);
        if (doc.QuerySelector("input[name='frm:tipoExame']") is null)
        {
            logger.LogWarning(
                "SISCAN[{Accession}]: esperava a tela de Novo Exame e estou em {Titulos}.",
                caso.Exame.AccessionNumber, string.Join(" | ", titulos));

            throw new ValidacaoException(
                "siscan.tela_inesperada",
                "O SISCAN não abriu a tela de Novo Exame — o fluxo parou em "
                + $"'{string.Join(" | ", titulos.Take(2))}'.");
        }

        var campo = doc.QuerySelector("input[name='frm:cartaoSUS']")
                    ?? throw new ValidacaoException(
                        "siscan.tela_inesperada", "A tela de Novo Exame não trouxe o campo Cartão SUS.");

        // Só o `onblur` já traz o paciente do CADSUS inteiro — não é preciso clicar a lupa.
        var resultado = await sessao.SubmeterA4JAsync(
            html, SiscanHtml.FormPrincipal,
            new Dictionary<string, string> { ["frm:cartaoSUS"] = caso.Cns },
            SiscanHtml.ParametrosA4JDoElemento(campo), cancellationToken);

        var depois = SiscanHtml.Documento(resultado);
        var nome = SiscanHtml.ValorDoCampo(depois, "frm:nome");
        if (string.IsNullOrWhiteSpace(nome))
        {
            var mensagens = SiscanHtml.Mensagens(depois);
            logger.LogWarning(
                "SISCAN[{Accession}]: CNS não resolveu. tela={Titulos} · campo nome existe? {TemCampo} "
                + "· cartaoSUS devolvido={Devolvido} · mensagens={Mensagens}",
                caso.Exame.AccessionNumber, string.Join(" | ", SiscanHtml.Titulos(depois)),
                depois.QuerySelector("[name='frm:nome']") is not null,
                SiscanHtml.ValorDoCampo(depois, "frm:cartaoSUS"),
                string.Join(" · ", mensagens.Take(3)));

            throw new ValidacaoException(
                "siscan.paciente_nao_encontrado",
                mensagens.Count > 0
                    ? $"O SISCAN recusou o Cartão SUS: {string.Join(" · ", mensagens.Take(2))}"
                    : $"O CADSUS não encontrou o Cartão SUS {caso.Cns} pela tela do SISCAN.");
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
    private async Task<(List<SiscanHtml.LinhaExame> Linhas, string Html)> PesquisarAsync(
        ISiscanWebSessao sessao, string? htmlAberto, DateOnly de, DateOnly ate,
        IReadOnlyDictionary<string, string> filtros, CancellationToken cancellationToken)
    {
        var inicio = de.ToString("dd/MM/yyyy");
        var fim = ate.ToString("dd/MM/yyyy");
        var achadas = new List<SiscanHtml.LinhaExame>();

        // CLICAR NO MENU É A COISA CARA. Medido em produção e no laboratório em 23/09/2026: o
        // POST em /visao/index.jsf leva de 14 a 32 SEGUNDOS, enquanto cada pesquisa custa ~200 ms.
        // Abrir o menu por status fazia a crítica custar 47 s; abrindo uma vez e repesquisando na
        // própria página de resultado, custa 21 s — com resultado idêntico.
        var html = htmlAberto
                   ?? await sessao.AbrirPorMenuAsync(
                       SiscanWebSessao.MenuGerenciarExame, cancellationToken);

        foreach (var status in TodosOsStatus)
        {
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

            // A página de resultado É a tela de pesquisa com a grade preenchida: dá para pesquisar
            // de novo nela, e o botão "Novo Exame" continua lá. Por isso o `html` avança em vez de
            // ser descartado — é o que evita reabrir o menu.
            html = await sessao.SubmeterFormAsync(
                html, SiscanHtml.FormPrincipal, extras, cancellationToken);

            achadas.AddRange(SiscanHtml.Grade(SiscanHtml.Documento(html)));
        }

        return (achadas, html);
    }

    /// <summary>
    /// O que a crítica achou — e a página em que ela parou.
    ///
    /// <para><paramref name="Html"/> não é detalhe de implementação: é a tela de pesquisa já
    /// aberta, de onde o assistente continua sem pagar outro clique de menu (que custa dezenas de
    /// segundos).</para>
    /// </summary>
    private sealed record Critica(
        RequisicaoEncontradaDto? Nossa, IReadOnlyList<RequisicaoEncontradaDto> DeOutroPedido,
        string Html);

    /// <summary>
    /// Pergunta ao SISCAN se já existe requisição — primeiro pelo <b>Cartão SUS</b>, e só se achar
    /// alguma é que pergunta pelo Nº do Prontuário para saber se é deste pedido.
    ///
    /// <para><b>Por que nesta ordem.</b> Cada pergunta custa uma varredura nos três status, e a
    /// varredura custa segundos no SISCAN. No caminho comum — paciente sem nenhuma requisição —
    /// a resposta da primeira já encerra o assunto, e a segunda nem acontece. Perguntar pelo
    /// prontuário antes dobrava o custo de todo mundo para atender o caso raro.</para>
    ///
    /// <para><b>A janela pode ser alargada.</b> Quando a data de solicitação do pedido é anterior
    /// ao ano da janela (registro retroativo), a busca recua até ela — senão a requisição DESTE
    /// pedido ficaria fora do alcance e criaríamos uma segunda. O efeito colateral aceito: nesses
    /// casos, uma requisição antiga de outro pedido também bloqueia e pede olho humano.</para>
    /// </summary>
    private async Task<Critica> CriticarAsync(
        ISiscanWebSessao sessao, Caso caso, CancellationToken cancellationToken)
    {
        var relogio = Stopwatch.StartNew();
        var (de, ate) = JanelaDeDuplicidade(DateOnly.FromDateTime(DateTime.Today));
        if (caso.DataSolicitacao < de) de = caso.DataSolicitacao.AddDays(-1);

        var (doPaciente, html) = await PesquisarAsync(
            sessao, null, de, ate,
            new Dictionary<string, string> { ["frm:cartaoSUS"] = caso.Cns },
            cancellationToken);

        logger.LogInformation(
            "SISCAN[{Accession}]: crítica por CNS em {De}..{Ate} — {Achadas} requisição(ões) · {Ms} ms",
            caso.Exame.AccessionNumber, de, ate, doPaciente.Count, relogio.ElapsedMilliseconds);

        if (doPaciente.Count == 0) return new Critica(null, [], html);

        // Achou alguma: agora vale a pergunta cara — alguma delas é DESTE pedido? Continua na
        // MESMA página, que já é a tela de pesquisa.
        var (nossas, html2) = await PesquisarAsync(
            sessao, html, de, ate,
            new Dictionary<string, string> { ["frm:numeroProntuario"] = caso.Exame.AccessionNumber },
            cancellationToken);

        logger.LogInformation(
            "SISCAN[{Accession}]: crítica por prontuário — {Achadas} · {Ms} ms",
            caso.Exame.AccessionNumber, nossas.Count, relogio.ElapsedMilliseconds);

        if (nossas.Count > 0) return new Critica(Encontrada(nossas[0]), [], html2);

        return new Critica(null, doPaciente.Select(Encontrada).ToList(), html2);
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
