using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>O que a análise automática concluiu sobre um pedido que chegou de um sistema externo.</summary>
public enum VereditoAnaliseRegras
{
    /// <summary>O recurso do pedido não está ligado a nenhum procedimento canônico — sem isso não
    /// há como saber quais regras valem.</summary>
    SemProcedimento = 1,

    /// <summary>O procedimento não tem regra ativa que valha para este sistema.</summary>
    SemRegras = 2,

    /// <summary>Nenhuma regra bloqueou nem fez ressalva, e nada que trave ficou em aberto.</summary>
    Apto = 3,

    /// <summary>Alguma regra fez ressalva (o agente decide).</summary>
    ComRessalva = 4,

    /// <summary>Uma regra que BLOQUEIA depende de pergunta ou documento que só uma pessoa responde
    /// — a máquina não conclui; alguém precisa conferir.</summary>
    AConferir = 5,

    /// <summary>Uma regra bloqueia este sistema com o dado que o pedido já tem (idade, sexo, CPF, CID).</summary>
    Bloqueado = 6,
}

/// <summary>
/// Resultado da <b>análise automática das regras de elegibilidade</b> sobre um pedido que
/// <b>chegou</b> de um sistema externo — o espelho do SER, do SERNIT ou do ESUS de São Gonçalo
/// (ADR-0063 §4).
///
/// <para>Antes disto as regras (plano 03, <c>regulacao_regra</c>) só rodavam no assistente de
/// Nova Solicitação, para pedidos criados dentro do SMSMais. O que alguém de Maricá incluía
/// direto no SER/SERNIT/ESUS nunca era conferido. A análise usa o MESMO avaliador puro
/// (<c>AvaliadorElegibilidade</c>) com o que o espelho tem: nascimento, sexo, CPF, CID. Pergunta e
/// documento não têm resposta aqui — viram "a conferir" quando podem travar.</para>
///
/// <para><b>Uma linha por pedido do espelho</b>, sobrescrita quando a entrada muda: único em
/// (<see cref="Sistema"/>, <see cref="EspelhoId"/>). <see cref="EntradaHash"/> evita reanalisar o
/// que não mudou (nem o pedido, nem as regras). Sem FK para o espelho: são três tabelas
/// diferentes e a análise não pode travar a varredura.</para>
/// </summary>
public sealed class RegulacaoAnaliseEspelho
{
    public Guid Id { get; set; }

    public SistemaRegulacao Sistema { get; set; }

    /// <summary>Id da linha no espelho do sistema (<c>ser_solicitacao</c>,
    /// <c>sernit_solicitacao</c>, <c>esussg_solicitacao</c>).</summary>
    public Guid EspelhoId { get; set; }

    /// <summary>O número do pedido no sistema externo — para a tela não precisar de join.</summary>
    public string NumeroExterno { get; set; } = string.Empty;

    /// <summary>Procedimento canônico resolvido pela origem do catálogo. Nulo = não resolveu.</summary>
    public Guid? ProcedimentoId { get; set; }

    public Guid? PacienteId { get; set; }

    public VereditoAnaliseRegras Veredito { get; set; }

    public int Bloqueios { get; set; }
    public int Ressalvas { get; set; }
    public int Avisos { get; set; }
    public int PerguntasPendentes { get; set; }
    public int DocumentosPendentes { get; set; }

    /// <summary>A frase que a fila mostra ao lado do pedido (o motivo mais específico).</summary>
    public string? Resumo { get; set; }

    /// <summary>As regras avaliadas, com resultado e motivo — o que o detalhe mostra (jsonb).</summary>
    public string AlertasJson { get; set; } = "[]";

    /// <summary>Hash do que entrou (dado do pedido + versão das regras do procedimento).</summary>
    public string EntradaHash { get; set; } = string.Empty;

    public DateTime AnalisadoEm { get; set; }
    public DateTime CriadoEm { get; set; }
}
