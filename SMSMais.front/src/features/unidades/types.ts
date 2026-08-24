export type EnderecoDto = {
  cep: string;
  logradouro: string;
  numero: string | null;
  complemento: string | null;
  bairro: string;
  cidade: string;
  uf: string;
  pontoReferencia: string | null;
};

export type UnidadeListItem = {
  id: string;
  nome: string;
  cidade: string | null;
  uf: string | null;
  ativo: boolean;
};

export type Unidade = {
  id: string;
  nome: string;
  cnes: string | null;
  endereco: EnderecoDto | null;
  telefone: string | null;
  latitude: number | null;
  longitude: number | null;
  ativo: boolean;
  criadoEm: string;
};

export type SalvarUnidadePayload = {
  nome: string;
  cnes?: string | null;
  endereco: EnderecoDto | null;
  telefone?: string;
  latitude?: number | null;
  longitude?: number | null;
};
