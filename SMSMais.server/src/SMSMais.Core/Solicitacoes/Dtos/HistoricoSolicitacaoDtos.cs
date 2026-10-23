namespace SMSMais.Core.Solicitacoes.Dtos;

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
    string? ErroMeta,
    /// <summary>Saiu (ou vai sair) no modelo "Mudança de data" — o aviso de uma remarcação do SISREG.</summary>
    bool MudancaDeData = false);

/// <summary>Contato manual registrado pela equipe (ligação etc.).</summary>
public sealed record HistoricoContatoDto(
    Guid Id,
    string Meio,
    string Resultado,
    string? Observacao,
    DateTime CriadoEm,
    string? RegistradoPorNome);

/// <summary>Evento de negócio da solicitação na linha do tempo. Duas fontes: a trilha de auditoria
/// (<c>registro_auditoria</c> — ex.: troca de unidade executante; uma única escrita alimenta a tela
/// de Auditoria e esta linha do tempo) e o que o SISREG mudou no agendamento
/// (<c>sisreg_alteracao_agenda</c> — remarcação, troca de profissional ou de procedimento).</summary>
/// <param name="Detalhe">O que foi feito a partir do evento — na remarcação, o que aconteceu com o
/// paciente ("confirmação reenviada com a data nova…"). Null na trilha de auditoria.</param>
public sealed record HistoricoEventoDto(
    Guid Id,
    string Acao,
    string? ValorAnterior,
    string? ValorNovo,
    string? RegistradoPorNome,
    DateTime CriadoEm,
    string? Detalhe = null);

/// <summary>Histórico do processo de comunicação da solicitação (para a linha do tempo do detalhe).</summary>
public sealed record HistoricoSolicitacaoDto(
    IReadOnlyList<HistoricoComunicacaoDto> Comunicacoes,
    IReadOnlyList<HistoricoContatoDto> Contatos,
    IReadOnlyList<HistoricoEventoDto> Eventos);

public sealed record RegistrarContatoRequest(string Meio, string Resultado, string? Observacao);

/// <summary>Resumo p/ os checks na LISTA de solicitações (por finalidade).</summary>
public sealed record ComunicacaoChipDto(string Status, bool Visualizado, string? Motivo);
