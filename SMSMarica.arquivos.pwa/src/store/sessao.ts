import { create } from 'zustand';
import { persist } from 'zustand/middleware';

/**
 * Sessão do app de Arquivos. NÃO há login: o token vem no QR code (query `?t=`)
 * e é o único acesso. Persistimos só o token — é multi-uso dentro do TTL, então
 * se o cidadão reabrir o PWA (sem a URL) ainda conseguimos revalidar.
 * Os dados do paciente/solicitação são revalidados a cada carga via API.
 */
type SessaoState = {
  token: string | null;
  /** Quando o token foi definido (epoch ms) — usado p/ descartar código velho. */
  definidoEm: number | null;
  definirToken: (token: string | null) => void;
  limpar: () => void;
};

export const useSessao = create<SessaoState>()(
  persist(
    (set) => ({
      token: null,
      definidoEm: null,
      definirToken: (token) => set({ token, definidoEm: token ? Date.now() : null }),
      limpar: () => set({ token: null, definidoEm: null }),
    }),
    { name: 'smsmarica-arquivos-sessao' },
  ),
);
