namespace SMSMais.Data.Entities;

/// <summary>
/// "Entrar como paciente" (Sandbox de QA): enquanto ativa, o operador que entra no app do
/// cidadão com o PRÓPRIO CPF — e recebe o código no PRÓPRIO WhatsApp verificado — abre a sessão
/// como o paciente escolhido aqui. O paciente não recebe nada nem perde a sessão dele.
/// <para>Uma ativa por operador: escolher outro paciente encerra a anterior. Encerrar (ou
/// expirar) derruba as sessões que nasceram dela — ver <see cref="CidadaoSessao.PersonificacaoId"/>.</para>
/// </summary>
public class PersonificacaoPaciente
{
    public Guid Id { get; set; }

    /// <summary>Quem entra como o paciente. O CPF de login é o <see cref="Usuario.Cpf"/> dele.</summary>
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>Paciente (hub FHIR) que o app vai abrir.</summary>
    public Guid PatientId { get; set; }

    public DateTime CriadaEm { get; set; }
    public DateTime ExpiraEm { get; set; }

    /// <summary>Null = ainda vale (se não expirou). Preenchido = encerrada pelo operador ou
    /// substituída por outra.</summary>
    public DateTime? EncerradaEm { get; set; }
}
