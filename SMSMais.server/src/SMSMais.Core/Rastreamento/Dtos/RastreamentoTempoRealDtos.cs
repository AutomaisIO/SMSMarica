using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento.Dtos;

/// <summary>
/// Posição corrente de um veículo da frota no dia (snapshot para o mapa ao vivo). Vem de uma rota do
/// dia (posição do app do motorista) e/ou do tablet fixo no carro (<see cref="Origem"/> = "tablet",
/// docs/modulos/tfd/deslocamento-tablet.md). Item só de tablet tem <see cref="RotaId"/>,
/// <see cref="Status"/> e <see cref="MotoristaId"/> nulos. Posição null = ainda sem GPS no dia.
/// </summary>
public sealed record FrotaVeiculoDto(
    Guid? RotaId,
    StatusRota? Status,
    Guid VeiculoId,
    string VeiculoPlaca,
    string VeiculoModelo,
    Guid? MotoristaId,
    string? MotoristaNome,
    int QtdPacientes,
    double? Latitude,
    double? Longitude,
    DateTime? AtualizadoEm,
    double? VelocidadeKmh = null,
    double? Rumo = null,
    string Origem = "motorista");

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
    Guid UnidadeAtendimentoId,
    string UnidadeAtendimentoNome,
    double? Latitude,
    double? Longitude,
    int? DistanciaMetros,
    bool AcompanhanteEsperado,
    DateOnly DataPrevista);

public sealed record PuxarPacienteRequest(Guid RotaId, Guid SessaoId);
