using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>
/// O que o paciente recebe, além do aviso do agendamento, sobre os agendamentos de UMA regulação
/// externa — SER, SERNIT ou ESUS de São Gonçalo (21/10/2026). Uma linha por sistema, editada na
/// tela de Configuração da própria regulação.
///
/// <para><b>Por que tabela própria, e não o <c>parametros_json</c> da credencial</b> (onde mora o
/// horário da varredura): salvar a credencial do SER e do SERNIT regrava aquele JSON inteiro, e o
/// que estivesse lá sumiria em silêncio. E aqui é regra de mensagem ao paciente, não de acesso.</para>
///
/// <para>O SISREG não tem linha aqui: as mesmas chaves dele vivem em
/// <see cref="ConfirmacaoConfiguracao"/> (menu Mensageria → Regras), e a tela de Configuração do
/// SISREG edita aquelas.</para>
///
/// <para>Sem linha = tudo desligado. A linha só nasce no primeiro salvamento — nada de seed em
/// migration (a mesma migration roda em toda instância nova).</para>
/// </summary>
public class AvisoRegulacaoConfiguracao
{
    /// <summary>Ser, Sernit ou EsusSg — a chave da linha.</summary>
    public SistemaRegulacao Sistema { get; set; }

    /// <summary>Manda o lembrete X dias antes do agendamento.</summary>
    public bool LembreteHabilitado { get; set; }

    /// <summary>Quantos dias antes sai o lembrete (1 a 30).</summary>
    public int LembreteDiasAntes { get; set; } = 2;

    /// <summary>Avisa o paciente quando o agendamento é cancelado ou devolvido para a fila no
    /// sistema de regulação, com a data ainda pela frente.</summary>
    public bool AvisoCancelamentoHabilitado { get; set; }

    /// <summary>Quando o aviso de cancelamento foi ligado pela última vez: ligar vale daqui para
    /// frente — o que foi desmarcado antes não é avisado.</summary>
    public DateTime? AvisoCancelamentoLigadoEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
