import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';
import type { NotificacaoDetalhe } from '@/features/notificacoes-agendamento/types';
import type {
  ConfirmacaoConfiguracao,
  PreviaLote,
  FiltroFila,
  FiltroRespostas,
  PaginaFila,
  PaginaRespostas,
  RegraUnidade,
  ResumoFilaConfirmacao,
  SalvarConfirmacaoConfiguracao,
} from '@/features/confirmacoes/types';

function params(filtro: object): Record<string, string | number> {
  const p: Record<string, string | number> = {};
  for (const [k, v] of Object.entries(filtro)) {
    if (v !== undefined && v !== null && v !== '') p[k] = v as string | number;
  }
  return p;
}

const raiz = ['confirmacoes'] as const;

export function useResumoFila() {
  return useQuery({
    queryKey: [...raiz, 'resumo'],
    queryFn: async () => (await http.get<ResumoFilaConfirmacao>('/confirmacoes/fila/resumo')).data,
    refetchInterval: 30_000,
  });
}

export function useFila(filtro: FiltroFila) {
  return useQuery({
    queryKey: [...raiz, 'fila', filtro],
    queryFn: async () => (await http.get<PaginaFila>('/confirmacoes/fila', { params: params(filtro) })).data,
    placeholderData: (anterior) => anterior,
  });
}

export function useItemFila(id: string | null) {
  return useQuery({
    queryKey: [...raiz, 'fila', 'item', id],
    queryFn: async () => (await http.get<NotificacaoDetalhe>(`/confirmacoes/fila/${id}`)).data,
    enabled: Boolean(id),
  });
}

export function useReenviarConfirmacao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await http.post(`/confirmacoes/fila/${id}/reenviar`);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: raiz }),
  });
}

export function useRespostas(filtro: FiltroRespostas) {
  return useQuery({
    queryKey: [...raiz, 'respostas', filtro],
    queryFn: async () =>
      (await http.get<PaginaRespostas>('/confirmacoes/respostas', { params: params(filtro) })).data,
    placeholderData: (anterior) => anterior,
  });
}

export function useConfiguracaoConfirmacao() {
  return useQuery({
    queryKey: [...raiz, 'configuracao'],
    queryFn: async () => (await http.get<ConfirmacaoConfiguracao>('/confirmacoes/configuracao')).data,
  });
}

export function useSalvarConfiguracaoConfirmacao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dados: SalvarConfirmacaoConfiguracao) =>
      (await http.put<ConfirmacaoConfiguracao>('/confirmacoes/configuracao', dados)).data,
    onSuccess: (dados) => {
      qc.setQueryData([...raiz, 'configuracao'], dados);
      void qc.invalidateQueries({ queryKey: [...raiz, 'resumo'] });
    },
  });
}

export function usePreviaLote(
  filtro: { unidadeId?: string; de?: string; ate?: string; forcar?: boolean },
  ativo: boolean,
) {
  return useQuery({
    queryKey: [...raiz, 'lote', 'previa', filtro],
    queryFn: async () =>
      (await http.get<PreviaLote>('/confirmacoes/lote/previa', { params: params(filtro) })).data,
    enabled: ativo,
  });
}

export function useDispararLote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dados: {
      unidadeId?: string;
      de?: string;
      ate?: string;
      forcar?: boolean;
      ignorarJanela?: boolean;
    }) =>
      (await http.post<PreviaLote>('/confirmacoes/lote', dados)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: raiz }),
  });
}

export function useRegrasUnidades() {
  return useQuery({
    queryKey: [...raiz, 'regras', 'unidades'],
    queryFn: async () => (await http.get<RegraUnidade[]>('/confirmacoes/regras/unidades')).data,
  });
}

export function useAlterarRegraUnidade() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ unidadeId, enviar }: { unidadeId: string; enviar: boolean }) => {
      await http.put(`/confirmacoes/regras/unidades/${unidadeId}`, { enviar });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: [...raiz, 'regras'] }),
  });
}
