namespace SMSMais.Core.Notificacoes.Comunicacao;

public sealed class ComunicacaoPacienteOptions
{
    public const string SecaoConfig = "ComunicacaoPaciente";

    /// <summary>Liga/desliga o worker (a fila continua sendo alimentada pelos gatilhos).</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>Intervalo entre passagens do worker. Default 60s.</summary>
    public int IntervaloSegundos { get; set; } = 60;

    /// <summary>OBSOLETO: a vazão agora é a do menu Confirmações (<c>confirmacao_configuracao</c>).
    /// Mantido só para não quebrar o bind de configurações antigas.</summary>
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

    /// <summary>
    /// Primeira mensagem para número NÃO verificado, quando o agendamento é de EXAME. Curta e sem
    /// pedir nada: "Olá {{1}}, esse é o canal oficial do Alô Maricá… Seu exame foi agendado! Para
    /// mais informações acesse: app.smsmarica.online". 1 param: {{1}} = "Sr./Sra. {primeiro nome}".
    /// Botões: <b>Não sou essa pessoa</b> e <b>Quero mais informações</b> — o pedido dos 4 dígitos
    /// do CPF só vem depois do toque (ver <see cref="Data.Entities.Enums.EtapaVerificacaoCadastral.AguardandoInteresse"/>).
    /// </summary>
    public string TemplateConfirmacaoExame { get; set; } = "confirmacao_exame";

    /// <summary>Igual ao <see cref="TemplateConfirmacaoExame"/>, para CONSULTA ("Sua consulta foi
    /// agendada!"). O texto fixo é do próprio modelo — por isso são dois.</summary>
    public string TemplateConfirmacaoConsulta { get; set; } = "confirmacao_consulta";

    /// <summary>Lembrete "a agenda está chegando", X dias antes (X no menu Confirmações). O modelo
    /// ainda está sendo criado na Meta; enquanto não houver conteúdo, nada é enviado.</summary>
    public string TemplateLembreteAgendamento { get; set; } = "agenda_chegando";

    /// <summary>OBSOLETO desde 20/09/2026: a primeira mensagem virou
    /// <see cref="TemplateConfirmacaoExame"/>/<see cref="TemplateConfirmacaoConsulta"/>, que não
    /// pedem CPF de cara. Mantido só para não quebrar o bind de configurações antigas.</summary>
    /// {{1}} primeiro nome, {{2}} procedimento. Pede os 4 primeiros dígitos do CPF; botões
    /// "Não sou essa pessoa." e "Prefiro falar com um atendente". Não revela data/local — a
    /// máquina determinística (VerificacaoCadastralWhatsAppHandler) valida dígitos + nascimento
    /// + nome e só então a confirmação real (pendurada) é enviada.</summary>
    public string TemplateValidacaoCadastro { get; set; } = "validacao_cadastro";

    /// <summary>Liga o DESAFIO cadastral antes da confirmação para número não verificado (fluxo
    /// determinístico, independe do robô LLM). Desligado = comportamento antigo: a confirmação
    /// com data/local sai direto — só desligue com plena consciência do vazamento que reabre.</summary>
    public bool VerificacaoCadastralHabilitada { get; set; } = true;

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
