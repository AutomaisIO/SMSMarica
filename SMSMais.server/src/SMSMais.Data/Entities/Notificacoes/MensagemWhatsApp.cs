using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>
/// Trilha de auditoria das mensagens WhatsApp (via Automais.Zap, payload no formato da Cloud API) — envios e recebimentos.
/// É infraestrutura transversal do SMSMais: serve a Central de Atendimento, as
/// comunicações ao paciente e qualquer módulo que precise falar por WhatsApp. A
/// idempotência do webhook se apoia em <see cref="WaMessageId"/>. Colunas
/// <c>conversa_*</c> são aditivas (nullable): linhas legadas (anteriores ao módulo
/// Conversas) permanecem com <see cref="ConversaId"/> nulo.
/// </summary>
/// <remarks>
/// Esta entidade NÃO conhece o TFD (ADR-0038). Quem precisar amarrar uma mensagem a um
/// evento de domínio referencia a mensagem — como <c>ComunicacaoPaciente</c> faz — e não
/// o contrário. A antiga coluna <c>sessao_id</c> (FK para a sessão de tratamento) foi
/// removida: nunca teve uma única linha preenchida em 26 mil mensagens.
/// </remarks>
public class MensagemWhatsApp
{
    public Guid Id { get; set; }

    /// <summary>Identidade do paciente no hub FHIR (sem FK local).</summary>
    public Guid? PacienteId { get; set; }

    public string Telefone { get; set; } = string.Empty;
    public string? Template { get; set; }
    public DirecaoMensagem Direcao { get; set; }
    public string? Conteudo { get; set; }
    public StatusMensagemWhatsApp Status { get; set; }

    /// <summary>ID da mensagem na Meta (wamid) — único quando presente.</summary>
    public string? WaMessageId { get; set; }

    /// <summary>wamid da mensagem respondida (contexto), quando houver.</summary>
    public string? ContextoWaMessageId { get; set; }

    /// <summary>Erro de ENTREGA reportado pela Meta via webhook value.statuses (code + título),
    /// distinto de falha do POST (que fica no Conteudo). Para a tela de gestão de notificações.</summary>
    public string? ErroMeta { get; set; }

    public DateTime OcorridoEm { get; set; }
    public DateTime CriadoEm { get; set; }

    // Módulo Conversas (aditivo, nullable) --------------------------------------------------

    /// <summary>Conversa (thread) a que a mensagem pertence. Nulo em linhas legadas.</summary>
    public Guid? ConversaId { get; set; }

    /// <summary>Operador que enviou pelo painel. Nulo = inbound do cidadão ou automação.</summary>
    public Guid? AutorUsuarioId { get; set; }

    /// <summary>Snapshot do nome de exibição do operador no momento do envio (histórico correto
    /// após rename/takeover).</summary>
    public string? AutorNomeExibicao { get; set; }

    /// <summary>Natureza da mensagem. Nulo em linhas legadas (tratar como texto/template).</summary>
    public TipoMensagem? TipoMensagem { get; set; }

    // Mídia recebida (imagem/documento/áudio/vídeo) — aditivo, nullable ---------------------

    /// <summary>Id da mídia na Meta (<c>image.id</c>, <c>document.id</c>…). Só serve por ~30 dias.</summary>
    public string? MidiaWaId { get; set; }
    public string? MidiaMimeType { get; set; }

    /// <summary>Nome do arquivo informado pelo remetente (só documento traz).</summary>
    public string? MidiaNomeArquivo { get; set; }

    /// <summary>Legenda que o paciente escreveu junto da mídia.</summary>
    public string? MidiaLegenda { get; set; }

    public long? MidiaTamanho { get; set; }
    public string? MidiaSha256 { get; set; }

    /// <summary>Chave no armazenamento (Spaces) quando o arquivo foi guardado.</summary>
    public string? MidiaChave { get; set; }

    /// <summary>Nulo = mensagem sem mídia (ou anterior a este recurso).</summary>
    public SituacaoMidiaWhatsApp? MidiaSituacao { get; set; }

    /// <summary>Quando alguém da equipe aceitou ou descartou a mídia.</summary>
    public DateTime? MidiaDecididaEm { get; set; }
    public Guid? MidiaDecididaPor { get; set; }

    /// <summary>Documento do acervo criado quando a mídia foi aceita no cadastro.</summary>
    public Guid? DocumentoPacienteId { get; set; }

    public Conversa? Conversa { get; set; }
}
