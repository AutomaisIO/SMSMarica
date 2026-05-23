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

export type UsuarioListItem = {
  id: string;
  nomeCompleto: string;
  email: string;
  fotoBase64: string | null;
  ativo: boolean;
  deveTrocarSenha: boolean;
};

export type Usuario = {
  id: string;
  nomeCompleto: string;
  email: string;
  cpf: string | null;
  dataNascimento: string | null;
  telefone: string | null;
  endereco: EnderecoDto | null;
  fotoBase64: string | null;
  ativo: boolean;
  criadoEm: string;
  ultimoAcessoEm: string | null;
  perfilIds: string[];
  deveTrocarSenha: boolean;
};

export type CadastrarUsuarioPayload = {
  nomeCompleto: string;
  email: string;
  cpf?: string;
  dataNascimento?: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
  perfilIds?: string[];
  senha?: string;
};

export type AtualizarUsuarioPayload = {
  nomeCompleto: string;
  cpf?: string;
  dataNascimento?: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};

export type AtualizarMinhaContaPayload = {
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};
