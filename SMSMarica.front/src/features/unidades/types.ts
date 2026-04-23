export type UnidadeListItem = {
  id: string;
  nome: string;
  ativo: boolean;
};

export type Unidade = {
  id: string;
  nome: string;
  endereco: string;
  telefone: string | null;
  latitude: number;
  longitude: number;
  ativo: boolean;
  criadoEm: string;
};

export type SalvarUnidadePayload = {
  nome: string;
  endereco: string;
  telefone?: string;
  latitude: number;
  longitude: number;
};
