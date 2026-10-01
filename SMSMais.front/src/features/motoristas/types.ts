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

/** Regime de contratação — viaja como string do enum do backend. */
export type RegimeContratacao = 'Clt' | 'Rpa';

export const REGIMES_CONTRATACAO: Record<RegimeContratacao, string> = {
  Clt: 'CLT',
  Rpa: 'RPA',
};

/** Categorias de CNH aceitas pelo backend (CTB art. 143). */
export const CATEGORIAS_CNH = ['A', 'B', 'C', 'D', 'E', 'AB', 'AC', 'AD', 'AE'] as const;

export type MotoristaListItem = {
  id: string;
  usuarioId: string;
  nomeCompleto: string;
  cpf: string;
  categoriaCnh: string | null;
  regimeContratacao: RegimeContratacao | null;
  fotoBase64: string | null;
  /** Espelha Usuario.Ativo (acesso liberado/bloqueado). Exclusão é separada (excluido_em). */
  usuarioAtivo: boolean;
};

export type Motorista = {
  id: string;
  usuarioId: string;
  nomeCompleto: string;
  cpf: string;
  dataNascimento: string | null;
  cnh: string;
  categoriaCnh: string | null;
  regimeContratacao: RegimeContratacao | null;
  telefone: string | null;
  endereco: EnderecoDto | null;
  fotoBase64: string | null;
  usuarioAtivo: boolean;
  criadoEm: string;
};

export type CadastrarMotoristaPayload = {
  nomeCompleto: string;
  cpf: string;
  dataNascimento?: string;
  cnh: string;
  categoriaCnh?: string | null;
  regimeContratacao?: RegimeContratacao | null;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};

export type AtualizarMotoristaPayload = {
  cnh: string;
  categoriaCnh?: string | null;
  regimeContratacao?: RegimeContratacao | null;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};

export type PromoverMotoristaPayload = {
  usuarioId: string;
  cnh: string;
  categoriaCnh?: string | null;
  regimeContratacao?: RegimeContratacao | null;
};
