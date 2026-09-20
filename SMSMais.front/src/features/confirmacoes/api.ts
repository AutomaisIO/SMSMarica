import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';
import { params } from '@/features/mensageria/api/comunicacoesApi';
import type {
  AcaoResultado,
  AtendenteConfirmacao,
  EventoAtendimento,
  FiltroAtendimento,
  MotivosTelefoneComprometido,
  PaginaAtendimento,
  ResumoAbas,
} from '@/features/confirmacoes/types';

const raiz = ['confirmacoes', 'atendimento'] as const;

export function useResumoAbas() {
  return useQuery({
    queryKey: [...raiz, 'resumo'],
    queryFn: async () => (await http.get<ResumoAbas>('/confirmacoes/atendimento/resumo')).data,
    refetchInterval: 30_000,
  });
}

export function useAtendimento(filtro: FiltroAtendimento) {
  return useQuery({
    queryKey: [...raiz, 'lista', filtro],
    queryFn: async () =>
      (await http.get<PaginaAtendimento>('/confirmacoes/atendimento', { params: params(filtro) })).data,
    placeholderData: (anterior) => anterior,
    refetchInterval: 30_000,
  });
}

/** Os porquês da aba Telefone comprometido — só busca quando a aba está aberta. */
export function useMotivosTelefoneComprometido(habilitado: boolean) {
  return useQuery({
    queryKey: [...raiz, 'telefone-comprometido', 'motivos'],
    queryFn: async () =>
      (await http.get<MotivosTelefoneComprometido>('/confirmacoes/atendimento/telefone-comprometido/motivos')).data,
    enabled: habilitado,
    refetchInterval: 60_000,
  });
}

export function useAtendentes() {
  return useQuery({
    queryKey: [...raiz, 'atendentes'],
    queryFn: async () => (await http.get<AtendenteConfirmacao[]>('/confirmacoes/atendimento/atendentes')).data,
    staleTime: 5 * 60_000,
  });
}

export function useHistoricoAtendimento(solicitacaoId: string | null) {
  return useQuery({
    queryKey: [...raiz, 'historico', solicitacaoId],
    queryFn: async () =>
      (await http.get<EventoAtendimento[]>(`/confirmacoes/atendimento/${solicitacaoId}/historico`)).data,
    enabled: Boolean(solicitacaoId),
  });
}

type Acao =
  | { tipo: 'atender' | 'assumir' | 'liberar' }
  | { tipo: 'confirmar'; meio?: string; observacao?: string }
  | { tipo: 'cancelar'; motivo: string; meio?: string }
  | { tipo: 'pendente'; motivo: string }
  | { tipo: 'contato-errado'; observacao?: string }
  | { tipo: 'transferir'; paraUsuarioId: string; observacao?: string }
  | { tipo: 'contato-corrigido'; telefone?: string; observacao?: string };

/** Uma mutação para todas as ações do card — invalida as listas ao terminar. */
export function useAcaoAtendimento() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ solicitacaoId, acao }: { solicitacaoId: string; acao: Acao }) => {
      const { tipo, ...corpo } = acao;
      const { data } = await http.post<AcaoResultado>(`/confirmacoes/atendimento/${solicitacaoId}/${tipo}`, corpo);
      return data;
    },
    onSettled: () => qc.invalidateQueries({ queryKey: raiz }),
  });
}
