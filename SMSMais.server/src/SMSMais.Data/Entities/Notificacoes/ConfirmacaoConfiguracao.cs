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

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
