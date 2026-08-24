import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  listarPendentes,
  listarTiposExameOpcoes,
  vincularMapeamento,
} from '@/features/mapeamento-sigtap/api/mapeamentoApi';

export const mapeamentoKeys = {
  raiz: ['mapeamento-sigtap'] as const,
  pendentes: ['mapeamento-sigtap', 'pendentes'] as const,
  tipos: ['mapeamento-sigtap', 'tipos'] as const,
};

export function useListarPendentes() {
  return useQuery({ queryKey: mapeamentoKeys.pendentes, queryFn: listarPendentes });
}

export function useTiposExameOpcoes() {
  return useQuery({ queryKey: mapeamentoKeys.tipos, queryFn: listarTiposExameOpcoes });
}

export function useVincularMapeamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ sigtapCodigo, tipoExameId }: { sigtapCodigo: string; tipoExameId: string }) =>
      vincularMapeamento(sigtapCodigo, tipoExameId),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.pendentes }),
  });
}
