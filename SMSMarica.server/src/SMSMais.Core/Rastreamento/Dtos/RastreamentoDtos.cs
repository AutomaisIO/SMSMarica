using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento.Dtos;

public sealed record PontoGpsDto(
    Guid Id,
    Guid MotoristaId,
    double Latitude,
    double Longitude,
    DateTime CapturadoEm);

public sealed record RegistrarPontoGpsRequest(
    Guid MotoristaId,
    double Latitude,
    double Longitude,
    DateTime CapturadoEm);

public sealed record GeofenceDto(
    Guid Id,
    TipoGeofence Tipo,
    Guid ReferenciaId,
    double Latitude,
    double Longitude,
    int RaioMetros);

public sealed record CadastrarGeofenceRequest(
    TipoGeofence Tipo,
    Guid ReferenciaId,
    double Latitude,
    double Longitude,
    int RaioMetros);

public sealed record AtualizarGeofenceRequest(
    double Latitude,
    double Longitude,
    int RaioMetros);

public sealed record EventoChegadaDto(
    Guid Id,
    Guid RotaDiariaId,
    Guid GeofenceId,
    DateTime OcorridoEm);
