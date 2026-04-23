import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarMotorista,
  cadastrarMotorista,
  desativarMotorista,
  listarMotoristas,
  obterMotoristaPorId,
} from '@/features/motoristas/api/motoristasApi';
import type {
  AtualizarMotoristaPayload,
  CadastrarMotoristaPayload,
} from '@/features/motoristas/types';

export const motoristasKeys = {
  lista: () => ['motoristas', 'lista'] as const,
  porId: (id: string) => ['motoristas', 'detalhe', id] as const,
};

export function useListarMotoristas() {
  return useQuery({ queryKey: motoristasKeys.lista(), queryFn: listarMotoristas });
}

export function useMotoristaPorId(id: string | null) {
  return useQuery({
    queryKey: id ? motoristasKeys.porId(id) : ['motoristas', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterMotoristaPorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarMotorista() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarMotoristaPayload) => cadastrarMotorista(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: motoristasKeys.lista() }),
  });
}

export function useAtualizarMotorista() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarMotoristaPayload }) =>
      atualizarMotorista(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: motoristasKeys.lista() });
      client.invalidateQueries({ queryKey: motoristasKeys.porId(v.id) });
    },
  });
}

export function useDesativarMotorista() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarMotorista(id),
    onSuccess: () => client.invalidateQueries({ queryKey: motoristasKeys.lista() }),
  });
}
