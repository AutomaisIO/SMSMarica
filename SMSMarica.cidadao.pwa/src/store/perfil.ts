import { create } from 'zustand';
import { api, type Perfil } from '@/lib/api';
import { extrairMensagemDeErro } from '@/lib/httpClient';

type PerfilState = {
  perfil: Perfil | null;
  carregando: boolean;
  erro: string | null;
  carregar: () => Promise<void>;
  setPerfil: (p: Perfil) => void;
};

export const usePerfil = create<PerfilState>((set, get) => ({
  perfil: null,
  carregando: false,
  erro: null,
  carregar: async () => {
    if (get().carregando) return;
    set({ carregando: true, erro: null });
    try {
      const perfil = await api.perfil();
      set({ perfil });
    } catch (e) {
      set({ erro: extrairMensagemDeErro(e) });
    } finally {
      set({ carregando: false });
    }
  },
  setPerfil: (perfil) => set({ perfil }),
}));
