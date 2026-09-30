import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  adicionarAcompanhante,
  consultarAcompanhante,
  listarAcompanhantes,
  removerAcompanhante,
} from '@/features/acompanhantes/api/acompanhantesApi';
import type { AdicionarAcompanhantePayload } from '@/features/acompanhantes/types';

export const acompanhantesKeys = {
  doPaciente: (pacienteId: string) => ['acompanhantes', pacienteId] as const,
};

export function useAcompanhantes(pacienteId: string | null | undefined) {
  return useQuery({
    queryKey: pacienteId ? acompanhantesKeys.doPaciente(pacienteId) : ['acompanhantes', 'nenhum'],
    queryFn: () => listarAcompanhantes(pacienteId!),
    enabled: Boolean(pacienteId),
  });
}

export function useConsultarAcompanhante(pacienteId: string) {
  return useMutation({
    mutationFn: (payload: { cpf: string; dataNascimento: string }) => consultarAcompanhante(pacienteId, payload),
  });
}

export function useAdicionarAcompanhante(pacienteId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AdicionarAcompanhantePayload) => adicionarAcompanhante(pacienteId, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: acompanhantesKeys.doPaciente(pacienteId) }),
  });
}

export function useRemoverAcompanhante(pacienteId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (acompanhanteId: string) => removerAcompanhante(pacienteId, acompanhanteId),
    onSuccess: () => client.invalidateQueries({ queryKey: acompanhantesKeys.doPaciente(pacienteId) }),
  });
}
