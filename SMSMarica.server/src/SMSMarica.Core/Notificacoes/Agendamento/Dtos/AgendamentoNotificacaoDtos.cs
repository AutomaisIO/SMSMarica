namespace SMSMarica.Core.Notificacoes.Agendamento.Dtos;

/// <summary>Filtro da tela de gestão de notificações de agendamento.</summary>
public sealed record NotificacaoFiltroDto(
    string? Status,          // Pendente|Enviada|Entregue|Lida|Falha|SemTelefoneValido
    string? Confirmacao,     // Pendente|Confirmada|Cancelada
    string? Texto,           // nome do paciente (resolvido) não filtra aqui; busca por accession/código
    DateTime? De,
    DateTime? Ate,
    int Pagina = 1,
    int Tamanho = 50);

/// <summary>Linha da lista de notificações (notificação + solicitação + confirmação).</summary>
public sealed record NotificacaoResumoDto(
    Guid Id,
    Guid? SolicitacaoExameId,
    string? AccessionNumber,
    string? CodigoSolicitacao,
    Guid PacienteId,
    string? PacienteNome,
    string? TipoExameNome,
    string? UnidadeNome,
    DateTime? DataAgendada,
    string? Telefone,
    string Status,
    string? MotivoFalha,
    int Tentativas,
    DateTime? EnviadoEm,
    DateTime? EntregueEm,
    DateTime? LidoEm,
    string StatusConfirmacao,
    DateTime? ConfirmadoEm,
    string? ConfirmadoCanal,
    string? MotivoCancelamentoPaciente,
    DateTime CriadoEm);

public sealed record PaginaNotificacoesDto(
    IReadOnlyList<NotificacaoResumoDto> Itens, int Total, int Pagina, int Tamanho);

/// <summary>Detalhe com a linha do tempo completa (mensagem + link).</summary>
public sealed record NotificacaoDetalheDto(
    NotificacaoResumoDto Resumo,
    DateTime? UltimaTentativaEm,
    DateTime? ProximaTentativaEm,
    string? MensagemConteudo,
    string? MensagemStatus,
    string? MensagemErroMeta,
    DateTime? LinkExpiraEm,
    DateTime? LinkUsadoEm,
    string? LinkUsadoIp);
