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

export type MotoristaListItem = {
  id: string;
  nomeCompleto: string;
  cpf: string;
  fotoBase64: string | null;
  ativo: boolean;
};

export type Motorista = {
  id: string;
  usuarioId: string;
  nomeCompleto: string;
  cpf: string;
  dataNascimento: string | null;
  cnh: string;
  telefone: string | null;
  endereco: EnderecoDto | null;
  fotoBase64: string | null;
  ativo: boolean;
  criadoEm: string;
};

export type CadastrarMotoristaPayload = {
  nomeCompleto: string;
  cpf: string;
  cnh: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};

export type AtualizarMotoristaPayload = {
  cnh: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};

export type PromoverMotoristaPayload = {
  usuarioId: string;
  cnh: string;
};
