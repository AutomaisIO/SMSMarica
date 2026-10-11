namespace SMSMais.Data.Entities;

/// <summary>
/// Um nome com que a <see cref="UnidadeAtendimento"/> aparece num sistema de regulação ("MS
/// HOSPITAL FEDERAL CARDOSO FONTES - HFCF (RIO DE JANEIRO)" no SER). Nasceu em 10/10/2026 para dar
/// ENDEREÇO ao aviso de agendamento do SER/SERNIT/ESUS, que só traz o nome.
///
/// <para><b>SEM USO desde 11/10/2026:</b> o dono decidiu que o aviso leva só o nome do local. A
/// tabela ficou (vazia) porque tirá-la exige migration nova e mexer na publicação da replicação
/// DO→EVEO — custo sem ganho. Se o endereço voltar a ser pedido, a ligação é por aqui (ADR-0072).</para>
///
/// <para><b>Ligado por uma pessoa, nunca pela máquina.</b> A máquina sugere, e erra de um jeito
/// que custa caro: "RIO IMAGEM LAGOS (CASIMIRO DE ABREU)" casa por nome com o Rio Imagem do Rio de
/// Janeiro — e o paciente iria à cidade errada. Mesma régua do ADR-0065.</para>
/// </summary>
public class UnidadeAtendimentoNomeExterno
{
    public Guid Id { get; set; }

    public Guid UnidadeAtendimentoId { get; set; }
    public UnidadeAtendimento? UnidadeAtendimento { get; set; }

    /// <summary>O nome como o sistema mostra (preservado para a tela).</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Sem acento, maiúsculo, espaços colapsados — a chave de casamento (única).</summary>
    public string NomeNormalizado { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}
