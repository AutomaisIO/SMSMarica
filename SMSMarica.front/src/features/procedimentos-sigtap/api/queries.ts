import { useQuery } from '@tanstack/react-query';
import {
  listarProcedimentos,
  obterProcedimento,
} from '@/features/procedimentos-sigtap/api/procedimentosSigtapApi';

export const procedimentosSigtapKeys = {
  raiz: ['procedimentos-sigtap'] as const,
  lista: (busca?: string, grupo?: string, limite?: number) =>
    ['procedimentos-sigtap', 'lista', { busca: busca ?? null, grupo: grupo ?? null, limite: limite ?? 50 }] as const,
  porId: (id: string) => ['procedimentos-sigtap', 'detalhe', id] as const,
};

export function useListarProcedimentos(busca?: string, grupo?: string, limite = 50) {
  return useQuery({
    queryKey: procedimentosSigtapKeys.lista(busca, grupo, limite),
    queryFn: () => listarProcedimentos(busca, grupo, limite),
    placeholderData: (anterior) => anterior,
  });
}

export function useProcedimentoPorId(id: string | null) {
  return useQuery({
    queryKey: id ? procedimentosSigtapKeys.porId(id) : ['procedimentos-sigtap', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterProcedimento(id);
    },
    enabled: Boolean(id),
  });
}
