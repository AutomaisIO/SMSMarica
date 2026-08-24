import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarLayoutVeiculo,
  atualizarVeiculo,
  cadastrarVeiculo,
  desativarVeiculo,
  listarVeiculos,
  obterVeiculoPorId,
} from '@/features/veiculos/api/veiculosApi';
import type {
  AtualizarLayoutPayload,
  AtualizarVeiculoPayload,
  CadastrarVeiculoPayload,
} from '@/features/veiculos/types';

export const veiculosKeys = {
  lista: () => ['veiculos', 'lista'] as const,
  porId: (id: string) => ['veiculos', 'detalhe', id] as const,
};

export function useListarVeiculos() {
  return useQuery({ queryKey: veiculosKeys.lista(), queryFn: listarVeiculos });
}

export function useVeiculoPorId(id: string | null) {
  return useQuery({
    queryKey: id ? veiculosKeys.porId(id) : ['veiculos', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterVeiculoPorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarVeiculo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarVeiculoPayload) => cadastrarVeiculo(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: veiculosKeys.lista() }),
  });
}

export function useAtualizarVeiculo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarVeiculoPayload }) =>
      atualizarVeiculo(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: veiculosKeys.lista() });
      client.invalidateQueries({ queryKey: veiculosKeys.porId(v.id) });
    },
  });
}

export function useAtualizarLayoutVeiculo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarLayoutPayload }) =>
      atualizarLayoutVeiculo(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: veiculosKeys.porId(v.id) });
    },
  });
}

export function useDesativarVeiculo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarVeiculo(id),
    onSuccess: () => client.invalidateQueries({ queryKey: veiculosKeys.lista() }),
  });
}
