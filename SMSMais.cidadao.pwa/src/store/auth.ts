import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type PacienteSessao = {
  id: string;
  nome: string;
  cpf: string;
};

type AuthState = {
  token: string | null;
  paciente: PacienteSessao | null;
  entrar: (token: string, paciente: PacienteSessao) => void;
  sair: () => void;
};

export const useAuth = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      paciente: null,
      entrar: (token, paciente) => set({ token, paciente }),
      sair: () => set({ token: null, paciente: null }),
    }),
    { name: 'smsmarica-paciente-auth' },
  ),
);

export function obterToken(): string | null {
  return useAuth.getState().token;
}
