using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Médico pedido na abertura da solicitação que ainda NÃO existe na lista do sistema de destino
/// (SER, SERNIT…) — o "cadastro pendente" (pedido do Bernardo, 02/10/2026).
///
/// <para><b>Por que pendente e não direto no sistema:</b> quem abre a solicitação não escreve no
/// cadastro do Estado. O médico fica aqui até o técnico da regulação, no fim do processo, cadastrá-lo
/// no sistema pela tela de lá (o ícone "Adicionar médico" do SER) e confirmar — ou apontar que ele
/// já existia com outro nome. No SER não há editar nem apagar: um cadastro errado ou duplicado é
/// para sempre, e por isso a escrita fica com quem conhece o sistema (complemento do ADR-0065).</para>
///
/// <para>Enquanto pendente, a solicitação guarda o médico como <c>pendente:{Id}</c>; ao resolver,
/// todas as que usam o pendente passam a ter o nome final.</para>
/// </summary>
public sealed class RegulacaoMedicoPendente
{
    public Guid Id { get; set; }

    /// <summary>Em qual lista o médico tem de entrar — o médico do SER não existe no SERNIT.</summary>
    public SistemaRegulacao Sistema { get; set; }

    /// <summary>Sempre em MAIÚSCULAS, como o sistema mostra.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Os tipos do modal do SER: CRM, CNS, RG, CPF, PMM, RMS. Nulo = sem documento.</summary>
    public string? TipoDocumento { get; set; }

    public string? NumeroDocumento { get; set; }

    public string? Especialidade { get; set; }

    public SituacaoMedicoPendente Situacao { get; set; } = SituacaoMedicoPendente.Pendente;

    /// <summary>
    /// O nome como ficou no sistema. Em "Cadastrado" costuma ser o próprio <see cref="Nome"/>; em
    /// "Já existia", é o nome do cadastro antigo (às vezes abreviado: "LAURA BEATRIZ A. RODRIGUES").
    /// </summary>
    public string? NomeNoSistema { get; set; }

    /// <summary>Por que foi recusado — é o que a unidade lê.</summary>
    public string? Motivo { get; set; }

    public DateTime? ResolvidoEm { get; set; }
    public Guid? ResolvidoPor { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
