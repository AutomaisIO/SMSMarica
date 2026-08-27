using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Notificacoes;

// Obs.: ComunicacaoPaciente/AgendamentoConfirmacaoEstado vivem em SMSMais.Data.Entities (raiz).

/// <summary>
/// Estado do diálogo DETERMINÍSTICO de verificação cadastral no WhatsApp (sem LLM):
/// dígitos do CPF → mês/ano de nascimento → confirmação do NOME → envia a comunicação
/// PENDURADA (<see cref="ComunicacaoPacienteId"/> — nada de procurar/deduzir na hora).
///
/// Vive FORA da conversa (molde <see cref="AgendamentoConfirmacaoEstado"/>): texto livre só é
/// interpretado enquanto há estado ativo e não expirado; no máximo UM estado por telefone.
/// Multi-paciente no mesmo número: o alvo (<see cref="PacienteId"/>) só é fixado quando os
/// dígitos casam com exatamente um candidato; até lá fica nulo e os dígitos ficam guardados
/// em <see cref="CpfDigitosInformados"/> para a desambiguação pelo nascimento.
/// </summary>
public class VerificacaoCadastralEstado
{
    public Guid Id { get; set; }

    /// <summary>Telefone canônico (só dígitos, DDI 55) — chave de roteamento. Único.</summary>
    public string TelefoneCanonical { get; set; } = string.Empty;

    /// <summary>A comunicação PENDURADA que será enviada quando a verificação concluir.</summary>
    public Guid ComunicacaoPacienteId { get; set; }

    /// <summary>Paciente-ALVO da validação. Nulo até os dígitos casarem com um único candidato.</summary>
    public Guid? PacienteId { get; set; }

    public EtapaVerificacaoCadastral Etapa { get; set; } = EtapaVerificacaoCadastral.AguardandoCpf;

    /// <summary>Dígitos de CPF informados (4–11) — guardados para desambiguar por nascimento
    /// quando mais de um paciente do número casa no prefixo.</summary>
    public string? CpfDigitosInformados { get; set; }

    /// <summary>Erros de validação na etapa corrente (2 erros ⇒ orienta o posto e silencia).</summary>
    public int TentativasErradas { get; set; }

    /// <summary>Re-orientações enviadas para texto não interpretável (teto 3 — mata loop com
    /// autoresponder/bot do outro lado; depois só processa resposta válida, em silêncio).</summary>
    public int Reorientacoes { get; set; }

    public DateTime ExpiraEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public Entities.ComunicacaoPaciente? Comunicacao { get; set; }
}
