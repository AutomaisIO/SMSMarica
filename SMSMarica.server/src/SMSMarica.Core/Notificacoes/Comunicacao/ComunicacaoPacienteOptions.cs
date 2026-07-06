namespace SMSMarica.Core.Notificacoes.Comunicacao;

public sealed class ComunicacaoPacienteOptions
{
    public const string SecaoConfig = "ComunicacaoPaciente";

    /// <summary>Liga/desliga o worker (a fila continua sendo alimentada pelos gatilhos).</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>Intervalo entre passagens do worker. Default 60s.</summary>
    public int IntervaloSegundos { get; set; } = 60;

    /// <summary>Máximo de comunicações por passagem (evita rajada num Importar-todos).</summary>
    public int MaximoPorPassagem { get; set; } = 20;

    /// <summary>Tentativas de envio antes de marcar Falha terminal.</summary>
    public int MaxTentativas { get; set; } = 5;

    /// <summary>Idioma dos templates (BCP-47 da Meta).</summary>
    public string Idioma { get; set; } = "pt_BR";

    // ---- Templates por finalidade (nomes APROVADOS na WABA, conferidos 2026-07-05) ----

    /// <summary>Genérico consulta/exame: 7 params (nome, "um exame", tipo, data, unidade, hora,
    /// endereço) + botão URL /entrar/{{1}} + quick reply "Não poderei comparecer!".</summary>
    public string TemplateConfirmaAgendamento { get; set; } = "confirmar_agendamento_urlapp";

    /// <summary>3 params (nome, exame, data realizada) + botão URL "Visualizar Exame".</summary>
    public string TemplateExameLiberado { get; set; } = "exame_liberado";

    /// <summary>3 params (nome, exame, data realizada) + botão URL "Visualizar Laudo".</summary>
    public string TemplateLaudoPronto { get; set; } = "laudo_disponivel";

    // ---- Chaves por finalidade. Desligada = a fila ACUMULA (ProximaTentativaEm fica no
    // passado) e o worker simplesmente não seleciona essa finalidade; ao ligar, tudo flui
    // sozinho. Útil enquanto o template correspondente aguarda aprovação da Meta. ----

    public bool EnviarConfirmacaoAgendamento { get; set; } = true;

    /// <summary>Ligado em 2026-07-06 (template exame_liberado APPROVED).</summary>
    public bool EnviarExameLiberado { get; set; } = true;

    /// <summary>OFF: a correção do typo devolveu o laudo_disponivel para PENDING na Meta.
    /// Ligar quando re-aprovar — a fila acumula e flui sozinha.</summary>
    public bool EnviarLaudoPronto { get; set; } = false;
}
