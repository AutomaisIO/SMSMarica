namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Configuração do SISREG de UMA unidade. Uma linha por unidade, com dois assuntos: a agenda do
/// motor diário de varredura e o gatilho de confirmação ao paciente
/// (<see cref="EnviarConfirmacao"/>, que vale para <b>toda</b> importação da unidade, inclusive
/// upload de arquivo — por isso a linha pode existir numa unidade que nunca varre).
///
/// <para>O scheduler dispara a varredura quando <see cref="ProximoRunEm"/> vence, varrendo apenas
/// as combinações profissional × procedimento habilitadas no mapeamento daquela unidade.</para>
///
/// <para><b>Por que a hora é configurável e a janela é estreita:</b> o SISREG mantém <b>uma
/// sessão por operador</b> — o login do motor derruba o atendente da recepção que estiver usando
/// a mesma credencial, e vice-versa. Por isso a varredura roda de madrugada, e a validação recusa
/// hora fora da janela configurada em <c>Sisreg:Varredura</c>.</para>
///
/// <para><b>Cursor:</b> quando o SISREG dispara o CAPTCHA anti-robô no meio da varredura, a
/// execução para como parcial e guarda em qual par (profissional, procedimento) parou. O run do
/// dia seguinte retoma dali — mas só se a janela de datas ainda for a mesma, porque janela vencida
/// é passado e refazer o passado é gastar orçamento com dado que não muda mais.</para>
/// </summary>
public class SisregVarreduraAgenda
{
    /// <summary>Unidade varrida. Chave primária — uma agenda por unidade.</summary>
    public Guid UnidadeId { get; set; }

    /// <summary>Liga/desliga a varredura diária desta unidade.</summary>
    public bool Ativo { get; set; }

    /// <summary>
    /// Puxar a agenda da unidade INTEIRA numa requisição, em vez de uma por par
    /// profissional × procedimento.
    ///
    /// <para><b>Medido contra o SISREG real em 27/08/2026</b> (sonda
    /// <c>Automais.SISREG/sonda_export_amplo.py</c>): com <c>cpf=0</c> e <c>procedimento=0</c> — as
    /// option-sentinela do próprio formulário, que o JS dele nunca valida — o
    /// <c>expo_solicitacoes</c> devolveu 3.286 linhas e 65 procedimentos do CDT numa requisição,
    /// contendo integralmente o recorte restrito. A varredura que custa 272 requisições cabe em 1.
    /// Era essa soma que estourava o CAPTCHA (~700 por operador) e deixava a unidade parcial todo
    /// dia.</para>
    ///
    /// <para><b>Muda o significado de "habilitado" no mapeamento:</b> ele deixa de decidir o que se
    /// CONSULTA (não há mais o que escolher — vem tudo) e a agenda inteira da unidade é importada.
    /// Filtrar de volta pelos códigos habilitados seria pior que não filtrar: quando o operador
    /// habilita um GRUPO (<c>1402000</c>), o TXT traz os ITENS (<c>1402077</c>…), e o filtro
    /// descartaria justamente o que veio pelo grupo. O mapeamento continua valendo para o gatilho
    /// de confirmação ao paciente, que é opt-in por procedimento.</para>
    ///
    /// <para><b>É REGRA, não escolha</b> (01/09/2026). Nasceu desligado, como opção por unidade,
    /// enquanto era mudança nova num motor de produção. Depois de rodar, não sobrou caso em que
    /// varrer por combinação seja melhor: além de custar 1 requisição em vez de uma por par
    /// profissional × procedimento, este caminho <b>atualiza o mapeamento de graça</b> — cada linha
    /// da agenda já diz quem executa o quê (<c>AtualizarMapeamentoObservadoAsync</c>), então a
    /// varredura diária mantém médicos e procedimentos em dia sem gastar acesso nenhum. Deixar
    /// desligado é escolher pagar caro por menos informação.</para>
    ///
    /// <para>A coluna continua existindo, sempre <c>true</c>: o caminho por combinação segue no
    /// motor como fallback interno (unidade sem agenda exportável), mas não é mais oferecido na
    /// tela — o operador não tem como desligar sem querer.</para>
    /// </summary>
    public bool RecorteUnidadeInteira { get; set; } = true;

    /// <summary>
    /// Gatilho mestre da unidade: ao importar uma solicitação, avisar o paciente por WhatsApp?
    /// Vale para <b>toda</b> importação da unidade — varredura e upload de arquivo.
    ///
    /// <para>Combina com <c>SisregProcedimentoProfissional.EnviarConfirmacao</c> por "E": desligar
    /// aqui corta tudo; ligado aqui, cada procedimento ainda precisa estar ligado.</para>
    ///
    /// <para><b>Nasce DESLIGADO</b>, e unidade sem linha nesta tabela também não envia. É opt-in
    /// deliberado: mensagem ao paciente só sai depois que alguém decidiu que deve sair, unidade a
    /// unidade e procedimento a procedimento.</para>
    /// </summary>
    public bool EnviarConfirmacao { get; set; }

    /// <summary>Hora do disparo diário, em hora LOCAL de Brasília.</summary>
    public TimeOnly HoraLocal { get; set; } = new(4, 30);

    /// <summary>
    /// Quantos dias à frente varrer: <c>dataInicial = hoje</c>, <c>dataFinal = hoje + N</c>.
    /// Para trás não se varre — a cada dia se puxa o que ainda "tem para fazer".
    /// Limitado a 30 porque o SISREG recusa intervalo maior que 31 dias.
    /// </summary>
    public int DiasAFrente { get; set; } = 21;

    /// <summary>Falhas consecutivas de execuções agendadas — alimenta o backoff.</summary>
    public int FalhasConsecutivas { get; set; }

    /// <summary>
    /// Próximo disparo elegível (UTC). <b>NULL = NÃO elegível</b> — o inverso da agenda do PEP,
    /// e de propósito: numa agenda "diária às HH:mm", tratar null como "elegível já" faria o
    /// primeiro boot após um deploy disparar a varredura no meio da tarde, derrubando a sessão
    /// do atendente. Quem liga a agenda calcula o próximo horário na hora de salvar.
    /// </summary>
    public DateTime? ProximoRunEm { get; set; }

    /// <summary>Pausa administrativa até este instante (UTC), sem desligar a agenda.
    /// O CAPTCHA empurra a unidade para cá, porque relogar não resolve.</summary>
    public DateTime? PausadoAte { get; set; }

    public DateTime? UltimaExecucaoEm { get; set; }

    /// <summary>Última execução desta unidade. Sem FK — o rastreio não pode travar a agenda.</summary>
    public Guid? UltimaExecucaoId { get; set; }

    /// <summary>Último par (profissional, procedimento) CONCLUÍDO antes de a varredura parar.</summary>
    public string? CursorProfissionalCpf { get; set; }

    public string? CursorProcedimentoCodigo { get; set; }

    /// <summary>Fim da janela de datas do run parcial. O cursor só vale enquanto for &gt;= hoje.</summary>
    public DateOnly? CursorJanelaFim { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
