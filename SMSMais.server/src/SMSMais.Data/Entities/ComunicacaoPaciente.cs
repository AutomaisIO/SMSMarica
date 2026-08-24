using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Entities;

/// <summary>
/// Comunicação ao paciente via WhatsApp (uma linha por finalidade × solicitação), com o ciclo
/// completo: fila → envio → entrega → leitura → visualização → falha (+tentativas/motivo).
/// Finalidades: confirmação de agendamento (import), exame liberado (Realizada) e laudo pronto
/// (laudo ASSINADO). O worker (EnviadorComunicacaoService) processa Pendentes com
/// ProximaTentativaEm vencida; os recibos da Meta (webhook value.statuses) e os acessos do
/// cidadão (magic link/app) alimentam Entregue/Lida/Visualizado — é a fonte dos checks
/// (✓ enviado, ✓✓ entregue, ✓✓ azul lida/visualizada, ⚠ falha) na lista de solicitações.
/// (Renomeada de AgendamentoNotificacao em 2026-07-05.)
/// </summary>
public class ComunicacaoPaciente
{
    public Guid Id { get; set; }

    /// <summary>Natureza do agendamento (exame/consulta) — consultas no futuro.</summary>
    public TipoAgendamento Tipo { get; set; } = TipoAgendamento.Exame;

    /// <summary>Assunto do envio (confirmação, exame liberado, laudo pronto).</summary>
    public FinalidadeComunicacao Finalidade { get; set; } = FinalidadeComunicacao.ConfirmacaoAgendamento;

    /// <summary>Solicitação (regulação) à qual a comunicação se refere — vale p/ exame E consulta
    /// (única por solicitação × finalidade).</summary>
    public Guid? SolicitacaoId { get; set; }
    public Solicitacao? Solicitacao { get; set; }

    /// <summary>Aponta para fhir.patient (hub FHIR) — sem FK/navegação local.</summary>
    public Guid PacienteId { get; set; }

    /// <summary>Telefone canônico (55DDD9XXXXXXXX) escolhido na hora do envio. Null antes do 1º envio.</summary>
    public string? Telefone { get; set; }

    public StatusComunicacao Status { get; set; } = StatusComunicacao.Pendente;

    /// <summary>Motivo legível da falha (erro Meta code/título) ou "sem celular válido".</summary>
    public string? MotivoFalha { get; set; }

    /// <summary>Automático (gatilho) ou Manual (operador clicou Enviar/Reenviar). Default Automático.</summary>
    public OrigemComunicacao Origem { get; set; } = OrigemComunicacao.Automatico;

    /// <summary>Usuário que disparou manualmente (null quando automático).</summary>
    public Guid? EnviadoPor { get; set; }

    /// <summary>
    /// Envio manual com "assumo o risco": ignora o gate de telefone verificado (dado clínico só
    /// vai para número verificado por padrão). Persistido para as retentativas honrarem a decisão.
    /// </summary>
    public bool IgnorarVerificacaoTelefone { get; set; }

    /// <summary>Magic link ativo desta comunicação (renovado a cada reenvio).</summary>
    public Guid? LoginLinkId { get; set; }
    public CidadaoLoginLink? LoginLink { get; set; }

    /// <summary>Última mensagem template enviada (rastreia recibos por wamid).</summary>
    public Guid? MensagemWhatsAppId { get; set; }
    public MensagemWhatsApp? MensagemWhatsApp { get; set; }

    // ---- Retry (mesmo padrão do EnviadorWorklistService) ----

    public int Tentativas { get; set; }
    public DateTime? UltimaTentativaEm { get; set; }

    /// <summary>Quando o worker deve (re)tentar. Null = terminal.</summary>
    public DateTime? ProximaTentativaEm { get; set; }

    // ---- Recibos / acesso ----

    public DateTime? EnviadoEm { get; set; }
    public DateTime? EntregueEm { get; set; }
    public DateTime? LidoEm { get; set; }

    /// <summary>Paciente ACESSOU o conteúdo (usou o magic link desta comunicação ou abriu o
    /// recurso no app). Compõe o "✓✓ azul" junto com Lida.</summary>
    public DateTime? VisualizadoEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }
}
