using System.Security.Cryptography;
using System.Text;

namespace SMSMais.Core.Alertas;

/// <summary>Uma fonte de aviso conhecida de antemão (a tela lista mesmo que nunca tenha ocorrido).</summary>
public sealed record OrigemConhecida(string Chave, string Rotulo, string Grupo, string Descricao);

/// <summary>
/// O que é reportado ao celular. Duas portas de entrada:
/// <list type="number">
/// <item><b>Fontes explícitas</b> (abaixo): pontos do código que chamam o aviso de propósito —
/// o robô, a conta da IA, o erro 500 novo, os sincronismos com provedor.</item>
/// <item><b>Captura do log</b>: todo <c>LogError</c>/<c>LogCritical</c> do código da plataforma
/// vira uma fonte <c>log:*</c> na primeira ocorrência. É o que cumpre "qualquer erro que eu for
/// acrescentando TEM que ser reportado": erro novo já nasce reportado, sem lembrar de ligar.</item>
/// </list>
/// </summary>
public static class AlertaCatalogo
{
    public const string RoboFalha = "robo.falha";
    public const string IaConta = "ia.conta";
    public const string Erro500 = "sistema.erro_500";
    public const string ServicoParou = "sistema.servico_parou";
    public const string Teste = "sistema.teste";

    public static string Sincronismo(string provedor) => $"sincronismo.{provedor.ToLowerInvariant()}";

    public static readonly IReadOnlyList<OrigemConhecida> Conhecidas =
    [
        new(RoboFalha, "Robô de atendimento", "Robô",
            "O robô não conseguiu responder um cidadão: a IA recusou, o modelo deu erro ou a "
            + "tarefa esgotou as tentativas. Quem escreveu fica sem resposta até alguém assumir."),
        new(IaConta, "Conta da IA (Anthropic)", "IA",
            "A Anthropic recusou a chamada por crédito esgotado, limite de gasto atingido, chave "
            + "inválida ou falta de permissão. Para o robô, o treinamento, a distribuição de translados e a varredura "
            + "de contato negado de uma vez."),
        new(Erro500, "Erro não tratado na API (ERRO-XXXXXX)", "Sistema",
            "Primeira ocorrência de um erro 500 novo. Repetição do mesmo erro ainda em aberto "
            + "não gera aviso; reincidência de erro já resolvido gera (é regressão)."),
        new(ServicoParou, "Serviço em segundo plano parou", "Sistema",
            "Um motor em segundo plano morreu com exceção e não volta sozinho até o próximo "
            + "restart do servidor."),
        new(Sincronismo("sisreg"), "Sincronismo SISREG", "Sincronismo",
            "CAPTCHA, credencial derrubada, unidade com erro e rodada do lote de mapeamento que "
            + "terminou com pendência."),
        new(Sincronismo("ser"), "Sincronismo SER", "Sincronismo",
            "Falhas avisadas pelo motor do SER."),
        new(Sincronismo("sernit"), "Sincronismo SERNIT", "Sincronismo",
            "Falhas avisadas pelo motor do SERNIT."),
        new(Teste, "Mensagem de teste", "Sistema",
            "Disparada pelo botão \"Enviar teste\" desta tela."),
    ];

    public static OrigemConhecida? Buscar(string chave) =>
        Conhecidas.FirstOrDefault(o => string.Equals(o.Chave, chave, StringComparison.Ordinal));

    /// <summary>Descrição das fontes <c>log:*</c> (as descobertas pela captura do log).</summary>
    public const string DescricaoLog =
        "Erro gravado no log por este componente. Entrou sozinho na lista na primeira ocorrência.";

    // ---------- captura do log ----------

    private const string CategoriaHost = "Microsoft.Extensions.Hosting.Internal.Host";

    /// <summary>
    /// Categorias que NÃO passam pela captura: as que já avisam por conta própria (o middleware
    /// manda o ERRO-XXXXXX com código) e as que formam o próprio caminho do aviso — se o
    /// WhatsApp está quebrado, avisar pelo WhatsApp que ele quebrou só gera laço.
    ///
    /// <para>Do WhatsApp, só o CLIENTE de envio fica fora (é por ele que o aviso sai). Antes o
    /// namespace inteiro ficava — e com ele o webhook e os manipuladores das conversas: erro no
    /// fluxo que responde o cidadão não chegava a ninguém.</para>
    /// </summary>
    private static readonly string[] Excluidas =
    [
        "SMSMais.Core.Alertas",
        "SMSMais.Api.Alertas",
        "SMSMais.Api.Middleware.ExceptionHandlingMiddleware",
        "SMSMais.Core.Notificacoes.WhatsApp.WhatsAppCliente",
        "SMSMais.Core.Notificacoes.Sincronismo",
    ];

    public static bool CapturarDoLog(string categoria)
    {
        if (categoria == CategoriaHost) return true;
        if (!categoria.StartsWith("SMSMais.", StringComparison.Ordinal)) return false;
        return !Excluidas.Any(e => categoria.StartsWith(e, StringComparison.Ordinal));
    }

    /// <summary>
    /// Fonte de um evento do log. A chave inclui o modelo da mensagem (não a mensagem renderizada),
    /// então "falha na varredura da unidade {Unidade}" é UMA fonte para todas as unidades — e
    /// dá para silenciar uma mensagem sem calar o componente inteiro.
    /// </summary>
    public static (string Chave, string Rotulo, string Grupo) OrigemDoLog(string categoria, string modeloMensagem)
    {
        if (categoria == CategoriaHost)
            return (ServicoParou, "Serviço em segundo plano parou", "Sistema");

        var classe = categoria[(categoria.LastIndexOf('.') + 1)..];
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(modeloMensagem)))[..8];
        return ($"log:{classe}:{hash}", classe, GrupoDaCategoria(categoria));
    }

    private static string GrupoDaCategoria(string c)
    {
        if (c.Contains(".SisregWeb.", StringComparison.Ordinal) || c.Contains(".Sisreg", StringComparison.Ordinal))
            return "Sincronismo SISREG";
        if (c.Contains(".Sernit", StringComparison.Ordinal)) return "Sincronismo SERNIT";
        if (c.Contains(".SerWeb.", StringComparison.Ordinal) || c.Contains(".Ser.", StringComparison.Ordinal))
            return "Sincronismo SER";
        if (c.Contains(".Pep.", StringComparison.Ordinal)) return "Sincronismo PEP";
        if (c.Contains(".Worklist.", StringComparison.Ordinal) || c.Contains(".Exames.", StringComparison.Ordinal)
            || c.Contains(".Pacs", StringComparison.Ordinal))
            return "PACS / Worklist";
        if (c.Contains(".RoboAtendimento.", StringComparison.Ordinal)) return "Robô";
        if (c.Contains(".Notificacoes.", StringComparison.Ordinal) || c.Contains(".PesquisasSatisfacao.", StringComparison.Ordinal))
            return "Comunicação";
        return "Sistema";
    }
}
