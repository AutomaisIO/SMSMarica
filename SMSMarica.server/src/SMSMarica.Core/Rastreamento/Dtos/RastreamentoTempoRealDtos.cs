namespace SMSMarica.Core.Rastreamento.Dtos;

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
