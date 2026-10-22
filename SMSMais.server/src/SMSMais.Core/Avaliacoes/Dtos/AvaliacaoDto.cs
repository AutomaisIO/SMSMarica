using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Avaliacoes.Dtos;

public sealed record AvaliacaoDto(
    Guid Id,
    Guid SessaoId,
    int Nota,
    string? Comentario,
    DateTime CriadoEm);

/// <summary>Linha da lista do painel: a nota com a viagem (dia, paciente, motorista, veículo) e por
/// onde chegou.</summary>
public sealed record AvaliacaoListItemDto(
    Guid Id,
    Guid SessaoId,
    int Nota,
    DateOnly? DataViagem = null,
    string? PacienteNome = null,
    string? MotoristaNome = null,
    string? VeiculoPlaca = null,
    CanalAvaliacao Canal = CanalAvaliacao.Painel,
    string? Comentario = null,
    DateTime? CriadoEm = null);

/// <summary>Viagem recente do paciente no app do cidadão, para avaliar. <paramref name="Nota"/> nula =
/// ainda não avaliou. <paramref name="Motorista"/>: só o primeiro nome.</summary>
public sealed record ViagemParaAvaliarDto(
    Guid SessaoId,
    DateOnly Data,
    string Destino,
    string? Motorista,
    int? Nota,
    string? Comentario);

/// <summary>A nota (1 a 5) e, se quiser, um comentário.</summary>
public sealed record AvaliarViagemRequest(int Nota, string? Comentario);
