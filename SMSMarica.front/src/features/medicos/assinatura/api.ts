import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';
import type { AssinaturaMedico, SalvarAssinaturaMedicoPayload } from '@/features/medicos/assinatura/types';

async function obterAssinatura(medicoId: string): Promise<AssinaturaMedico | null> {
  const resp = await http.get<AssinaturaMedico | ''>(`/medicos/${medicoId}/assinatura`);
  // 204 (sem rubrica) chega como string vazia no axios.
  return resp.status === 204 || !resp.data ? null : (resp.data as AssinaturaMedico);
}

async function salvarAssinatura(
  medicoId: string,
  payload: SalvarAssinaturaMedicoPayload,
): Promise<AssinaturaMedico> {
  const { data } = await http.put<AssinaturaMedico>(`/medicos/${medicoId}/assinatura`, payload);
  return data;
}

async function removerAssinatura(medicoId: string): Promise<void> {
  await http.delete(`/medicos/${medicoId}/assinatura`);
}

const chave = (medicoId: string) => ['medicos', medicoId, 'assinatura'] as const;

export function useAssinaturaMedico(medicoId: string | null) {
  return useQuery({
    queryKey: medicoId ? chave(medicoId) : ['medicos', 'nenhum', 'assinatura'],
    queryFn: () => obterAssinatura(medicoId!),
    enabled: Boolean(medicoId),
  });
}

export function useSalvarAssinaturaMedico(medicoId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarAssinaturaMedicoPayload) => salvarAssinatura(medicoId, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: chave(medicoId) }),
  });
}

export function useRemoverAssinaturaMedico(medicoId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => removerAssinatura(medicoId),
    onSuccess: () => client.invalidateQueries({ queryKey: chave(medicoId) }),
  });
}
