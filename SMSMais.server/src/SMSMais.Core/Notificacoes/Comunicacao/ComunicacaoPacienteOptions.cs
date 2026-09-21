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

    // ---- Cabeçalho com IMAGEM ----
    // Modelo aprovado com foto no topo EXIGE o componente de header em cada envio: a imagem do
    // modelo é só exemplo, não vai sozinha. Sem isso a Meta recusa com
    // "(#132012) Parameter format does not match format in the created template" — foi o que
    // aconteceu com os três modelos novos em 20/09/2026.

    /// <summary>
    /// Arte de cabeçalho POR MODELO (nome do modelo → URL pública; a Meta baixa o arquivo em
    /// cada envio) — o <b>padrão de fábrica</b> desta instância.
    /// <para>Desde 20/09/2026 quem manda é o painel (Mensageria → Modelos, gravado em
    /// <c>mensageria_configuracao.templates_imagens_json</c>): este mapa só vale para modelo que
    /// o painel não definiu, e existe para a instância já subir funcionando. O formato do
    /// cabeçalho vem do catálogo do relay — modelo que exige imagem e não tem arte em lugar
    /// nenhum tem o envio recusado antes da Meta, em vez de voltar 132012 da fila.</para>
    /// </summary>
    public Dictionary<string, string> ImagensCabecalho { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["confirmacao_exame"] = "https://app.smsmarica.online/mensagens/consultas-e-exames.jpg",
        ["confirmacao_consulta"] = "https://app.smsmarica.online/mensagens/consultas-e-exames.jpg",
        ["agendamento_proximo"] = "https://app.smsmarica.online/mensagens/agendamento-proximo.jpg",
        // Escolha do Bernardo entre as quatro variações da arte (20/09/2026).
        ["agendamento_cancelado_anonimo"] = "https://app.smsmarica.online/mensagens/cancelamento-branco-invertido.jpg",
    };

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

    /// <summary>
    /// Lembrete X dias antes para quem JÁ confirmou: "…está se aproximando! … Ainda está confirmado
    /// seu comparecimento?" com os botões *Sim! Está confirmado!* e *Não poderei ir.*
    /// <para>Quem ainda NÃO respondeu não recebe este: recebe a mensagem ORIGINAL de novo
    /// (<see cref="TemplateConfirmacaoExame"/>/<see cref="TemplateConfirmacaoConsulta"/>) — o que
    /// falta a essa pessoa não é lembrar a data, é entrar na conversa.</para>
    /// </summary>
    public string TemplateLembreteConfirmado { get; set; } = "agendamento_proximo";

    /// <summary>
    /// Aviso de cancelamento. Corpo aprovado:
    /// <code>
    /// Olá *{{1}}*, esse é o canal oficial do *Alô Maricá* da Secretaria Municipal de Saúde.
    /// Comunicamos que *{{2}}.*
    /// Para maiores esclarecimentos, busque informações no posto que lhe atende.
    /// </code>
    /// {{1}} = "Sr. João"/"Sra. Maria"; {{2}} = a frase <b>sem ponto final</b> — o modelo já fecha
    /// com <c>.*</c>, e um ponto a mais vira "cancelada..". Botões "Não sou essa pessoa" e "Quero
    /// mais informações", os mesmos da primeira mensagem: a máquina de verificação cadastral que
    /// já existe atende sem alteração.
    ///
    /// <para><b>Anônimo por padrão, e daí o nome.</b> Contato NÃO verificado recebe só "seu exame
    /// foi cancelado" — sem procedimento, sem data, sem unidade. O detalhe só depois de a pessoa
    /// se identificar (4 dígitos do CPF + mês/ano de nascimento). Contato verificado recebe tudo
    /// de uma vez.</para>
    ///
    /// <para><b>O motivo nunca entra</b>, em nenhuma das duas trilhas.</para>
    /// </summary>
    public string TemplateCancelamento { get; set; } = "agendamento_cancelado_anonimo";

    /// <summary>
    /// Trava de código do aviso de cancelamento. Nasce DESLIGADA: o backfill de 20/09/2026
    /// conciliou 628 cancelamentos de até três meses atrás, e ligar isto antes de a conciliação
    /// estar rodando ao vivo dispararia aviso sobre coisa velha.
    /// </summary>
    public bool EnviarAvisoCancelamento { get; set; }

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

    /// <summary>Trava de código do lembrete. A chave de operação é <c>lembrete_habilitado</c>
    /// (menu Confirmações); esta existe para desligar o recurso inteiro sem mexer no banco.</summary>
    public bool EnviarLembreteAgendamento { get; set; } = true;

    /// <summary>
    /// Silêncio mínimo entre a mensagem PRINCIPAL e o lembrete. Quem foi avisado há poucos dias não
    /// precisa ser avisado de novo — vira insistência, e insistência faz a pessoa bloquear o número
    /// (o que custa a nota da conta na Meta). Padrão 7 dias.
    /// <para>Cenário que motivou: o lote saiu em 18/09 e o lembrete de 2 dias alcançaria em 20/09
    /// justamente quem tinha acabado de receber.</para>
    /// </summary>
    public int LembreteIntervaloMinimoDias { get; set; } = 7;

    /// <summary>Ligado em 2026-07-06 (template exame_liberado APPROVED).</summary>
    public bool EnviarExameLiberado { get; set; } = true;

    /// <summary>Religado em 2026-07-08 (laudo_disponivel voltou a APPROVED na Meta após a
    /// correção do typo). A fila acumulada flui sozinha ao ligar.</summary>
    public bool EnviarLaudoPronto { get; set; } = true;
}
