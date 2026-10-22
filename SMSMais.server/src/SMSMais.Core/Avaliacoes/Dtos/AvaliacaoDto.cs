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
    DateTime? CriadoEm = null,
    TrechoViagem Trecho = TrechoViagem.Ida);

/// <summary>Um trecho recente da viagem do paciente no app do cidadão, para avaliar o motorista: a ida
/// (até a unidade) e, depois do resgate, a volta para casa — cada um com o seu motorista.
/// <paramref name="Nota"/> nula = ainda não avaliou. <paramref name="Motorista"/>: só o primeiro nome.
/// <paramref name="Hora"/>: quando desceu (na unidade ou em casa).</summary>
public sealed record ViagemParaAvaliarDto(
    Guid SessaoId,
    DateOnly Data,
    string Destino,
    string? Motorista,
    int? Nota,
    string? Comentario,
    TrechoViagem Trecho = TrechoViagem.Ida,
    TimeOnly? Hora = null);

/// <summary>A nota (1 a 5) do motorista do <paramref name="Trecho"/> e, se quiser, um comentário.</summary>
public sealed record AvaliarViagemRequest(int Nota, string? Comentario, TrechoViagem Trecho = TrechoViagem.Ida);
