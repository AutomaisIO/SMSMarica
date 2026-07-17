import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  descartarFalhaImportacao,
  listarFalhasImportacao,
  reprocessarFalhaImportacao,
} from '@/features/importacao-sisreg/api/importacaoApi';

export const importacaoKeys = {
  falhas: (somentePendentes: boolean) => ['importacao-sisreg', 'falhas', somentePendentes] as const,
};

export function useFalhasImportacao(somentePendentes: boolean) {
  return useQuery({
    queryKey: importacaoKeys.falhas(somentePendentes),
    queryFn: () => listarFalhasImportacao(somentePendentes),
  });
}

/** "Validar" uma linha. Invalida as duas listas (pendentes e histórico) — a linha muda de lado. */
export function useReprocessarFalha() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: reprocessarFalhaImportacao,
    onSuccess: () => client.invalidateQueries({ queryKey: ['importacao-sisreg', 'falhas'] }),
  });
}

export function useDescartarFalha() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, nota }: { id: string; nota?: string }) => descartarFalhaImportacao(id, nota),
    onSuccess: () => client.invalidateQueries({ queryKey: ['importacao-sisreg', 'falhas'] }),
  });
}
