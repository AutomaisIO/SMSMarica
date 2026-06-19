export type TipoGeofence = 'Unidade' | 'Paciente' | 'PontoLogistico';

/** StatusRota do backend (enum serializado como string): Planejada | EmAndamento | Concluida | Cancelada. */
export type FrotaVeiculo = {
  rotaId: string;
  status: string;
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
