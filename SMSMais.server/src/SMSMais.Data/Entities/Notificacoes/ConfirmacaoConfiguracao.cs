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

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
