using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento.Dtos;

/// <summary>
/// Posição corrente de um veículo/motorista da frota no dia (snapshot para o mapa ao vivo).
/// <see cref="Latitude"/>/<see cref="Longitude"/>/<see cref="AtualizadoEm"/> vêm do último ponto
/// GPS do dia; null quando o motorista ainda não enviou posição.
/// </summary>
public sealed record FrotaVeiculoDto(
    Guid RotaId,
    StatusRota Status,
    Guid VeiculoId,
    string VeiculoPlaca,
    string VeiculoModelo,
    Guid MotoristaId,
    string MotoristaNome,
    int QtdPacientes,
    double? Latitude,
    double? Longitude,
    DateTime? AtualizadoEm);

/// <summary>
/// Paciente que terminou o atendimento fora de Maricá e aguarda o carro para a volta.
/// <see cref="DistanciaMetros"/> é a distância (haversine) do destino até a última
/// posição GPS do motorista informado (null quando sem motorista/posição).
/// </summary>
public sealed record PacienteAguardandoDto(
    Guid SessaoId,
    Guid TratamentoId,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeId,
    string UnidadeNome,
    double? Latitude,
    double? Longitude,
    int? DistanciaMetros,
    bool AcompanhanteEsperado,
    DateOnly DataPrevista);

public sealed record PuxarPacienteRequest(Guid RotaId, Guid SessaoId);
