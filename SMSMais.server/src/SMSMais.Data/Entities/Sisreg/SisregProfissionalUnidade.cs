namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Profissional executante de uma unidade, como o SISREG o conhece — a "verdade" do
/// mapeamento. Alimentado pelo AJAX <c>PROFISSIONAIS_POR_UPS</c> ao atualizar o mapeamento.
///
/// <para><b>Para que serve:</b> nasceu como régua de custo — a agenda exigia unidade +
/// profissional + procedimento, varrer era percorrer o produto cartesiano, e o
/// <see cref="Habilitado"/> impedia gastar requisição com quem não interessava (o SISREG exige
/// CAPTCHA por volume). Essa razão acabou quando a exportação passou a devolver a unidade inteira
/// numa requisição: hoje o campo só decide quem sobe ao hub FHIR, e por isso o padrão é LIGADO.</para>
///
/// <para><b>Divisão de responsabilidade:</b> a identidade do profissional é sincronizada para
/// o hub FHIR (<c>Practitioner</c>, identificado por CPF) — ver <see cref="PractitionerId"/>.
/// Já o controle de habilitação e os procedimentos vivem <b>aqui</b>, no smsmarica: são regra
/// de operação da varredura, não identidade clínica.</para>
/// </summary>
public class SisregProfissionalUnidade
{
    public Guid Id { get; set; }

    /// <summary>Unidade executante (a mesma do header X-Unidade-Id no momento do mapeamento).</summary>
    public Guid UnidadeId { get; set; }

    /// <summary>CPF do profissional, como o SISREG devolve (só dígitos). Chave natural na unidade.</summary>
    public string Cpf { get; set; } = string.Empty;

    /// <summary>Nome como o SISREG o exibe (caixa alta, sem acento normalizado).</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Entra na operação da unidade — hoje é o que decide quem sobe ao hub FHIR como
    /// <c>Practitioner</c>. <b>Nasce LIGADO</b> (padrão da rede desde 20/09/2026): a varredura lê a
    /// unidade inteira numa requisição, então habilitar deixou de comprar economia e só decidiria,
    /// de graça, quem fica fora da identidade clínica.
    /// </summary>
    public bool Habilitado { get; set; }

    /// <summary>
    /// <c>Practitioner.id</c> no hub FHIR, quando já sincronizado. Null = ainda não foi ao hub
    /// (profissional recém-descoberto ou sincronização não executada).
    /// </summary>
    public Guid? PractitionerId { get; set; }

    /// <summary>Última sincronização bem-sucedida com o hub FHIR.</summary>
    public DateTime? SincronizadoEm { get; set; }

    /// <summary>Última vez que o SISREG confirmou este profissional na unidade.</summary>
    public DateTime VistoEm { get; set; }

    /// <summary>
    /// Última vez que os <b>procedimentos</b> deste profissional foram buscados no SISREG.
    ///
    /// <para>É diferente de <see cref="VistoEm"/> de propósito, e é o que sustenta a economia de
    /// requisições do "sincroniza tudo": a lista de profissionais da unidade custa 1 requisição e
    /// já diz quem é novo, mas os procedimentos custam <b>1 requisição por profissional</b> — de
    /// longe o maior gasto do mapeamento. Com esta marca dá para rebuscar só quem é novo (ou quem
    /// está velho demais) em vez de refazer a unidade inteira toda noite.</para>
    ///
    /// <para>Null = nunca buscados (linha anterior a esta coluna). Tratado como "muito antigo",
    /// então o primeiro lote depois do deploy recompõe a marca naturalmente.</para>
    /// </summary>
    public DateTime? ProcedimentosVistosEm { get; set; }

    /// <summary>
    /// Sumiu da lista do SISREG na última atualização do mapeamento. Não apagamos a linha
    /// (perderia a habilitação e o vínculo com o hub) — marcamos e escondemos.
    /// </summary>
    public bool Ausente { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }

    public ICollection<SisregProcedimentoProfissional> Procedimentos { get; set; } = [];
}
