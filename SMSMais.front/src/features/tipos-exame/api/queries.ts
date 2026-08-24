import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarTipoExame,
  cadastrarTipoExame,
  excluirTipoExame,
  listarTiposExame,
  obterTipoExame,
} from '@/features/tipos-exame/api/tiposExameApi';
import type { ModalidadeDicom, SalvarTipoExamePayload } from '@/features/tipos-exame/types';

export const tiposExameKeys = {
  raiz: ['tipos-exame'] as const,
  lista: (modalidade?: ModalidadeDicom, incluirInativos?: boolean) =>
    ['tipos-exame', 'lista', { modalidade: modalidade ?? null, incluirInativos: !!incluirInativos }] as const,
  porId: (id: string) => ['tipos-exame', 'detalhe', id] as const,
};

export function useListarTiposExame(modalidade?: ModalidadeDicom, incluirInativos = false) {
  return useQuery({
    queryKey: tiposExameKeys.lista(modalidade, incluirInativos),
    queryFn: () => listarTiposExame(modalidade, incluirInativos),
  });
}

export function useTipoExamePorId(id: string | null) {
  return useQuery({
    queryKey: id ? tiposExameKeys.porId(id) : ['tipos-exame', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterTipoExame(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarTipoExame() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarTipoExamePayload) => cadastrarTipoExame(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: tiposExameKeys.raiz }),
  });
}

export function useAtualizarTipoExame() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarTipoExamePayload }) =>
      atualizarTipoExame(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: tiposExameKeys.raiz });
      client.invalidateQueries({ queryKey: tiposExameKeys.porId(v.id) });
    },
  });
}

export function useExcluirTipoExame() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirTipoExame(id),
    onSuccess: () => client.invalidateQueries({ queryKey: tiposExameKeys.raiz }),
  });
}
