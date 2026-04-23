export type MotoristaListItem = {
  id: string;
  nomeCompleto: string;
  cpf: string;
  ativo: boolean;
};

export type Motorista = {
  id: string;
  nomeCompleto: string;
  cpf: string;
  cnh: string;
  telefone: string | null;
  ativo: boolean;
  criadoEm: string;
};

export type CadastrarMotoristaPayload = {
  nomeCompleto: string;
  cpf: string;
  cnh: string;
  telefone?: string;
};

export type AtualizarMotoristaPayload = {
  nomeCompleto: string;
  cnh: string;
  telefone?: string;
};
