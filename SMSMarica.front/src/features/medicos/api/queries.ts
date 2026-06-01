import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarMedico,
  buscarMedicos,
  cadastrarMedico,
  desativarMedico,
  obterMedicoPorId,
  promoverMedico,
} from '@/features/medicos/api/medicosApi';
import type {
  AtualizarMedicoPayload,
  CadastrarMedicoPayload,
  FiltroConselho,
  PromoverMedicoPayload,
} from '@/features/medicos/types';

export const medicosKeys = {
  raiz: ['medicos'] as const,
  busca: (termo: string, filtro: FiltroConselho) =>
    ['medicos', 'busca', termo, filtro.conselho ?? '', filtro.conselhoExceto ?? ''] as const,
  porId: (id: string) => ['medicos', 'detalhe', id] as const,
};

export function useBuscarMedicos(termo: string, filtro: FiltroConselho = {}) {
  const t = termo.trim();
  return useQuery({
    queryKey: medicosKeys.busca(termo, filtro),
    queryFn: () => buscarMedicos(termo, filtro),
    // Vazio: backend devolve os 10 últimos cadastros. Com 1 char a busca seria
    // ampla demais — espera o segundo caractere. >= 2: busca normal.
    enabled: t.length === 0 || t.length >= 2,
    placeholderData: (anterior) => anterior,
  });
}

export function useMedicoPorId(id: string | null) {
  return useQuery({
    queryKey: id ? medicosKeys.porId(id) : ['medicos', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterMedicoPorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarMedico() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarMedicoPayload) => cadastrarMedico(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: medicosKeys.raiz }),
  });
}

export function usePromoverMedico() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: PromoverMedicoPayload) => promoverMedico(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: medicosKeys.raiz }),
  });
}

export function useAtualizarMedico() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarMedicoPayload }) =>
      atualizarMedico(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: medicosKeys.raiz });
      client.invalidateQueries({ queryKey: medicosKeys.porId(v.id) });
    },
  });
}

export function useDesativarMedico() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarMedico(id),
    onSuccess: () => client.invalidateQueries({ queryKey: medicosKeys.raiz }),
  });
}
