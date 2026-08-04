namespace SMSMarica.Core.SolicitacoesExame.Dtos;

/// <summary>Comunicação da solicitação na linha do tempo (checks ✓/✓✓/✓✓azul/⚠).</summary>
public sealed record HistoricoComunicacaoDto(
    Guid Id,
    string Finalidade,
    string Status,
    string? Telefone,
    int Tentativas,
    DateTime CriadoEm,
    DateTime? EnviadoEm,
    DateTime? EntregueEm,
    DateTime? LidoEm,
    DateTime? VisualizadoEm,
    string? MotivoFalha,
    string? ErroMeta);

/// <summary>Contato manual registrado pela equipe (ligação etc.).</summary>
public sealed record HistoricoContatoDto(
    Guid Id,
    string Meio,
    string Resultado,
    string? Observacao,
    DateTime CriadoEm,
    string? RegistradoPorNome);

/// <summary>Evento de negócio da solicitação na linha do tempo (trilha de auditoria — ex.: troca
/// de unidade executante). Lido de <c>registro_auditoria</c> (uma única escrita alimenta a tela
/// de Auditoria e esta linha do tempo).</summary>
public sealed record HistoricoEventoDto(
    Guid Id,
    string Acao,
    string? ValorAnterior,
    string? ValorNovo,
    string? RegistradoPorNome,
    DateTime CriadoEm);

/// <summary>Histórico do processo de comunicação da solicitação (para a linha do tempo do detalhe).</summary>
public sealed record HistoricoSolicitacaoDto(
    IReadOnlyList<HistoricoComunicacaoDto> Comunicacoes,
    IReadOnlyList<HistoricoContatoDto> Contatos,
    IReadOnlyList<HistoricoEventoDto> Eventos);

public sealed record RegistrarContatoRequest(string Meio, string Resultado, string? Observacao);

/// <summary>Resumo p/ os checks na LISTA de solicitações (por finalidade).</summary>
public sealed record ComunicacaoChipDto(string Status, bool Visualizado, string? Motivo);
