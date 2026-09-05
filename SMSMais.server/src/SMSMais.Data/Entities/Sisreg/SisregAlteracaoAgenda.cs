using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Uma alteração que o SISREG fez numa solicitação que já estava aqui — remarcação, troca de
/// profissional ou de procedimento.
///
/// <para><b>Por que virou entidade e não só um log:</b> a alteração precisa de alguém que a
/// resolva. O agente de regulação e a unidade solicitante têm de conseguir ver o que mudou, decidir
/// e avisar o paciente. Log em arquivo ninguém lê; linha em tela com ação, sim.</para>
///
/// <para><b>Uma linha por campo alterado</b>, não por solicitação: a remarcação e a troca de médico
/// são fatos diferentes, com urgências diferentes, e agrupar os dois esconderia o que importa.</para>
///
/// <para><b>Guarda o "antes" mesmo depois de aplicar a mudança.</b> A solicitação já foi atualizada
/// com o dado novo quando esta linha nasce — sem o valor anterior guardado aqui, ninguém teria como
/// dizer ao paciente o que exatamente mudou.</para>
/// </summary>
public class SisregAlteracaoAgenda
{
    public Guid Id { get; set; }

    public Guid SolicitacaoId { get; set; }
    public Solicitacao? Solicitacao { get; set; }

    /// <summary>Nº do SISREG — snapshot, para a linha continuar legível se a solicitação sumir.</summary>
    public string? CodigoSolicitacao { get; set; }

    public TipoAlteracaoAgenda Tipo { get; set; }

    /// <summary>Como estava. Null quando o campo não existia antes.</summary>
    public string? ValorAntes { get; set; }

    public string? ValorDepois { get; set; }

    /// <summary>Unidades copiadas da solicitação: é por elas que a tela filtra, e o executante pode
    /// mudar depois. Sem FK — o rastro não pode travar cadastro.</summary>
    public Guid? UnidadeExecutanteId { get; set; }
    public Guid? UnidadeSolicitanteId { get; set; }

    public DateTime DetectadaEm { get; set; }

    /// <summary>Operador marcou como resolvida. Null = ainda na fila da tela.</summary>
    public DateTime? TratadaEm { get; set; }
    public Guid? TratadaPor { get; set; }

    /// <summary>Quando o paciente foi avisado a partir desta alteração. Separado de
    /// <see cref="TratadaEm"/> porque nem toda alteração merece mensagem — trocar o médico da sala
    /// muitas vezes não muda nada para quem vai comparecer.</summary>
    public DateTime? ComunicadaEm { get; set; }
    public Guid? ComunicadaPor { get; set; }
}
