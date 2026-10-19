using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento.Dtos;

/// <summary>
/// Posição corrente de um veículo da frota no dia (snapshot para o mapa ao vivo). Vem de uma rota do
/// dia (posição do app do motorista) e/ou do tablet fixo no carro (<see cref="Origem"/> = "tablet",
/// docs/modulos/tfd/deslocamento-tablet.md). Item só de tablet tem <see cref="RotaId"/>,
/// <see cref="Status"/> e <see cref="MotoristaId"/> nulos. Posição null = ainda sem GPS no dia.
/// <see cref="OcorrenciaAberta"/> = tipo da ocorrência mais recente em aberto (REPORT do tablet).
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
    string Origem = "motorista",
    TipoOcorrenciaVeiculo? OcorrenciaAberta = null);
