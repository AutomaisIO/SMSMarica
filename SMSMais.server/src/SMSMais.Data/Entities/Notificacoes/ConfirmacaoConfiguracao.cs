namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>
/// Regras de disparo da CONFIRMAÇÃO de agendamento por WhatsApp (linha única), editadas no menu
/// Confirmações. Vale só para a finalidade confirmação — exame liberado e laudo pronto não passam
/// por aqui.
///
/// <para>As chaves por unidade (<c>sisreg_varredura_agenda.enviar_confirmacao</c>) e por
/// procedimento (<c>sisreg_procedimento_profissional.enviar_confirmacao</c>) continuam onde estão;
/// esta linha é o que vale para a rede inteira.</para>
/// </summary>
public class ConfirmacaoConfiguracao
{
    /// <summary>PK fixa — a tabela tem sempre uma única linha.</summary>
    public static readonly Guid IdSingleton = new("c0f1c0f1-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = IdSingleton;

    /// <summary>
    /// Abertura da janela de envio (Brasília). Antes dela nenhuma confirmação sai: a sincronização
    /// da madrugada EMPILHA e a fila escoa quando a janela abre. Padrão 08:00.
    /// </summary>
    public TimeOnly HoraInicioEnvio { get; set; } = new(8, 0);

    /// <summary>Fechamento da janela (Brasília, exclusivo). Padrão 18:00 — às 18:00 já não sai.</summary>
    public TimeOnly HoraFimEnvio { get; set; } = new(18, 0);

    /// <summary>Quantas confirmações o worker dispara por passagem (vazão). Padrão 100.</summary>
    public int MaximoPorPassagem { get; set; } = 100;

    /// <summary>
    /// Só agendamentos vindos do SISREG (importação/varredura/extensão) geram confirmação.
    /// Solicitação cadastrada à mão não avisa o paciente enquanto isto estiver ligado.
    /// </summary>
    public bool SomenteSisreg { get; set; } = true;

    /// <summary>
    /// Quantos dias ANTES do agendamento sai o lembrete ("a sua data está chegando"). Configuração
    /// GLOBAL — não é por unidade (decisão do produto em 19/09/2026). Padrão 2.
    /// </summary>
    public int LembreteDiasAntes { get; set; } = 2;

    /// <summary>
    /// Liga o lembrete. Os dois modelos (quem já confirmou e quem ainda não respondeu) estão
    /// aprovados na Meta desde 20/09/2026 — a ressalva que havia aqui deixou de valer.
    /// </summary>
    public bool LembreteHabilitado { get; set; }

    /// <summary>
    /// Liga o MOTOR que traz para a nossa base os cancelamentos feitos NO SISREG por outra pessoa
    /// (unidade executante, solicitante, regulação). Só leitura: roda a cada 10 min das 8h às 18h
    /// e relê o dia anterior às 7h.
    ///
    /// <para>Nasce DESLIGADO. Ligar antes de conferir o volume diário traria de uma vez tudo o que
    /// o backfill não alcançou.</para>
    /// </summary>
    public bool ConciliacaoCancelamentoHabilitada { get; set; }

    /// <summary>
    /// Liga o AVISO ao paciente quando o agendamento é cancelado. Separado do motor de propósito:
    /// dá para conciliar a base por uns dias, conferir os números, e só então começar a avisar.
    ///
    /// <para>O aviso diz que foi cancelado e nada mais — o motivo registrado no SISREG é interno.</para>
    /// </summary>
    public bool AvisoCancelamentoHabilitado { get; set; }

    /// <summary>
    /// Quando o aviso de cancelamento foi LIGADO pela última vez (transição desligado → ligado, gravada
    /// pela tela). É o que faz "ligar vale daqui para frente" ser regra do código e não de
    /// procedimento: aviso que entrou na fila antes disso, ou cancelamento feito no SISREG antes
    /// disso e só conciliado depois, não é enviado — sai como "aviso retroativo".
    /// <para>Nasceu em 25/09/2026, com 218 avisos parados de dias anteriores que não podiam sair de
    /// uma vez quando a chave fosse ligada.</para>
    /// </summary>
    public DateTime? AvisoCancelamentoLigadoEm { get; set; }

    /// <summary>
    /// Minutos entre passadas da conciliação. O padrão 10 saiu de medição: um dia útil tem 46–62
    /// cancelamentos, a listagem traz 20 por página, e o ciclo custa 1 a 3 requisições em ~1,5 s —
    /// 11 req/h no dia típico contra um teto de ~700/h. Mas é a operação que decide: apertar custa
    /// requisição, afrouxar custa latência.
    /// </summary>
    public int ConciliacaoIntervaloMinutos { get; set; } = 10;

    /// <summary>
    /// Janela de leitura, em hora de Brasília. Das 1.599 cancelações medidas em 31 dias, 98,2%
    /// caem entre 8h e 18h de segunda a sexta, e nada entre 22h e 6h — daí o padrão. Fora da
    /// janela o SISREG fica em paz e o que escapar é recolhido pela passada de fechamento.
    /// </summary>
    public int ConciliacaoHoraInicio { get; set; } = 8;
    public int ConciliacaoHoraFim { get; set; } = 18;

    /// <summary>
    /// Hora da passada de FECHAMENTO, que relê o dia ANTERIOR inteiro. É o conferidor: pega o que
    /// aconteceu fora da janela e qualquer passada que tenha falhado calada — inclusive um
    /// domingo, na segunda de manhã. Fora da janela de leitura de propósito.
    /// </summary>
    public int ConciliacaoHoraFechamento { get; set; } = 7;

    /// <summary>
    /// Último dia cujo fechamento foi concluído — e a razão de ele estar no banco, não em memória.
    ///
    /// <para>Sem isto, um dia perdido ficava perdido para sempre: bastava a API estar fora do ar
    /// durante a hora marcada (um deploy das 6h58 às 8h05 basta), ou alguém mudar a hora do
    /// fechamento para um horário que já passou — coisa que só virou possível quando a hora deixou
    /// de ser código e passou a ser tela. O dia não fecha, o expediente seguinte lê apenas o dia
    /// corrente, e os cancelamentos daquele dia feitos fora da janela nunca entram: o paciente
    /// segue sendo lembrado de um agendamento que o SISREG já cancelou.</para>
    ///
    /// <para>Guardando o último dia fechado, a passada seguinte descobre sozinha o que ficou para
    /// trás e recupera — e um reinício dentro da hora do fechamento não refaz o que já foi feito.</para>
    /// </summary>
    public DateOnly? ConciliacaoUltimoDiaFechado { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
