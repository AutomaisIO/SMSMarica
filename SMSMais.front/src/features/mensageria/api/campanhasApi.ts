import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';
import type {
  Campanha,
  CampanhaAlcance,
  ModoEnvioCampanha,
  SalvarCampanha,
} from '@/features/mensageria/types';

// Campanhas (ADR-0062) — Mensageria → Campanhas.

export async function listarCampanhas(): Promise<Campanha[]> {
  const { data } = await http.get<Campanha[]>('/campanhas');
  return data;
}

export async function criarCampanha(dados: SalvarCampanha): Promise<string> {
  const { data } = await http.post<string>('/campanhas', dados);
  return data;
}

export async function atualizarCampanha(id: string, dados: SalvarCampanha): Promise<void> {
  await http.put(`/campanhas/${id}`, dados);
}

export async function excluirCampanha(id: string): Promise<void> {
  await http.delete(`/campanhas/${id}`);
}

export async function obterAlcanceCampanha(id: string): Promise<CampanhaAlcance> {
  const { data } = await http.get<CampanhaAlcance>(`/campanhas/${id}/alcance`);
  return data;
}

export async function enviarCampanha(id: string, modo: ModoEnvioCampanha): Promise<{ enfileirados: number }> {
  const { data } = await http.post<{ enfileirados: number }>(`/campanhas/${id}/enviar`, { modo });
  return data;
}

export const campanhasKeys = {
  lista: ['mensageria', 'campanhas'] as const,
  alcance: (id: string) => ['mensageria', 'campanhas', id, 'alcance'] as const,
};

export function useCampanhas() {
  return useQuery({ queryKey: campanhasKeys.lista, queryFn: listarCampanhas });
}

/** O alcance se atualiza sozinho: depois do "Enviar", os recibos da Meta chegam aos poucos. */
export function useAlcanceCampanha(id: string | null) {
  return useQuery({
    queryKey: campanhasKeys.alcance(id ?? ''),
    queryFn: () => obterAlcanceCampanha(id!),
    enabled: !!id,
    refetchInterval: 15_000,
  });
}

export function useSalvarCampanha() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dados }: { id: string | null; dados: SalvarCampanha }) =>
      id ? atualizarCampanha(id, dados).then(() => id) : criarCampanha(dados),
    onSuccess: () => qc.invalidateQueries({ queryKey: campanhasKeys.lista }),
  });
}

export function useExcluirCampanha() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: excluirCampanha,
    onSuccess: () => qc.invalidateQueries({ queryKey: campanhasKeys.lista }),
  });
}

export function useEnviarCampanha() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, modo }: { id: string; modo: ModoEnvioCampanha }) => enviarCampanha(id, modo),
    onSuccess: (_r, { id }) => qc.invalidateQueries({ queryKey: campanhasKeys.alcance(id) }),
  });
}
