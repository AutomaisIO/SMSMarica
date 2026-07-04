namespace SMSMarica.Core.Notificacoes.Agendamento;

public sealed class NotificadorAgendamentoOptions
{
    public const string SecaoConfig = "NotificadorAgendamento";

    /// <summary>Liga/desliga o worker (a fila continua sendo alimentada pelo import).</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>Intervalo entre passagens do worker. Default 60s.</summary>
    public int IntervaloSegundos { get; set; } = 60;

    /// <summary>Máximo de notificações por passagem (evita rajada num Importar-todos).</summary>
    public int MaximoPorPassagem { get; set; } = 20;

    /// <summary>Tentativas de envio antes de marcar Falha terminal.</summary>
    public int MaxTentativas { get; set; } = 5;

    /// <summary>Nome do template de confirmação de exame aprovado na Meta.</summary>
    public string TemplateExame { get; set; } = "confirma_exame";

    /// <summary>Idioma do template (BCP-47 da Meta).</summary>
    public string Idioma { get; set; } = "pt_BR";
}
