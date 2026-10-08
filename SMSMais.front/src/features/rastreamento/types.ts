export type TipoGeofence = 'Unidade' | 'Paciente' | 'PontoLogistico';

/** StatusRota do backend (enum serializado como string): Planejada | EmAndamento | Concluida | Cancelada. */
export type FrotaVeiculo = {
  /** Nulo quando o veículo aparece só pelo tablet fixo nele (sem rota no dia). */
  rotaId: string | null;
  status: string | null;
  veiculoId: string;
  veiculoPlaca: string;
  veiculoModelo: string;
  motoristaId: string | null;
  motoristaNome: string | null;
  qtdPacientes: number;
  latitude: number | null;
  longitude: number | null;
  atualizadoEm: string | null;
  velocidadeKmh: number | null;
  rumo: number | null;
  /** De onde veio a posição: app do motorista ou tablet do carro. */
  origem: 'motorista' | 'tablet';
};

export type Geofence = {
  id: string;
  tipo: TipoGeofence | number;
  referenciaId: string;
  latitude: number;
  longitude: number;
  raioMetros: number;
};
