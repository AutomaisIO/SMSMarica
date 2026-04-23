import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarPaciente,
  cadastrarPaciente,
  desativarPaciente,
  listarPacientes,
  obterPacientePorId,
} from '@/features/pacientes/api/pacientesApi';
import type {
  AtualizarPacientePayload,
  CadastrarPacientePayload,
} from '@/features/pacientes/types';

export const pacientesKeys = {
  raiz: ['pacientes'] as const,
  lista: () => ['pacientes', 'lista'] as const,
  porId: (id: string) => ['pacientes', 'detalhe', id] as const,
};

export function useListarPacientes() {
  return useQuery({
    queryKey: pacientesKeys.lista(),
    queryFn: listarPacientes,
  });
}

export function usePacientePorId(id: string | null) {
  return useQuery({
    queryKey: id ? pacientesKeys.porId(id) : ['pacientes', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterPacientePorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarPacientePayload) => cadastrarPaciente(payload),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pacientesKeys.lista() });
    },
  });
}

export function useAtualizarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarPacientePayload }) =>
      atualizarPaciente(id, payload),
    onSuccess: (_data, variables) => {
      client.invalidateQueries({ queryKey: pacientesKeys.lista() });
      client.invalidateQueries({ queryKey: pacientesKeys.porId(variables.id) });
    },
  });
}

export function useDesativarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarPaciente(id),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pacientesKeys.lista() });
    },
  });
}
