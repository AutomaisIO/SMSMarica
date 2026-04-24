export type TipoGeofence = 'Unidade' | 'Paciente' | 'PontoLogistico';

export type Geofence = {
  id: string;
  tipo: TipoGeofence | number;
  referenciaId: string;
  latitude: number;
  longitude: number;
  raioMetros: number;
};
