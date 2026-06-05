import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarEquipamento,
  cadastrarEquipamento,
  excluirEquipamento,
  listarEquipamentos,
} from '@/features/equipamentos/api/equipamentosApi';
import type { SalvarEquipamentoPayload } from '@/features/equipamentos/types';

export const equipamentosKeys = {
  raiz: ['equipamentos'] as const,
  lista: (unidadeId?: string, incluirInativos?: boolean) =>
    ['equipamentos', 'lista', { unidadeId: unidadeId ?? null, incluirInativos: !!incluirInativos }] as const,
};

export function useListarEquipamentos(unidadeId?: string, incluirInativos = false) {
  return useQuery({
    queryKey: equipamentosKeys.lista(unidadeId, incluirInativos),
    queryFn: () => listarEquipamentos(unidadeId, incluirInativos),
  });
}

export function useCadastrarEquipamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarEquipamentoPayload) => cadastrarEquipamento(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: equipamentosKeys.raiz }),
  });
}

export function useAtualizarEquipamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarEquipamentoPayload }) =>
      atualizarEquipamento(id, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: equipamentosKeys.raiz }),
  });
}

export function useExcluirEquipamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirEquipamento(id),
    onSuccess: () => client.invalidateQueries({ queryKey: equipamentosKeys.raiz }),
  });
}
