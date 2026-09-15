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

/** Papel ativo derivado da existência de linha 1:1 (Medico/Motorista/Paciente). Ver ADR-0006. */
export type PapelAtual = 'Medico' | 'Motorista' | 'Paciente';

export type UsuarioListItem = {
  id: string;
  nomeCompleto: string;
  cpf: string | null;
  email: string | null;
  fotoBase64: string | null;
  ativo: boolean;
  deveTrocarSenha: boolean;
};

/** Filtro da listagem de usuários (busca por nome/CPF, unidade e limite). */
export type FiltroUsuarios = {
  busca?: string;
  unidadeId?: string;
  limite?: number;
};

export type Usuario = {
  id: string;
  nomeCompleto: string;
  email: string | null;
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
  papelAtual: PapelAtual | null;
  /** Registro do conselho quando papelAtual = 'Medico' (ex.: "CRM 52702650/RJ"). */
  registroProfissional: string | null;
  /** Nome de usuário para login, alternativa ao e-mail/CPF. */
  login: string | null;
  /** Enxerga todas as unidades, sem depender de vínculo. Só quem tem pode conceder. */
  acessoGlobal: boolean;
  /** Logins desta pessoa no SISREG (maiúsculas) — dão nome ao operador nas estatísticas. */
  loginsSisreg: string[] | null;
};

export type CadastrarUsuarioPayload = {
  nomeCompleto: string;
  email?: string;
  cpf?: string;
  dataNascimento?: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
  perfilIds?: string[];
  senha?: string;
  /** Com senha inicial, exige troca no próximo login. */
  deveTrocarSenha?: boolean;
  /** Nome de usuário para login. Opcional — o CPF já serve. */
  login?: string;
  /** Logins no SISREG. */
  loginsSisreg?: string[];
};

export type AtualizarUsuarioPayload = {
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
  /** E-mail editável/inserível (médicos importados vêm sem e-mail). Em branco = não altera. */
  email?: string;
  /** Nome de usuário. Em branco = não altera. */
  login?: string;
  /** Acesso a todas as unidades. Omitido = não altera; só quem tem pode conceder. */
  acessoGlobal?: boolean;
  /** Logins no SISREG. Omitido = não altera; lista vazia = remove a associação. */
  loginsSisreg?: string[];
};

export type AtualizarMinhaContaPayload = {
  telefone?: string;
  endereco: EnderecoDto | null;
  fotoBase64?: string | null;
};
