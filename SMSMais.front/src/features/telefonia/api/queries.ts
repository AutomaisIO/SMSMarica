import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  definirSoftphone,
  listarRamaisLivres,
  obterMeuSoftphone,
  obterSoftphoneDoUsuario,
  removerSoftphone,
} from '@/features/telefonia/api/telefoniaApi';
import type { DefinirSoftphonePayload } from '@/features/telefonia/types';

export const telefoniaKeys = {
  all: ['telefonia'] as const,
  doUsuario: (id: string) => [...telefoniaKeys.all, 'usuario', id] as const,
  meu: () => [...telefoniaKeys.all, 'meu'] as const,
  livres: () => [...telefoniaKeys.all, 'livres'] as const,
};

export function useSoftphoneDoUsuario(usuarioId: string | null, habilitado = true) {
  return useQuery({
    queryKey: usuarioId ? telefoniaKeys.doUsuario(usuarioId) : [...telefoniaKeys.all, 'nenhum'],
    queryFn: () => obterSoftphoneDoUsuario(usuarioId!),
    enabled: !!usuarioId && habilitado,
  });
}

export function useRamaisLivres(habilitado: boolean) {
  return useQuery({
    queryKey: telefoniaKeys.livres(),
    queryFn: listarRamaisLivres,
    enabled: habilitado,
    staleTime: 30_000,
    retry: false,
  });
}

export function useMeuSoftphone() {
  return useQuery({
    queryKey: telefoniaKeys.meu(),
    queryFn: obterMeuSoftphone,
    staleTime: 5 * 60_000,
  });
}

export function useDefinirSoftphone() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (v: { usuarioId: string; payload: DefinirSoftphonePayload }) => definirSoftphone(v.usuarioId, v.payload),
    onSuccess: (_, v) => {
      client.invalidateQueries({ queryKey: telefoniaKeys.doUsuario(v.usuarioId) });
      client.invalidateQueries({ queryKey: telefoniaKeys.livres() });
      client.invalidateQueries({ queryKey: telefoniaKeys.meu() });
    },
  });
}

export function useRemoverSoftphone() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (usuarioId: string) => removerSoftphone(usuarioId),
    onSuccess: (_, usuarioId) => {
      client.invalidateQueries({ queryKey: telefoniaKeys.doUsuario(usuarioId) });
      client.invalidateQueries({ queryKey: telefoniaKeys.livres() });
      client.invalidateQueries({ queryKey: telefoniaKeys.meu() });
    },
  });
}
