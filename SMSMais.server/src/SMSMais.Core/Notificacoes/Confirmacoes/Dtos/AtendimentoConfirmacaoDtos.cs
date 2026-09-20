namespace SMSMais.Core.Notificacoes.Confirmacoes.Dtos;

/// <summary>As filas do menu Confirmações (derivadas — ver <c>AtendimentoConfirmacaoService</c>).</summary>
public enum AbaAtendimentoConfirmacao
{
    NaoConfirmados = 1,
    Confirmados = 2,
    ContatoErrado = 3,
    Pendentes = 4,

    /// <summary>
    /// O canal não alcança o paciente: cadastro sem celular, ou número que a Meta recusa por não
    /// estar no WhatsApp. Não é "contato errado" — o número pode ser do paciente; o que falta é
    /// chegar nele. Sai da fila automática e entra na de ligação.
    /// </summary>
    TelefoneComprometido = 5,

    /// <summary>
    /// Pediram para cancelar e ninguém tratou. Vem de qualquer canal — botão do WhatsApp, robô,
    /// app, recepção: o que importa é que a pessoa disse que não vem e a vaga continua de pé.
    /// <para>É o estado <c>StatusConfirmacao = Cancelada</c> com <c>Status</c> ainda diferente de
    /// <c>Cancelada</c>: a INTENÇÃO registrada, sem o cancelamento. Até 20/09/2026 esse estado não
    /// aparecia em aba nenhuma — as filas Contato errado e Pendentes excluem explicitamente quem
    /// está cancelado, e as outras duas exigem Pendente/Confirmada. Ficava invisível.</para>
    /// </summary>
    Cancelamento = 6,
}

/// <summary>Situação do envio automático da confirmação (o que a atendente precisa ver no card).</summary>
public sealed record EnvioConfirmacaoDto(
    Guid ComunicacaoId,
    string Status,
    string? MotivoFalha,
    string? ErroMeta,
    int Tentativas,
    DateTime? ProximaTentativaEm,
    DateTime? EnviadoEm,
    DateTime? EntregueEm,
    DateTime? LidoEm,
    DateTime? VisualizadoEm,
    string? Telefone);

/// <summary>Quem está com a solicitação (ou a estacionou) e em que situação.</summary>
public sealed record AtendimentoDto(
    Guid Id,
    Guid AtendenteId,
    string AtendenteNome,
    string Situacao,
    string? Motivo,
    DateTime IniciadoEm,
    DateTime? AtualizadoEm,
    /// <summary>O atendimento é do usuário que está consultando.</summary>
    bool EhMeu);

/// <summary>Um card da fila.</summary>
public sealed record SolicitacaoAtendimentoDto(
    Guid SolicitacaoId,
    Guid? ExameId,
    string? CodigoSolicitacao,
    Guid PacienteId,
    string? PacienteNome,
    string? PacienteCpf,
    string? Telefone,
    bool TelefoneVerificado,
    string Categoria,
    string? Procedimento,
    Guid UnidadeExecutanteId,
    string? UnidadeExecutante,
    DateTime? DataAgendada,
    string StatusConfirmacao,
    string? ConfirmadoCanal,
    DateTime? RespondidoEm,
    string? MotivoCancelamentoPaciente,
    EnvioConfirmacaoDto? Envio,
    /// <summary>O paciente escreveu no zap nas últimas 24h — dá para falar em texto livre.</summary>
    bool JanelaZapAberta,
    Guid? ConversaId,
    /// <summary>Há pendência aberta de "número errado" para este paciente.</summary>
    bool ContatoNegado,
    AtendimentoDto? Atendimento,
    /// <summary>Por que o canal não alcança (<c>SemCelular</c>/<c>NaoEhWhatsApp</c>), quando é o caso.</summary>
    string? MotivoTelefoneComprometido = null,
    /// <summary>Quantas mensagens já se perderam por esse mesmo motivo — mede a urgência.</summary>
    int TentativasPerdidas = 0);

public sealed record PaginaAtendimentoDto(
    IReadOnlyList<SolicitacaoAtendimentoDto> Itens, int Total, int Pagina, int Tamanho);

/// <summary>Contagem por aba (badges) + quantas estão comigo agora.</summary>
public sealed record ResumoAbasAtendimentoDto(
    int NaoConfirmados, int Confirmados, int ContatoErrado, int Pendentes, int EmAtendimentoComigo,
    int TelefoneComprometido = 0, int Cancelamento = 0);

/// <summary>
/// Uma mensagem da conversa, para a atendente ler o CONTEXTO antes de cancelar. Pedido de
/// cancelamento chega em texto livre e ambíguo: "não quero cancelar" e "qual o motivo do
/// cancelamento?" caem no mesmo filtro que "quero cancelar". Sem ler em volta, cancela-se errado.
/// </summary>
public sealed record MensagemContextoDto(
    bool DoPaciente, string? Texto, string? Template, DateTime OcorridoEm, string? Autor);

/// <summary>
/// Os "porquês" da aba Telefone comprometido: quantas solicitações estão paradas por cada motivo.
/// É o que responde "por que o canal não alcança parte da base" sem precisar abrir a lista.
/// </summary>
public sealed record MotivosTelefoneComprometidoDto(
    int SemCelular, int NaoEhWhatsApp, int Total, int PacientesDistintos);

public sealed record AtendenteConfirmacaoDto(Guid Id, string Nome);

public sealed record ConfirmarAtendimentoRequest(string? Meio, string? Observacao);
public sealed record CancelarAtendimentoRequest(string Motivo, string? Meio);
public sealed record PendenteAtendimentoRequest(string Motivo);
public sealed record ContatoErradoAtendimentoRequest(string? Observacao);
public sealed record TransferirAtendimentoRequest(Guid ParaUsuarioId, string? Observacao);
public sealed record ContatoCorrigidoAtendimentoRequest(string? Telefone, string? Observacao);

/// <summary>Resultado de uma ação sobre o atendimento.</summary>
public sealed record AcaoAtendimentoResultadoDto(
    Guid AtendimentoId,
    string Situacao,
    /// <summary>Fase 1 do cancelamento: o SMSMais cancelou, mas o SISREG não — a atendente precisa
    /// cancelar lá pelo navegador (a extensão observa e concilia).</summary>
    bool OrientacaoSisreg = false);

/// <summary>Evento da trilha do atendimento (detalhe).</summary>
public sealed record EventoAtendimentoDto(
    string Tipo, Guid? AtorUsuarioId, string? AtorNome, Guid? DeUsuarioId, string? DeNome,
    Guid? ParaUsuarioId, string? ParaNome, string? Observacao, DateTime OcorridoEm);
