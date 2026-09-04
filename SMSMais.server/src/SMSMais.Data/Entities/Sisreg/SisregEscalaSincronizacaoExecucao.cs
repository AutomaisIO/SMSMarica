using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Uma execução do sincronismo de ESCALAS — a grade de oferta da rede inteira.
///
/// <para><b>Por que persistir:</b> o progresso vivo some quando a execução termina, e quem clicou no
/// botão e saiu da tela nunca saberia o que entrou. Aqui fica o depois-do-fato: quantas escalas o
/// arquivo trouxe, quantas eram novas, quantas mudaram, quantas sumiram e quantas linhas o SISREG
/// mandou quebradas. Mesmo espírito de <see cref="SisregVarreduraExecucao"/> e
/// <see cref="SisregMapeamentoLoteExecucao"/>.</para>
///
/// <para><b>Rejeitadas não é sinônimo de erro.</b> Medido em 04/09/2026 no arquivo real: 17 linhas
/// vêm com vigência <c>---</c>, defeito de cadastro do próprio SISREG, e nenhuma delas é ATIVA.
/// Por isso a execução com rejeitadas continua <c>Concluida</c> — o número existe para alguém
/// perceber quando ele <b>cresce</b>, não para pintar de vermelho o que sempre foi assim.</para>
/// </summary>
public class SisregEscalaSincronizacaoExecucao
{
    public Guid Id { get; set; }

    public DisparoSincronizacao Disparo { get; set; } = DisparoSincronizacao.Manual;

    /// <summary>Reaproveita o status da varredura — os estados são os mesmos.</summary>
    public StatusVarredura Status { get; set; } = StatusVarredura.Pendente;

    /// <summary>Linhas de escala lidas do arquivo (já descontadas as rejeitadas).</summary>
    public int EscalasLidas { get; set; }

    /// <summary>Escalas que não existiam aqui.</summary>
    public int EscalasNovas { get; set; }

    /// <summary>Escalas que já existiam e tiveram algum campo alterado pelo SISREG.</summary>
    public int EscalasAtualizadas { get; set; }

    /// <summary>Escalas que existiam aqui e não vieram no arquivo — marcadas como ausentes.</summary>
    public int EscalasAusentes { get; set; }

    /// <summary>
    /// Linhas que o parser recusou, com o motivo guardado no log. Ver a nota da classe: um número
    /// estável é o normal; o que interessa é ele crescer.
    /// </summary>
    public int LinhasRejeitadas { get; set; }

    /// <summary>
    /// Escalas cujo CNES não casou com nenhuma unidade do cadastro. Não entram — sem
    /// <c>unidade_id</c> a oferta não tem onde pousar. Zero na medição (34 de 34 casaram), então
    /// qualquer valor aqui é sinal de unidade nova no SISREG que ainda não foi descoberta.
    /// </summary>
    public int UnidadesNaoEncontradas { get; set; }

    public int Requisicoes { get; set; }

    public string? MensagemErro { get; set; }

    public DateTime IniciadoEm { get; set; }
    public DateTime? FinalizadoEm { get; set; }
    public int? DuracaoSegundos { get; set; }

    /// <summary>Quem clicou. NULL no disparo agendado — a autoria ali é o <see cref="Disparo"/>.</summary>
    public Guid? CriadoPor { get; set; }
    public string? CriadoPorNome { get; set; }
}
