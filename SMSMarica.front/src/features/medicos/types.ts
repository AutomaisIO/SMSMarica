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

export type MedicoListItem = {
  id: string;
  usuarioId: string;
  nomeCompleto: string;
  cpf: string;
  crm: string;
  ufCrm: string;
  especialidade: string | null;
  fotoBase64: string | null;
  /** Espelha Usuario.Ativo (acesso liberado/bloqueado). Exclusão é separada (excluido_em). */
  usuarioAtivo: boolean;
};

export type Medico = {
  id: string;
  usuarioId: string;
  nomeCompleto: string;
  cpf: string;
  dataNascimento: string | null;
  crm: string;
  ufCrm: string;
  especialidade: string | null;
  rqe: string | null;
  validadeCrm: string | null;
  telefone: string | null;
  endereco: EnderecoDto | null;
  fotoBase64: string | null;
  usuarioAtivo: boolean;
  criadoEm: string;
};

export type CadastrarMedicoPayload = {
  nomeCompleto: string;
  cpf: string;
  dataNascimento?: string;
  crm: string;
  ufCrm: string;
  especialidade?: string;
  rqe?: string;
  validadeCrm?: string;
  email?: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};

export type AtualizarMedicoPayload = {
  crm: string;
  ufCrm: string;
  especialidade?: string;
  rqe?: string;
  validadeCrm?: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};

export type PromoverMedicoPayload = {
  usuarioId: string;
  crm: string;
  ufCrm: string;
  especialidade?: string;
  rqe?: string;
  validadeCrm?: string;
};
