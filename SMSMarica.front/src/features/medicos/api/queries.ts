import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarMedico,
  cadastrarMedico,
  desativarMedico,
  listarMedicos,
  obterMedicoPorId,
  promoverMedico,
} from '@/features/medicos/api/medicosApi';
import type {
  AtualizarMedicoPayload,
  CadastrarMedicoPayload,
  PromoverMedicoPayload,
} from '@/features/medicos/types';

export const medicosKeys = {
  lista: () => ['medicos', 'lista'] as const,
  porId: (id: string) => ['medicos', 'detalhe', id] as const,
};

export function useListarMedicos() {
  return useQuery({ queryKey: medicosKeys.lista(), queryFn: listarMedicos });
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
    onSuccess: () => client.invalidateQueries({ queryKey: medicosKeys.lista() }),
  });
}

export function usePromoverMedico() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: PromoverMedicoPayload) => promoverMedico(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: medicosKeys.lista() }),
  });
}

export function useAtualizarMedico() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarMedicoPayload }) =>
      atualizarMedico(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: medicosKeys.lista() });
      client.invalidateQueries({ queryKey: medicosKeys.porId(v.id) });
    },
  });
}

export function useDesativarMedico() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarMedico(id),
    onSuccess: () => client.invalidateQueries({ queryKey: medicosKeys.lista() }),
  });
}
