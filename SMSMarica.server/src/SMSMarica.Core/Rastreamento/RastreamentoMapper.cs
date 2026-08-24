using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Rastreamento.Dtos;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Rastreamento;

[Mapper]
internal static partial class RastreamentoMapper
{
    public static PontoGpsDto ParaDto(PontoGps p) => new(
        p.Id, p.MotoristaId, p.Coordenada.Latitude, p.Coordenada.Longitude, p.CapturadoEm);

    public static GeofenceDto ParaDto(Geofence g) => new(
        g.Id, g.Tipo, g.ReferenciaId, g.Centro.Latitude, g.Centro.Longitude, g.RaioMetros);

    public static EventoChegadaDto ParaDto(EventoChegada e) =>
        new(e.Id, e.RotaDiariaId, e.GeofenceId, e.OcorridoEm);
}
