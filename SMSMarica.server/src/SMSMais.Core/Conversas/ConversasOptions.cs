namespace SMSMais.Core.Conversas;

public sealed class ConversasOptions
{
    public const string SecaoConfig = "Conversas";

    /// <summary>
    /// Templates que o operador pode usar para ABRIR uma conversa (a Meta não deixa falar com
    /// quem não tem janela de 24h aberta; o template é o que provoca o primeiro contato).
    /// Lista branca proposital: dos aprovados na WABA, só os conversacionais fazem sentido aqui
    /// — os de confirmação/laudo são disparados pela fila de comunicações, não à mão. Vazia =
    /// libera todos os aprovados.
    ///
    /// Hoje só o <c>validacaio_cadastral</c> (sic — o nome aprovado na Meta tem esse typo), que
    /// apenas provoca a resposta do cidadão e abre a janela. O <c>validacao_cadastro</c>, que
    /// pede os 4 primeiros dígitos do CPF, fica fora até existir o handler que confere a
    /// resposta — sem ele o operador dispararia uma pergunta que ninguém trata.
    /// </summary>
    public string[] TemplatesInicioConversa { get; set; } = ["validacaio_cadastral"];
}
