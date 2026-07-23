using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Tickets.Dtos;

/// <summary>Item da lista de tickets (visão enxuta).</summary>
public sealed record TicketListItemDto(
    Guid Id,
    int Numero,
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
    DateTime? AtualizadoEm,
    /// <summary>Autor: há resposta da equipe ainda não reconhecida (mostra a "bandeira").</summary>
    bool RespostaNaoReconhecida,
    /// <summary>Gestão: ticket novo/sem visualização (ou com atividade nova do autor).</summary>
    bool NovoParaGestao,
    /// <summary>Gestão: a equipe já respondeu ao ticket (retorno/RespostaFinal preenchido,
    /// comentário público ou conclusão/negação) — mesmo que ainda em aberto.</summary>
    bool Respondido);

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
    int Numero,
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

// ---- Resumos (badges/notificação, ticket #42) ----

/// <summary>Resumo do lado do autor: quantas respostas ainda não foram reconhecidas.</summary>
public sealed record TicketResumoAutorDto(int NaoReconhecidos);

/// <summary>Resumo do lado da gestão para o badge do menu e o cabeçalho.</summary>
public sealed record TicketResumoGestaoDto(int Novos, int Abertos, int EmAnalise);
