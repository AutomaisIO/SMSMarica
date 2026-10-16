using System.Text.Json;

namespace SMSMais.Core.Integracoes.SiscanWeb.Requisicao;

/// <summary>
/// O bloco "TIPO DE MAMOGRAFIA" da requisição, que a anamnese passou a reproduzir com a mesma
/// dinâmica da tela do SISCAN (16/10/2026).
///
/// <para>Até aqui o tipo saía só da idade, a população só dos critérios de risco, e a diagnóstica ia
/// vazia — o SISCAN recusava ("É obrigatório informar ao menos um dos campos de Diagnóstica"). O
/// mapa do bloco, medido sem gravar, está em <c>Automais.SISCAN/docs/FLUXO-NOVA-REQUISICAO.md</c>
/// §6b-2: o rastreamento pede uma população; a diagnóstica pede ao menos uma de 6 indicações, e cada
/// indicação abre o seu sub-bloco (até 103 campos).</para>
/// </summary>
public static partial class SiscanRequisicaoMapper
{
    /// <summary>Uma indicação da diagnóstica: chave na anamnese, checkbox no SISCAN, rótulo deles.</summary>
    public sealed record IndicacaoDiagnostica(string Chave, string Campo, string Rotulo);

    /// <summary>As 6 indicações, na ordem da tela. Cada checkbox é um A4J que desenha o sub-bloco.</summary>
    public static readonly IReadOnlyList<IndicacaoDiagnostica> IndicacoesDiagnostica =
    [
        new("achadosExameClinico", "frm:achadosExameClinico", "Achados no exame clínico"),
        new("controleRadiologico", "frm:controleRadiologicoLesao", "Controle radiológico de lesão categoria 3"),
        new("lesaoDiagnosticoCancer", "frm:lesaoDiagnosticoCancer", "Lesão com diagnóstico de câncer"),
        new("respostaQuimioterapia", "frm:avaliacaoRespostaQuimioterapia",
            "Avaliação da resposta de quimioterapia neoadjuvante"),
        new("revisaoMamografia", "frm:revisaoMamografia",
            "Revisão de mamografia com lesão realizada em outra instituição"),
        new("controleAposBiopsia", "frm:controleLesao",
            "Controle de lesão após biópsia de fragmento ou PAAF com resultado benigno"),
    ];

    /// <summary>
    /// O que o navegador manda num checkbox SEM atributo <c>value</c> — é assim que as 6 indicações
    /// vêm na tela, e o JSF lê "on" como marcado.
    /// </summary>
    public const string Marcado = "on";

    private static readonly (string Lado, string Sufixo)[] Lados = [("direita", "Direita"), ("esquerda", "Esquerda")];

    /// <summary>Os 7 achados da grade que 3 indicações repetem, × 2 mamas. "Axiliar" é o nome deles.</summary>
    private static readonly (string Chave, string Campo, string Rotulo)[] AchadosRadiologicos =
    [
        ("nodulo", "Nodulo", "nódulo"),
        ("microcalcificacoes", "Microcalcificacoes", "microcalcificações"),
        ("assimetriaFocal", "AssimetriaFocal", "assimetria focal"),
        ("assimetriaDifusa", "AssimetriaDifusa", "assimetria difusa"),
        ("areaDensa", "AreaDensa", "área densa"),
        ("distorcaoFocal", "DistorcaoFocal", "distorção focal"),
        ("linfonodoAxilar", "LinfonodoAxiliar", "linfonodo axilar"),
    ];

    /// <summary>Prefixo dos campos da grade de achados, por indicação.</summary>
    private static readonly Dictionary<string, string> PrefixoDaGrade = new(StringComparer.Ordinal)
    {
        ["controleRadiologico"] = "frm:radiologico",
        ["lesaoDiagnosticoCancer"] = "frm:lesaoCancer",
        ["controleAposBiopsia"] = "frm:controleLesao",
    };

    /// <summary>As 10 regiões da mama do exame clínico, com o código da tela deles.</summary>
    private static readonly (string Sigla, string Codigo)[] Regioes =
    [
        ("QSL", "01"), ("QIL", "02"), ("QSM", "03"), ("QIM", "04"), ("UQLat", "05"),
        ("UQsup", "06"), ("UQmed", "07"), ("UQinf", "08"), ("RRA", "09"), ("PA", "10"),
    ];

    private static readonly string[] CategoriasBiRads = ["0", "3", "4", "5"];

    /// <summary>Todo campo que a diagnóstica pode mandar — é o que a geração confere na tela antes do Salvar.</summary>
    private static readonly HashSet<string> NomesDaDiagnostica = MontarNomesDaDiagnostica();

    public static bool EhCampoDaDiagnostica(string campo) => NomesDaDiagnostica.Contains(campo);

    /// <summary>
    /// O tipo que vai ao SISCAN: o que a anamnese escolheu (<c>siscan.tipoMamografia</c>) ou, sem
    /// escolha, a régua da idade. Na tela do SISCAN o tipo é escolha de quem preenche; a idade é a
    /// sugestão do CDT — e é o que vale para toda anamnese anterior a 16/10/2026.
    /// </summary>
    public static (string Tipo, bool PelaIdade) TipoMamografiaEfetivo(
        string? conteudoJson, DateOnly nascimento, DateOnly referencia)
    {
        var escolhido = Texto(Objeto(Raiz(conteudoJson), "siscan"), "tipoMamografia") switch
        {
            "diagnostica" => Diagnostica,
            "rastreamento" => Rastreamento,
            _ => null,
        };

        return escolhido is null
            ? (TipoMamografiaPorIdade(nascimento, referencia), true)
            : (escolhido, false);
    }

    /// <summary>
    /// "MAMOGRAFIA DE RASTREAMENTO *": a população escolhida na anamnese ou, sem escolha, a que os
    /// critérios de risco indicam.
    ///
    /// <para><b>A 02 (risco elevado) só vale com "Apresenta risco elevado" = Sim.</b> Na tela do
    /// SISCAN ela nasce desabilitada e só habilita com esse Sim (medido em 16/10/2026). O servidor
    /// deles grava a 02 mesmo assim — e foi o que aconteceu em 32 requisições até aqui: uma ficha que
    /// nenhuma pessoa consegue montar na tela.</para>
    ///
    /// <para>Sem escolha na anamnese, a 02 travada vira <b>população alvo</b> — é o que a tela deixa
    /// marcar e o que a operação marcava à mão (39 de 39 relidas, 16/10/2026). Escolhida
    /// explicitamente com o risco em Não/Não sabe, é contradição de quem preencheu: lacuna.</para>
    /// </summary>
    private static void MontarRastreamento(
        JsonElement risco, JsonElement siscan,
        List<KeyValuePair<string, string>> campos, List<LacunaAnamnese> lacunas)
    {
        var escolhida = Texto(siscan, "populacaoRastreamento") switch
        {
            "alvo" => "01",
            "riscoElevado" => "02",
            "jaTratada" => "03",
            _ => null,
        };
        var populacao = escolhida ?? PopulacaoRastreamento(risco);

        if (populacao == "02" && RiscoElevado(risco, siscan) != Sim)
        {
            if (escolhida is null)
            {
                populacao = "01";
            }
            else
            {
                lacunas.Add(new LacunaAnamnese(
                    "siscan.populacaoRastreamento",
                    "População do rastreamento: \"risco elevado (história familiar)\" só pode ser marcada "
                    + "quando \"Apresenta risco elevado\" é Sim — como na tela do SISCAN. Escolha outra "
                    + "população ou revise o risco."));
                return;
            }
        }

        campos.Add(new(CampoPopulacaoRastreamento, populacao));
    }

    /// <summary>
    /// "DIAGNÓSTICA *": cada indicação marcada na anamnese vai com o seu sub-bloco.
    ///
    /// <para>Duas lacunas, as duas de "não inventar": nenhuma indicação (o SISCAN recusa) e indicação
    /// marcada sem nada dentro — "achados no exame clínico" sem achado nenhum não informa coisa
    /// alguma, e escolher um por ela seria inventar o exame.</para>
    /// </summary>
    private static void MontarDiagnostica(
        JsonElement siscan, List<KeyValuePair<string, string>> campos, List<LacunaAnamnese> lacunas)
    {
        var diagnostica = Objeto(siscan, "diagnostica");
        var marcadas = 0;

        foreach (var indicacao in IndicacoesDiagnostica)
        {
            var bloco = Objeto(diagnostica, indicacao.Chave);
            if (Booleano(bloco, "marcada") != true) continue;

            marcadas++;
            var detalhes = DetalhesDaIndicacao(indicacao.Chave, bloco);
            if (detalhes.Count == 0)
            {
                lacunas.Add(new LacunaAnamnese(
                    $"siscan.diagnostica.{indicacao.Chave}",
                    $"Diagnóstica — {indicacao.Rotulo}: a indicação está marcada, mas sem nada "
                    + "dentro dela (mama, achado ou categoria)."));
                continue;
            }

            campos.Add(new(indicacao.Campo, Marcado));
            campos.AddRange(detalhes);
        }

        if (marcadas == 0)
        {
            lacunas.Add(new LacunaAnamnese(
                "siscan.diagnostica",
                "Mamografia diagnóstica: marque ao menos uma indicação na seção 7 — o SISCAN recusa "
                + "sem nenhuma (\"É obrigatório informar ao menos um dos campos de Diagnóstica\")."));
        }
    }

    private static List<KeyValuePair<string, string>> DetalhesDaIndicacao(string chave, JsonElement bloco)
    {
        var detalhes = new List<KeyValuePair<string, string>>();

        switch (chave)
        {
            case "achadosExameClinico":
                foreach (var (lado, sufixo) in Lados)
                {
                    var mama = Objeto(bloco, lado);
                    if (Booleano(mama, "lesaoPapilar") == true)
                        detalhes.Add(new($"frm:lesaoPapilar{sufixo}", "S"));

                    var descarga = Texto(mama, "descargaPapilar") switch
                    {
                        "cristalina" => "01",
                        "hemorragica" => "02",
                        _ => null,
                    };
                    // A descarga da mama direita é o único campo do bloco SEM o sufixo do lado.
                    if (descarga is not null)
                        detalhes.Add(new(lado == "direita" ? "frm:descargaPapilar" : "frm:descargaPapilarEsquerda", descarga));

                    foreach (var codigo in CodigosDeRegiao(mama, "nodulo"))
                        detalhes.Add(new($"frm:noduloLocalizacao{sufixo}", codigo));

                    // "Lcalizacao" (sic) é o nome do campo deles.
                    foreach (var codigo in CodigosDeRegiao(mama, "espessamento"))
                        detalhes.Add(new($"frm:espessamentoLcalizacao{sufixo}", codigo));

                    foreach (var item in Lista(mama, "linfonodo"))
                    {
                        var codigo = (item.ValueKind == JsonValueKind.String ? item.GetString() : null) switch
                        {
                            "axilar" => "01",
                            "supraclavicular" => "02",
                            _ => null,
                        };
                        if (codigo is not null) detalhes.Add(new($"frm:linfonodoPalpavel{sufixo}", codigo));
                    }
                }

                break;

            case "controleRadiologico" or "lesaoDiagnosticoCancer" or "controleAposBiopsia":
                var achados = Objeto(bloco, "achados");
                foreach (var (achado, campo, _) in AchadosRadiologicos)
                {
                    foreach (var (lado, sufixo) in Lados)
                    {
                        if (Booleano(Objeto(achados, achado), lado) == true)
                            detalhes.Add(new($"{PrefixoDaGrade[chave]}{campo}{sufixo}", "S"));
                    }
                }

                break;

            case "respostaQuimioterapia":
                // A mesma inversão da radioterapia, medida: 01 = esquerda, 02 = direita.
                var ladoQuimio = Texto(bloco, "lado") switch
                {
                    "esquerda" => "01",
                    "direita" => "02",
                    "ambas" => "03",
                    _ => null,
                };
                if (ladoQuimio is not null) detalhes.Add(new("frm:avaliacaoRespostaQuimiNeo", ladoQuimio));
                break;

            case "revisaoMamografia":
                // Uma categoria BI-RADS por mama (na tela, marcar uma desmarca as outras do lado).
                foreach (var (_, sufixo) in Lados)
                {
                    var categoria = Texto(bloco, $"categoria{sufixo}");
                    if (categoria is not null && CategoriasBiRads.Contains(categoria))
                        detalhes.Add(new($"frm:revisaoCategoria{categoria}{sufixo}", "S"));
                }

                break;
        }

        return detalhes;
    }

    /// <summary>Siglas da anamnese ("QSL", "RRA"…) → códigos da tela, na ordem da tela, sem repetir.</summary>
    private static IEnumerable<string> CodigosDeRegiao(JsonElement mama, string nome)
    {
        var siglas = Lista(mama, nome)
            .Where(i => i.ValueKind == JsonValueKind.String)
            .Select(i => i.GetString())
            .ToHashSet(StringComparer.Ordinal);

        return Regioes.Where(r => siglas.Contains(r.Sigla)).Select(r => r.Codigo);
    }

    private static HashSet<string> MontarNomesDaDiagnostica()
    {
        var nomes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var indicacao in IndicacoesDiagnostica) nomes.Add(indicacao.Campo);

        foreach (var (_, sufixo) in Lados)
        {
            nomes.Add($"frm:lesaoPapilar{sufixo}");
            nomes.Add($"frm:noduloLocalizacao{sufixo}");
            nomes.Add($"frm:espessamentoLcalizacao{sufixo}");
            nomes.Add($"frm:linfonodoPalpavel{sufixo}");
            foreach (var categoria in CategoriasBiRads) nomes.Add($"frm:revisaoCategoria{categoria}{sufixo}");
            foreach (var prefixo in PrefixoDaGrade.Values)
            {
                foreach (var (_, campo, _) in AchadosRadiologicos) nomes.Add($"{prefixo}{campo}{sufixo}");
            }
        }

        nomes.Add("frm:descargaPapilar");
        nomes.Add("frm:descargaPapilarEsquerda");
        nomes.Add("frm:avaliacaoRespostaQuimiNeo");
        return nomes;
    }

    /// <summary>
    /// Pergunta e resposta legíveis de um campo da diagnóstica, para a prévia do modal conferir antes
    /// de mandar. Null = não é campo da diagnóstica.
    /// </summary>
    public static (string Pergunta, string Resposta)? RotularDiagnostica(string campo, string valor)
    {
        var indicacao = IndicacoesDiagnostica.FirstOrDefault(i => i.Campo == campo);
        if (indicacao is not null) return ("Diagnóstica — indicação", indicacao.Rotulo);

        if (campo == "frm:avaliacaoRespostaQuimiNeo")
        {
            return ("Resposta à quimioterapia neoadjuvante — mama",
                valor switch { "01" => "esquerda", "02" => "direita", _ => "ambas" });
        }

        foreach (var (lado, sufixo) in Lados)
        {
            var achados = $"Achados no exame clínico — mama {lado}";
            if (campo == $"frm:lesaoPapilar{sufixo}") return (achados, "lesão papilar");
            if (campo == (lado == "direita" ? "frm:descargaPapilar" : "frm:descargaPapilarEsquerda"))
                return (achados, valor == "02" ? "descarga papilar hemorrágica" : "descarga papilar cristalina");
            if (campo == $"frm:noduloLocalizacao{sufixo}") return (achados, $"nódulo {Sigla(valor)}");
            if (campo == $"frm:espessamentoLcalizacao{sufixo}") return (achados, $"espessamento {Sigla(valor)}");
            if (campo == $"frm:linfonodoPalpavel{sufixo}")
                return (achados, valor == "02" ? "linfonodo palpável supraclavicular" : "linfonodo palpável axilar");

            foreach (var categoria in CategoriasBiRads)
            {
                if (campo == $"frm:revisaoCategoria{categoria}{sufixo}")
                    return ($"Revisão de mamografia — mama {lado}", $"BI-RADS categoria {categoria}");
            }

            foreach (var (chave, prefixo) in PrefixoDaGrade)
            {
                foreach (var (_, nome, rotulo) in AchadosRadiologicos)
                {
                    if (campo == $"{prefixo}{nome}{sufixo}")
                    {
                        var dona = IndicacoesDiagnostica.First(i => i.Chave == chave);
                        return ($"{dona.Rotulo} — mama {lado}", rotulo);
                    }
                }
            }
        }

        return null;
    }

    private static string Sigla(string codigo) =>
        Regioes.FirstOrDefault(r => r.Codigo == codigo).Sigla ?? codigo;
}
