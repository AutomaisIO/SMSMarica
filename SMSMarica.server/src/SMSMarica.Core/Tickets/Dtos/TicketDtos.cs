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
    bool Respondido,
    /// <summary>Gestão: o ticket já foi encaminhado ao Agente IA (marca "Enviado à IA").</summary>
    bool EnviadoIa);

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
    IReadOnlyList<TicketComentarioDto> Comentarios,
    /// <summary>Última vez que a equipe respondeu ao autor (base da bandeira). Gestão usa para
    /// saber se o autor já visualizou a resposta.</summary>
    DateTime? RespondidoEm,
    /// <summary>Quando o autor reconheceu/visualizou a última resposta (nulo = ainda não viu).</summary>
    DateTime? RespostaReconhecidaEm,
    /// <summary>Gestão: o ticket já foi encaminhado ao Agente IA (base do rótulo do botão de encaminhamento).</summary>
    bool EnviadoIa);

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

/// <summary>Ticket com resposta pendente de reconhecimento (alimenta o modal do autor).</summary>
public sealed record TicketPendenteDto(
    Guid Id,
    int Numero,
    string Titulo,
    TicketStatus Status,
    string? RespostaFinal);

/// <summary>
/// Resumo do lado do autor: quantas respostas ainda não foram reconhecidas e a lista delas
/// (para o modal "a equipe respondeu"). Um usuário raramente tem muitas pendências.
/// </summary>
public sealed record TicketResumoAutorDto(int NaoReconhecidos, IReadOnlyList<TicketPendenteDto> Pendentes);

/// <summary>Resumo do lado da gestão para o badge do menu e o cabeçalho.</summary>
public sealed record TicketResumoGestaoDto(int Novos, int Abertos, int EmAnalise);
