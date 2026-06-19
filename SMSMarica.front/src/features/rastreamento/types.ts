export type TipoGeofence = 'Unidade' | 'Paciente' | 'PontoLogistico';

/** StatusRota do backend: 1=Planejada, 2=EmAndamento, 3=Concluída, 4=Cancelada. */
export type FrotaVeiculo = {
  rotaId: string;
  status: number;
  veiculoId: string;
  veiculoPlaca: string;
  veiculoModelo: string;
  motoristaId: string;
  motoristaNome: string;
  qtdPacientes: number;
  latitude: number | null;
  longitude: number | null;
  atualizadoEm: string | null;
};

export type Geofence = {
  id: string;
  tipo: TipoGeofence | number;
  referenciaId: string;
  latitude: number;
  longitude: number;
  raioMetros: number;
};
