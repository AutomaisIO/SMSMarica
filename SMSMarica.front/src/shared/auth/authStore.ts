import { create } from 'zustand';

export type Perfil = 'operador' | 'gestor';

export type UsuarioAutenticado = {
  nome: string;
  email: string;
  perfil: Perfil;
  token: string;
};

type AuthState = {
  usuario: UsuarioAutenticado | null;
  entrar: (credenciais: { email: string; senha: string; perfil: Perfil }) => Promise<void>;
  sair: () => void;
};

const CHAVE_STORAGE = 'smsmarica.auth';

function lerUsuarioPersistido(): UsuarioAutenticado | null {
  try {
    const bruto = localStorage.getItem(CHAVE_STORAGE);
    return bruto ? (JSON.parse(bruto) as UsuarioAutenticado) : null;
  } catch {
    return null;
  }
}

export const useAuth = create<AuthState>((set) => ({
  usuario: lerUsuarioPersistido(),
  entrar: async ({ email, perfil }) => {
    // Mock até S2.4 (Identidade) expor endpoint de autenticação real.
    await new Promise((r) => setTimeout(r, 250));
    const usuario: UsuarioAutenticado = {
      nome: email.split('@')[0] ?? 'Usuário',
      email,
      perfil,
      token: `mock-token-${perfil}-${Date.now()}`,
    };
    localStorage.setItem(CHAVE_STORAGE, JSON.stringify(usuario));
    set({ usuario });
  },
  sair: () => {
    localStorage.removeItem(CHAVE_STORAGE);
    set({ usuario: null });
  },
}));

export function obterTokenMock(): string | null {
  return useAuth.getState().usuario?.token ?? null;
}
