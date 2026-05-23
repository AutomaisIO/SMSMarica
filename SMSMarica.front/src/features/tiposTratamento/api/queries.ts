import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarTipoTratamento,
  cadastrarTipoTratamento,
  desativarTipoTratamento,
  listarTiposTratamento,
  obterTipoTratamentoPorId,
} from '@/features/tiposTratamento/api/tiposTratamentoApi';
import type {
  AtualizarTipoTratamentoPayload,
  CadastrarTipoTratamentoPayload,
} from '@/features/tiposTratamento/types';

export const tiposTratamentoKeys = {
  lista: (somenteAtivos: boolean) => ['tiposTratamento', 'lista', somenteAtivos] as const,
  porId: (id: string) => ['tiposTratamento', 'detalhe', id] as const,
};

export function useListarTiposTratamento(somenteAtivos = false) {
  return useQuery({
    queryKey: tiposTratamentoKeys.lista(somenteAtivos),
    queryFn: () => listarTiposTratamento(somenteAtivos),
  });
}

export function useTipoTratamentoPorId(id: string | null) {
  return useQuery({
    queryKey: id ? tiposTratamentoKeys.porId(id) : ['tiposTratamento', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterTipoTratamentoPorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarTipoTratamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarTipoTratamentoPayload) => cadastrarTipoTratamento(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: ['tiposTratamento'] }),
  });
}

export function useAtualizarTipoTratamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarTipoTratamentoPayload }) =>
      atualizarTipoTratamento(id, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: ['tiposTratamento'] }),
  });
}

export function useDesativarTipoTratamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarTipoTratamento(id),
    onSuccess: () => client.invalidateQueries({ queryKey: ['tiposTratamento'] }),
  });
}
