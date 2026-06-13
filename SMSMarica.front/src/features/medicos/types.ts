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

/**
 * Conselhos profissionais de saúde. CRM = médico (menu Médicos); os demais
 * aparecem no menu Profissionais. Siglas batem com o que o Salux guarda em
 * CD_CONSELHO (COREN_TE é dobrado em COREN no import).
 */
export const CONSELHOS = [
  { sigla: 'CRM', nome: 'Medicina' },
  { sigla: 'COREN', nome: 'Enfermagem' },
  { sigla: 'CRN', nome: 'Nutrição' },
  { sigla: 'CRO', nome: 'Odontologia' },
  { sigla: 'CRF', nome: 'Farmácia' },
  { sigla: 'CREFITO', nome: 'Fisioterapia e Terapia Ocupacional' },
  { sigla: 'CRP', nome: 'Psicologia' },
  { sigla: 'CRESS', nome: 'Serviço Social' },
  { sigla: 'CRFA', nome: 'Fonoaudiologia' },
  { sigla: 'CRBM', nome: 'Biomedicina' },
  { sigla: 'CRTR', nome: 'Técnico em Radiologia' },
] as const;

/** Siglas exibidas como abas no menu Profissionais (tudo menos CRM). */
export const CONSELHOS_PROFISSIONAIS = CONSELHOS.filter((c) => c.sigla !== 'CRM');

export type MedicoListItem = {
  id: string;
  /** Usuario (login) vinculado por CPF — null se o médico não tem usuário de acesso. */
  usuarioId: string | null;
  nomeCompleto: string;
  cpf: string;
  conselho: string;
  registro: string;
  ufConselho: string;
  especialidade: string | null;
  fotoBase64: string | null;
  /** Espelha Usuario.Ativo (acesso liberado/bloqueado). Exclusão é separada (excluido_em). */
  usuarioAtivo: boolean;
};

export type Medico = {
  id: string;
  /** Usuario (login) vinculado por CPF — null se o médico não tem usuário de acesso. */
  usuarioId: string | null;
  nomeCompleto: string;
  cpf: string;
  dataNascimento: string | null;
  conselho: string;
  registro: string;
  ufConselho: string;
  especialidade: string | null;
  rqe: string | null;
  validadeRegistro: string | null;
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
  conselho: string;
  registro: string;
  ufConselho: string;
  especialidade?: string;
  rqe?: string;
  validadeRegistro?: string;
  email?: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};

export type AtualizarMedicoPayload = {
  conselho: string;
  registro: string;
  ufConselho: string;
  especialidade?: string;
  rqe?: string;
  validadeRegistro?: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};

export type PromoverMedicoPayload = {
  usuarioId: string;
  conselho: string;
  registro: string;
  ufConselho: string;
  especialidade?: string;
  rqe?: string;
  validadeRegistro?: string;
};

/** Filtro de conselho aplicado na busca: exata (conselho) ou exclusão (conselhoExceto). */
export type FiltroConselho = { conselho?: string; conselhoExceto?: string };
