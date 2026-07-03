using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Tickets.Dtos;

/// <summary>Item da lista de tickets (visão enxuta).</summary>
public sealed record TicketListItemDto(
    Guid Id,
    string Titulo,
    TicketTipo Tipo,
    TicketStatus Status,
    TicketPrioridade Prioridade,
    string? AutorNome,
    Guid? AutorId,
    Guid? UnidadeId,
    bool Arquivado,
    int QtdComentarios,
    DateTime CriadoEm,
    DateTime? AtualizadoEm);

/// <summary>Anexo (imagem) de um ticket/comentário.</summary>
public sealed record TicketAnexoDto(
    Guid Id,
    Guid MidiaId,
    string NomeArquivo,
    string Url);

/// <summary>Mensagem na conversa do ticket.</summary>
public sealed record TicketComentarioDto(
    Guid Id,
    Guid? AutorId,
    string? AutorNome,
    string Texto,
    bool Interno,
    DateTime CriadoEm,
    IReadOnlyList<TicketAnexoDto> Anexos);

/// <summary>Detalhe completo do ticket com conversa e anexos.</summary>
public sealed record TicketDto(
    Guid Id,
    string Titulo,
    string Descricao,
    TicketTipo Tipo,
    TicketStatus Status,
    TicketPrioridade Prioridade,
    string? RespostaFinal,
    string? AutorNome,
    Guid? AutorId,
    Guid? UnidadeId,
    bool ArquivadoPeloAutor,
    bool ArquivadoPeloAdmin,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    IReadOnlyList<TicketAnexoDto> Anexos,
    IReadOnlyList<TicketComentarioDto> Comentarios);

// ---- Requests ----

/// <summary>Referência a uma mídia já enviada (via POST /tickets/anexos) a vincular ao ticket/comentário.</summary>
public sealed record TicketAnexoRef(Guid MidiaId, string NomeArquivo);

public sealed record AbrirTicketRequest(
    string Titulo,
    string Descricao,
    TicketTipo Tipo,
    IReadOnlyList<TicketAnexoRef>? Anexos);

public sealed record ComentarTicketRequest(
    string Texto,
    bool Interno,
    IReadOnlyList<TicketAnexoRef>? Anexos);

/// <summary>Triagem/gestão: muda status, prioridade e/ou retorno final. Só a gestão usa.</summary>
public sealed record AtualizarTicketGestaoRequest(
    TicketStatus? Status,
    TicketPrioridade? Prioridade,
    string? RespostaFinal);

public sealed record AtualizarVisibilidadeRequest(TicketVisibilidade Visibilidade);

public sealed record TicketConfiguracaoDto(TicketVisibilidade Visibilidade);
