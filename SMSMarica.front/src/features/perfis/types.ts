import type { AcaoPermissao, ModuloPermissao } from '@/shared/auth/authStore';

/** Permissão sobre um módulo. `acoes` vem como string do backend (ex.: "Todas", "Consulta, Edicao"). */
export type PermissaoModuloApi = { modulo: ModuloPermissao; acoes: string };

export type PerfilListItem = {
  id: string;
  nome: string;
  descricao: string | null;
  ativo: boolean;
  modulos: number;
};

export type Perfil = {
  id: string;
  nome: string;
  descricao: string | null;
  ativo: boolean;
  criadoEm: string;
  permissoes: PermissaoModuloApi[];
};

/** Matriz de edição: para cada módulo, lista de ações marcadas. */
export type MatrizEdicao = Partial<Record<ModuloPermissao, AcaoPermissao[]>>;

export type CadastrarPerfilPayload = {
  nome: string;
  descricao: string | null;
  permissoes: PermissaoModuloApi[];
};

export type AtualizarPerfilPayload = {
  nome: string;
  descricao: string | null;
  ativo: boolean;
  permissoes: PermissaoModuloApi[];
};
