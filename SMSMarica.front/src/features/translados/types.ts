export type StatusRota = 'Planejada' | 'EmAndamento' | 'Concluida' | 'Cancelada';

export type RotaDiariaListItem = {
  id: string;
  data: string;
  veiculoId: string;
  motoristaId: string;
  status: StatusRota | number;
};

export type RotaDiaria = {
  id: string;
  data: string;
  veiculoId: string;
  motoristaId: string;
  status: StatusRota | number;
  criadoEm: string;
  iniciadaEm: string | null;
  concluidaEm: string | null;
};
