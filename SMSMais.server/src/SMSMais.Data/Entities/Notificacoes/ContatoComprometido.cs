using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>
/// Marca no CADASTRO de que o canal não alcança este paciente por este número. Nasce de um fato
/// observado no envio — a Meta recusou a entrega, ou não havia celular válido para tentar — e vive
/// além da solicitação que a revelou: o problema é do cadastro, não daquele agendamento.
///
/// <para><b>Por que uma tabela e não um tipo novo de <c>PendenciaCadastro</c>:</b> pendência de
/// cadastro hoje significa "quem atendeu negou ser o paciente", e uma dúzia de consultas espalhadas
/// (envio, conversas, robô, abas) tratam qualquer pendência aberta como número NEGADO — o que
/// bloqueia mensagem automática por LGPD. Número sem WhatsApp não é número negado: continua sendo
/// do paciente, e bloquear seria punir quem só trocou de aparelho. Separar evita que uma regra de
/// privacidade pegue carona num problema de entrega.</para>
///
/// <para><b>Fecha sozinha:</b> a marca vale para <see cref="TelefoneCanonical"/>. Se a recepção
/// trocar o número do cadastro, a marca deixa de casar e para de valer — por isso a consulta
/// compara sempre com o telefone atual do paciente, em vez de confiar num booleano no cadastro.</para>
/// </summary>
public class ContatoComprometido
{
    public Guid Id { get; set; }

    public Guid PacienteId { get; set; }

    /// <summary>
    /// Número (canônico) que não alcança. Vazio quando o motivo é
    /// <see cref="MotivoContatoComprometido.SemCelular"/> — não havia número para registrar.
    /// </summary>
    public string TelefoneCanonical { get; set; } = string.Empty;

    public MotivoContatoComprometido Motivo { get; set; }

    /// <summary>Erro cru da Meta (ou motivo interno) que revelou o problema — para auditoria.</summary>
    public string? Detalhe { get; set; }

    /// <summary>Comunicação em que isso apareceu pela primeira vez.</summary>
    public Guid? ComunicacaoId { get; set; }

    public DateTime DescobertoEm { get; set; }

    /// <summary>Última vez que o mesmo problema se repetiu — mede insistência do cadastro errado.</summary>
    public DateTime UltimaOcorrenciaEm { get; set; }

    public int Ocorrencias { get; set; } = 1;

    /// <summary>Preenchido quando alguém trata (corrigiu o número, falou por telefone, desistiu).</summary>
    public DateTime? ResolvidoEm { get; set; }
    public Guid? ResolvidoPor { get; set; }
    public string? ResolucaoNota { get; set; }
}
