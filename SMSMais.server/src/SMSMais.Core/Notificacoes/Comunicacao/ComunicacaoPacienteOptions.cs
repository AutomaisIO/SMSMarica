namespace SMSMais.Core.Notificacoes.Comunicacao;

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

    /// <summary>Modelo do Complexo Regulador (troca de 2026-07-08; antes era
    /// confirmar_agendamento_urlapp). 7 params com flexão de gênero: 1 "Sr./Sra. {nome}",
    /// 2 "O seu exame"/"A sua consulta", 3 tipo, 4 "dd/MM/aaaa às HH:mmh",
    /// 5 "o Sr. é assistido"/"a Sra. é assistida", 6 "do seu exame"/"da sua consulta",
    /// 7 "o Sr."/"a Sra.". Botões: URL /entrar/{{1}} + quick replies "Não poderei ir!" e
    /// "Falar com atendente" (este cai no módulo Conversas).</summary>
    public string TemplateConfirmaAgendamento { get; set; } = "confirmacao_regulacao";

    /// <summary>Desafio cadastral para número NÃO verificado (UTILITY, aprovado). 2 params:
    /// {{1}} primeiro nome, {{2}} procedimento. Pede os 4 primeiros dígitos do CPF; botões
    /// "Não sou essa pessoa." e "Prefiro falar com um atendente". Não revela data/local — o robô
    /// valida os 4 dígitos (VerificarCadastro) e só então a confirmação real é enviada.</summary>
    public string TemplateValidacaoCadastro { get; set; } = "validacao_cadastro";

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

    /// <summary>Religado em 2026-07-08 (laudo_disponivel voltou a APPROVED na Meta após a
    /// correção do typo). A fila acumulada flui sozinha ao ligar.</summary>
    public bool EnviarLaudoPronto { get; set; } = true;
}
