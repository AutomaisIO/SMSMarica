using System.Globalization;
using System.Text.Json;

namespace SMSMais.Core.Integracoes.SiscanWeb.Requisicao;

/// <summary>Uma resposta que falta na anamnese e que o SISCAN exige — a tela mostra e pede.</summary>
public sealed record LacunaAnamnese(string Campo, string Pergunta);

/// <summary>
/// O que vai no POST do Salvar, já pronto, mais o que faltou.
///
/// <para>É uma LISTA de pares, não um dicionário: "tem nódulo" é um grupo de checkbox e pode ir
/// com as duas mamas marcadas — <c>frm:localizacaoNodulo=01&amp;frm:localizacaoNodulo=02</c>.
/// Um dicionário perderia a segunda mama em silêncio.</para>
/// </summary>
public sealed record CamposRequisicao(
    List<KeyValuePair<string, string>> Campos,
    List<LacunaAnamnese> Lacunas);

/// <summary>
/// Traduz a anamnese (questionário <c>mamografia</c> v2) para os campos da requisição do SISCAN.
///
/// <para><b>Regra que atravessa tudo: não inventar.</b> Onde o SISCAN oferece "Não Sabe" e nós não
/// perguntamos, vai "Não Sabe" — é uma resposta de verdade, não um buraco tapado. Onde ele exige
/// Sim ou Não e a anamnese não respondeu, isto devolve uma <see cref="LacunaAnamnese"/> e a
/// requisição não é gerada: afirmar "não fez cirurgia" sem ter perguntado é escrever mentira em
/// base federal de rastreamento de câncer.</para>
///
/// <para>Códigos medidos contra o SISCAN real (ver <c>Automais.SISCAN/docs/</c>), não deduzidos.</para>
/// </summary>
public static class SiscanRequisicaoMapper
{
    // Os nomes dos campos são auto-gerados pelo JSF (`j_idNN`) e mudam quando o DATASUS
    // recompila. Ficam aqui porque a tela não oferece outro jeito de endereçá-los — e por isso
    // cada geração confere o rótulo antes de mandar (ver SiscanRequisicaoService).
    public const string CampoNodulo = "frm:localizacaoNodulo";
    public const string CampoRiscoElevado = "frm:j_id91";
    public const string CampoMamasExaminadas = "frm:mamasExaminadas";
    public const string CampoFezMamografia = "frm:j_id105";
    public const string CampoRadioterapia = "frm:j_id121";
    public const string CampoLocalRadioterapia = "frm:j_id128";
    public const string CampoCirurgia = "frm:j_id151";
    public const string CampoTipoMamografia = "frm:j_id242";
    public const string CampoPopulacaoRastreamento = "frm:tipoMamografiaRastreamento";
    public const string CampoDataSolicitacao = "frm:dataSolicitacaoInputDate";
    public const string CampoProntuario = "frm:prontuario";
    public const string CampoAnoUltimaMamografia = "frm:anoUltimaMamografia";
    public const string CampoResponsavel = "frm:responsavelColeta";

    public const string Sim = "01";
    public const string Nao = "02";
    public const string NaoSabe = "03";

    public const string Diagnostica = "01";
    public const string Rastreamento = "02";

    /// <summary>
    /// Régua do CDT Maricá (22/09/2026): <b>≥ 36 anos = rastreamento, &lt; 36 = diagnóstica</b>.
    ///
    /// <para>Não é o critério do INCA (50–69 para rastreamento populacional) — é a régua operacional
    /// daqui. Consequência que não é óbvia: como a lista de Responsável do SISCAN <b>muda</b> entre
    /// diagnóstica e rastreamento, a idade da paciente também decide quem pode assinar.</para>
    /// </summary>
    public static string TipoMamografiaPorIdade(DateOnly nascimento, DateOnly referencia)
    {
        var idade = referencia.Year - nascimento.Year;
        if (referencia < nascimento.AddYears(idade)) idade--;
        return idade >= 36 ? Rastreamento : Diagnostica;
    }

    /// <param name="dataDoExame">
    /// Vai no campo que o SISCAN chama de <b>"Data da Solicitação"</b> — mas o que se grava ali é
    /// a data em que o exame foi FEITO, não a da ficha do SISREG (decisão do Bernardo, 23/09/2026).
    /// Quem resolve de onde ela sai é <c>SiscanRequisicaoService.ResolverDataDoExame</c>.
    /// </param>
    public static CamposRequisicao Montar(
        string? conteudoJson, string prontuario, DateOnly dataDoExame, string tipoMamografia)
    {
        var campos = new List<KeyValuePair<string, string>>();
        var lacunas = new List<LacunaAnamnese>();

        JsonElement raiz;
        try
        {
            raiz = JsonDocument.Parse(conteudoJson ?? "{}").RootElement.Clone();
        }
        catch (JsonException)
        {
            raiz = JsonDocument.Parse("{}").RootElement.Clone();
        }

        var historico = Objeto(raiz, "historicoClinico");
        var queixas = Objeto(raiz, "queixas");
        var risco = Objeto(raiz, "avaliacaoRisco");
        var siscan = Objeto(raiz, "siscan");

        campos.Add(new(CampoProntuario, prontuario));
        campos.Add(new(CampoDataSolicitacao, dataDoExame.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
        campos.Add(new(CampoTipoMamografia, tipoMamografia));

        MontarNodulo(queixas, campos);
        campos.Add(new(CampoRiscoElevado, RiscoElevado(risco)));
        campos.Add(new(CampoMamasExaminadas, MamasExaminadas(siscan)));
        MontarMamografiaAnterior(historico, siscan, campos);
        MontarRadioterapia(siscan, campos);
        MontarCirurgia(historico, siscan, campos, lacunas);

        if (tipoMamografia == Rastreamento)
        {
            campos.Add(new(CampoPopulacaoRastreamento, PopulacaoRastreamento(risco)));
        }

        return new CamposRequisicao(campos, lacunas);
    }

    // ------------------------------------------------------------------ perguntas

    /// <summary>
    /// "TEM NÓDULO OU CAROÇO NA MAMA?" — 01 direita · 02 esquerda · 04 não. As duas mamas podem ir
    /// juntas; nenhuma marcada é "Não", que é o que a tela significa quando a enfermeira não marca.
    /// </summary>
    private static void MontarNodulo(JsonElement queixas, List<KeyValuePair<string, string>> campos)
    {
        var nodulo = Objeto(Objeto(queixas, "sintomas"), "noduloPalpavel");
        var direita = Booleano(nodulo, "direita") == true;
        var esquerda = Booleano(nodulo, "esquerda") == true;

        if (direita) campos.Add(new(CampoNodulo, "01"));
        if (esquerda) campos.Add(new(CampoNodulo, "02"));
        if (!direita && !esquerda) campos.Add(new(CampoNodulo, "04"));
    }

    /// <summary>
    /// "APRESENTA RISCO ELEVADO PARA CÂNCER DE MAMA?"
    ///
    /// <para>A nossa régua tem três níveis (Baixo/Moderado/Alto) e a do SISCAN tem Sim/Não/Não
    /// Sabe. Decisão do Bernardo (22/09/2026): <b>Moderado vira Sim</b> — acima de Baixo entra
    /// como risco elevado. Quando a classificação não foi preenchida, valem os quatro critérios
    /// objetivos; se nem eles foram respondidos, é "Não Sabe".</para>
    /// </summary>
    private static string RiscoElevado(JsonElement risco)
    {
        var classificacao = Texto(risco, "classificacao");
        if (classificacao is "Alto" or "Moderado") return Sim;
        if (classificacao == "Baixo") return Nao;

        var criterios = new[]
        {
            "familiar1GrauCancerMama", "cancerMamaAntes50Familia",
            "historicoPessoalCancer", "mutacaoGeneticaConhecida",
        }.Select(c => Booleano(risco, c)).ToList();

        if (criterios.Any(c => c == true)) return Sim;
        return criterios.All(c => c == false) ? Nao : NaoSabe;
    }

    /// <summary>
    /// "ANTES DESTA CONSULTA, TEVE AS MAMAS EXAMINADAS POR UM PROFISSIONAL DE SAÚDE?"
    /// Pergunta que só existe na v2 — anamnese antiga responde honestamente "Não Sabe".
    /// </summary>
    private static string MamasExaminadas(JsonElement siscan) => Texto(siscan, "mamasExaminadasAntes") switch
    {
        "sim" => "01",
        "nunca" => "02",
        _ => NaoSabe,
    };

    /// <summary>"FEZ MAMOGRAFIA ALGUMA VEZ?" — Sim abre o ano da última.</summary>
    private static void MontarMamografiaAnterior(
        JsonElement historico, JsonElement siscan, List<KeyValuePair<string, string>> campos)
    {
        var fez = Booleano(Objeto(historico, "jaRealizouMamografia"), "resposta");

        campos.Add(new(CampoFezMamografia, fez switch
        {
            true => Sim,
            false => Nao,
            _ => NaoSabe,
        }));

        var ano = Texto(siscan, "anoUltimaMamografia");
        if (fez == true && !string.IsNullOrWhiteSpace(ano))
        {
            campos.Add(new(CampoAnoUltimaMamografia, ano.Trim()));
        }
    }

    /// <summary>
    /// "FEZ RADIOTERAPIA NA MAMA OU NO PLASTRÃO?" — Sim abre a localização, e a localização abre o
    /// ano de cada lado. Três níveis, não dois.
    ///
    /// <para>Atenção ao código da localização, que é contraintuitivo e foi medido:
    /// <b>01 = Esquerda, 02 = Direita</b>, 03 = Ambas.</para>
    /// </summary>
    private static void MontarRadioterapia(JsonElement siscan, List<KeyValuePair<string, string>> campos)
    {
        var radio = Objeto(siscan, "radioterapia");
        var resposta = Texto(radio, "resposta");

        campos.Add(new(CampoRadioterapia, resposta switch
        {
            "sim" => Sim,
            "nao" => Nao,
            _ => NaoSabe,
        }));

        if (resposta != "sim") return;

        var lado = Texto(radio, "lado");
        var codigo = lado switch { "esquerda" => "01", "direita" => "02", "ambas" => "03", _ => null };
        if (codigo is null) return;

        campos.Add(new(CampoLocalRadioterapia, codigo));

        var anoDireita = Texto(radio, "anoDireita");
        var anoEsquerda = Texto(radio, "anoEsquerda");
        if (lado is "direita" or "ambas" && !string.IsNullOrWhiteSpace(anoDireita))
        {
            campos.Add(new("frm:anoRadioterapiaDireita", anoDireita.Trim()));
        }

        if (lado is "esquerda" or "ambas" && !string.IsNullOrWhiteSpace(anoEsquerda))
        {
            campos.Add(new("frm:anoRadioterapiaEsquerda", anoEsquerda.Trim()));
        }
    }

    /// <summary>
    /// "FEZ CIRURGIA DE MAMA?" — S/N, sem "não sabe". Sim abre 26 campos de ano (13 tipos × 2
    /// lados), e o nome de cada um é <c>frm:ano{Tipo}{Lado}</c>.
    ///
    /// <para>Sem resposta na anamnese, isto vira <b>lacuna</b>: o SISCAN não tem "Não Sabe" aqui, e
    /// mandar "Não" sem ter perguntado é afirmar o que ninguém apurou.</para>
    /// </summary>
    private static void MontarCirurgia(
        JsonElement historico, JsonElement siscan,
        List<KeyValuePair<string, string>> campos, List<LacunaAnamnese> lacunas)
    {
        var fezCirurgia = Booleano(Objeto(historico, "jaRealizouCirurgiaMamaria"), "resposta");
        var protese = Booleano(Objeto(historico, "possuiProteseMamaria"), "resposta");

        // Prótese é uma cirurgia de mama para o SISCAN ("inclusão de implantes"): quem respondeu
        // que tem prótese já respondeu que fez cirurgia, mesmo sem marcar a outra pergunta.
        var teve = fezCirurgia == true || protese == true;

        if (!teve && fezCirurgia is null && protese is null)
        {
            lacunas.Add(new LacunaAnamnese(
                "historicoClinico.jaRealizouCirurgiaMamaria",
                "Já realizou cirurgia mamária? O SISCAN exige Sim ou Não — não existe "
                + "\"não sabe\" nessa pergunta."));
            return;
        }

        campos.Add(new(CampoCirurgia, teve ? "S" : "N"));
        if (!teve) return;

        foreach (var cirurgia in Lista(siscan, "cirurgias"))
        {
            var tipo = Texto(cirurgia, "tipo");
            var lado = Texto(cirurgia, "lado");
            var ano = Texto(cirurgia, "ano");
            if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(ano)) continue;

            var sufixo = lado == "esquerda" ? "Esquerda" : "Direita";
            campos.Add(new($"frm:ano{Capitalizar(tipo)}{sufixo}", ano.Trim()));
        }
    }

    /// <summary>
    /// População-alvo do rastreamento: 01 alvo · 02 risco elevado (história familiar) ·
    /// 03 já tratada de câncer de mama. Sai dos critérios da seção 5 — histórico pessoal manda.
    /// </summary>
    private static string PopulacaoRastreamento(JsonElement risco)
    {
        if (Booleano(risco, "historicoPessoalCancer") == true) return "03";
        if (Booleano(risco, "familiar1GrauCancerMama") == true
            || Booleano(risco, "cancerMamaAntes50Familia") == true)
        {
            return "02";
        }

        return "01";
    }

    // ------------------------------------------------------------------ JSON defensivo

    private static JsonElement Objeto(JsonElement pai, string nome) =>
        pai.ValueKind == JsonValueKind.Object && pai.TryGetProperty(nome, out var filho)
            ? filho
            : default;

    private static IEnumerable<JsonElement> Lista(JsonElement pai, string nome)
    {
        var lista = Objeto(pai, nome);
        return lista.ValueKind == JsonValueKind.Array ? lista.EnumerateArray() : [];
    }

    private static string? Texto(JsonElement pai, string nome)
    {
        var valor = Objeto(pai, nome);
        return valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
    }

    private static bool? Booleano(JsonElement pai, string nome)
    {
        var valor = Objeto(pai, nome);
        return valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    /// <summary>`biopsiaCirurgicaIncisional` → `BiopsiaCirurgicaIncisional`, que é como o SISCAN
    /// nomeia o campo. A chave do nosso questionário foi escolhida para espelhar a deles.</summary>
    private static string Capitalizar(string chave) =>
        chave.Length == 0 ? chave : char.ToUpperInvariant(chave[0]) + chave[1..];
}
