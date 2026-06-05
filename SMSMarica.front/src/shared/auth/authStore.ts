import { create } from 'zustand';
import { http } from '@/shared/api/httpClient';

export type ModuloPermissao =
  | 'Pacientes'
  | 'Unidades'
  | 'Veiculos'
  | 'Motoristas'
  | 'Usuarios'
  | 'TiposTratamento'
  | 'Perfis'
  | 'Tratamentos'
  | 'Translados'
  | 'Rastreamento'
  | 'Avaliacoes'
  | 'Pacs'
  | 'Medicos'
  | 'Laudos'
  | 'LaudosTemplates'
  | 'SolicitacoesExame'
  | 'TiposExame'
  | 'ProcedimentosSigtap'
  | 'Inteligencia'
  | 'InteligenciaConfiguracao';

export type AcaoPermissao = 'Consulta' | 'Inclusao' | 'Edicao' | 'Exclusao';

export type UsuarioAutenticado = {
  id: string;
  nome: string;
  email: string;
  deveTrocarSenha: boolean;
};

type PermissaoApi = { modulo: ModuloPermissao; acoes: string };

type AuthState = {
  usuario: UsuarioAutenticado | null;
  token: string | null;
  expiraEm: string | null;
  permissoes: Partial<Record<ModuloPermissao, AcaoPermissao[]>>;
  entrar: (credenciais: { email: string; senha: string }) => Promise<void>;
  sair: () => void;
  recarregarPermissoes: () => Promise<void>;
  /** Marca que a troca obrigatória foi concluída (limpa a flag local). */
  marcarSenhaTrocada: () => void;
};

const CHAVE_STORAGE = 'smsmarica.auth';

type Persistido = {
  usuario: UsuarioAutenticado;
  token: string;
  expiraEm: string;
  permissoes: Partial<Record<ModuloPermissao, AcaoPermissao[]>>;
};

function lerPersistido(): Persistido | null {
  try {
    const bruto = localStorage.getItem(CHAVE_STORAGE);
    if (!bruto) return null;
    const obj = JSON.parse(bruto) as Persistido;
    // Se já expirou, descarta para forçar novo login.
    if (obj?.expiraEm && new Date(obj.expiraEm).getTime() < Date.now()) return null;
    return obj;
  } catch {
    return null;
  }
}

function parseAcoes(s: string): AcaoPermissao[] {
  if (!s || s === 'Nenhuma') return [];
  if (s === 'Todas') return ['Consulta', 'Inclusao', 'Edicao', 'Exclusao'];
  return s
    .split(/\s*,\s*/)
    .filter(Boolean)
    .filter((x): x is AcaoPermissao =>
      x === 'Consulta' || x === 'Inclusao' || x === 'Edicao' || x === 'Exclusao',
    );
}

function indexarPermissoes(lista: PermissaoApi[] | undefined | null) {
  const result: Partial<Record<ModuloPermissao, AcaoPermissao[]>> = {};
  for (const p of lista ?? []) {
    result[p.modulo] = parseAcoes(p.acoes);
  }
  return result;
}

type LoginResposta = {
  token: string;
  expiraEm: string;
  usuario: { id: string; nomeCompleto: string; email: string; deveTrocarSenha: boolean };
  permissoes: PermissaoApi[];
};

const persistido = lerPersistido();

export const useAuth = create<AuthState>((set, get) => ({
  usuario: persistido?.usuario ?? null,
  token: persistido?.token ?? null,
  expiraEm: persistido?.expiraEm ?? null,
  permissoes: persistido?.permissoes ?? {},

  entrar: async ({ email, senha }) => {
    const { data } = await http.post<LoginResposta>('/identidade/login', { email, senha });
    const usuario: UsuarioAutenticado = {
      id: data.usuario.id,
      nome: data.usuario.nomeCompleto,
      email: data.usuario.email,
      deveTrocarSenha: data.usuario.deveTrocarSenha,
    };
    const permissoes = indexarPermissoes(data.permissoes);
    const persistir: Persistido = { usuario, token: data.token, expiraEm: data.expiraEm, permissoes };
    localStorage.setItem(CHAVE_STORAGE, JSON.stringify(persistir));
    set({ usuario, token: data.token, expiraEm: data.expiraEm, permissoes });
  },

  sair: () => {
    localStorage.removeItem(CHAVE_STORAGE);
    set({ usuario: null, token: null, expiraEm: null, permissoes: {} });
  },

  marcarSenhaTrocada: () => {
    const atual = get();
    if (!atual.usuario) return;
    const novo: UsuarioAutenticado = { ...atual.usuario, deveTrocarSenha: false };
    if (atual.token && atual.expiraEm) {
      const persistir: Persistido = {
        usuario: novo,
        token: atual.token,
        expiraEm: atual.expiraEm,
        permissoes: atual.permissoes,
      };
      localStorage.setItem(CHAVE_STORAGE, JSON.stringify(persistir));
    }
    set({ usuario: novo });
  },

  recarregarPermissoes: async () => {
    const usuario = get().usuario;
    if (!usuario) return;
    type Resp = { resolvidas: PermissaoApi[] };
    const { data } = await http.get<Resp>('/identidade/me/permissoes');
    const permissoes = indexarPermissoes(data.resolvidas);
    const atual = get();
    if (atual.token && atual.expiraEm) {
      const persistir: Persistido = {
        usuario,
        token: atual.token,
        expiraEm: atual.expiraEm,
        permissoes,
      };
      localStorage.setItem(CHAVE_STORAGE, JSON.stringify(persistir));
    }
    set({ permissoes });
  },
}));

export function obterToken(): string | null {
  return useAuth.getState().token;
}

/** Hook utilitário: o usuário pode CONSULTAR (entrar em) o módulo? */
export function useTemConsulta(modulo: ModuloPermissao): boolean {
  return useAuth((s) => (s.permissoes[modulo] ?? []).includes('Consulta'));
}

/** Hook utilitário: o usuário pode executar a ação específica no módulo? */
export function usePermissao(modulo: ModuloPermissao, acao: AcaoPermissao): boolean {
  return useAuth((s) => (s.permissoes[modulo] ?? []).includes(acao));
}
