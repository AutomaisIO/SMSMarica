namespace SMSMais.Core.Notificacoes.Comunicacao.Dtos;

/// <summary>Filtro da tela de gestão de comunicações ao paciente.</summary>
public sealed record ComunicacaoFiltroDto(
    string? Status,          // Pendente|Enviada|Entregue|Lida|Falha|SemTelefoneValido
    string? Finalidade,      // ConfirmacaoAgendamento|ExameLiberado|LaudoPronto
    string? Confirmacao,     // Pendente|Confirmada|Cancelada (resposta do paciente)
    string? Texto,           // accession/código SISREG/telefone
    DateTime? De,
    DateTime? Ate,
    int Pagina = 1,
    int Tamanho = 50);

/// <summary>Linha da lista de comunicações (comunicação + solicitação + confirmação).</summary>
public sealed record ComunicacaoResumoDto(
    Guid Id,
    string Finalidade,
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
    DateTime? VisualizadoEm,
    string StatusConfirmacao,
    DateTime? ConfirmadoEm,
    string? ConfirmadoCanal,
    string? MotivoCancelamentoPaciente,
    DateTime CriadoEm);

public sealed record PaginaComunicacoesDto(
    IReadOnlyList<ComunicacaoResumoDto> Itens, int Total, int Pagina, int Tamanho);

/// <summary>Detalhe com a linha do tempo completa (mensagem + link).</summary>
public sealed record ComunicacaoDetalheDto(
    ComunicacaoResumoDto Resumo,
    DateTime? UltimaTentativaEm,
    DateTime? ProximaTentativaEm,
    string? MensagemConteudo,
    string? MensagemStatus,
    string? MensagemErroMeta,
    DateTime? LinkExpiraEm,
    DateTime? LinkUsadoEm,
    string? LinkUsadoIp);
