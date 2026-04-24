import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarPaciente,
  buscarPacientes,
  cadastrarPaciente,
  desativarPaciente,
  obterPacientePorCpf,
  obterPacientePorId,
  reativarPaciente,
} from '@/features/pacientes/api/pacientesApi';
import type {
  AtualizarPacientePayload,
  CadastrarPacientePayload,
} from '@/features/pacientes/types';

export const pacientesKeys = {
  raiz: ['pacientes'] as const,
  busca: (termo: string) => ['pacientes', 'busca', termo] as const,
  porId: (id: string) => ['pacientes', 'detalhe', id] as const,
  porCpf: (cpf: string) => ['pacientes', 'por-cpf', cpf] as const,
};

export function useBuscarPacientes(termo: string) {
  return useQuery({
    queryKey: pacientesKeys.busca(termo),
    queryFn: () => buscarPacientes(termo),
    enabled: termo.trim().length >= 2,
    placeholderData: (anterior) => anterior,
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
      client.invalidateQueries({ queryKey: pacientesKeys.raiz });
    },
  });
}

export function useAtualizarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarPacientePayload }) =>
      atualizarPaciente(id, payload),
    onSuccess: (_data, variables) => {
      client.invalidateQueries({ queryKey: pacientesKeys.raiz });
      client.invalidateQueries({ queryKey: pacientesKeys.porId(variables.id) });
    },
  });
}

export function useDesativarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarPaciente(id),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pacientesKeys.raiz });
    },
  });
}

export function useReativarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reativarPaciente(id),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pacientesKeys.raiz });
    },
  });
}

export async function consultarPacientePorCpf(cpf: string) {
  return obterPacientePorCpf(cpf);
}
