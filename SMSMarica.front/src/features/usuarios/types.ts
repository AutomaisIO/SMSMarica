// PerfilUsuario como int (sem JsonStringEnumConverter no server).
// Data/Entities/Enums/PerfilUsuario.cs: Operador=1, Gestor=2, Paciente=3, Motorista=4.
export const PerfilUsuario = {
  Operador: 1,
  Gestor: 2,
  Paciente: 3,
  Motorista: 4,
} as const;

export type PerfilUsuarioValor = (typeof PerfilUsuario)[keyof typeof PerfilUsuario];

export const rotulosPerfil: Record<PerfilUsuarioValor, string> = {
  1: 'Operador',
  2: 'Gestor',
  3: 'Paciente',
  4: 'Motorista',
};

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
  perfil: PerfilUsuarioValor;
  ativo: boolean;
};

export type Usuario = {
  id: string;
  nomeCompleto: string;
  email: string;
  cpf: string | null;
  telefone: string | null;
  endereco: EnderecoDto | null;
  perfil: PerfilUsuarioValor;
  ativo: boolean;
  criadoEm: string;
  ultimoAcessoEm: string | null;
};

export type CadastrarUsuarioPayload = {
  nomeCompleto: string;
  email: string;
  cpf?: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  perfil: PerfilUsuarioValor;
};

export type AtualizarUsuarioPayload = {
  nomeCompleto: string;
  cpf?: string;
  telefone?: string;
  endereco: EnderecoDto | null;
  perfil: PerfilUsuarioValor;
};
