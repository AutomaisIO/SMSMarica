import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  abrirTreinamento,
  atualizarAssunto,
  catalogoComandos,
  criarAssunto,
  descartarTreinamento,
  desfazerAlteracaoTreinamento,
  dispensarPendenciaTreinamento,
  excluirAssunto,
  listarAssuntos,
  listarErrosRobo,
  listarTreinamento,
  obterAssunto,
  obterConfiguracao,
  obterTreinamento,
  responderPendenciaTreinamento,
  revisarErroRobo,
  simularRobo,
  simularTreinamento,
  salvarConfiguracao,
  treinarItem,
} from '@/features/robo-atendimento/api/roboApi';
import type {
  RoboConfiguracao,
  SimularRoboPayload,
  SalvarRoboAssuntoPayload,
  StatusRoboErro,
  StatusTreinamento,
} from '@/features/robo-atendimento/types';

export const roboKeys = {
  raiz: ['robo-atendimento'] as const,
  lista: (incluirInativos?: boolean) =>
    ['robo-atendimento', 'assuntos', { incluirInativos: !!incluirInativos }] as const,
  assunto: (id: string) => ['robo-atendimento', 'assunto', id] as const,
  catalogo: ['robo-atendimento', 'catalogo-comandos'] as const,
  config: ['robo-atendimento', 'configuracao'] as const,
  erros: (status?: StatusRoboErro) => ['robo-atendimento', 'erros', status ?? 'todos'] as const,
  treinamento: (status?: StatusTreinamento) =>
    ['robo-atendimento', 'treinamento', status ?? 'todos'] as const,
  treinamentoItem: (id: string) => ['robo-atendimento', 'treinamento-item', id] as const,
};

export function useListarAssuntos(incluirInativos = false) {
  return useQuery({
    queryKey: roboKeys.lista(incluirInativos),
    queryFn: () => listarAssuntos(incluirInativos),
  });
}

export function useObterAssunto(id: string | null) {
  return useQuery({
    queryKey: roboKeys.assunto(id ?? ''),
    queryFn: () => obterAssunto(id as string),
    enabled: !!id,
  });
}

export function useCatalogoComandos() {
  return useQuery({ queryKey: roboKeys.catalogo, queryFn: catalogoComandos, staleTime: 1000 * 60 * 30 });
}

export function useCriarAssunto() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarRoboAssuntoPayload) => criarAssunto(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: roboKeys.raiz }),
  });
}

export function useAtualizarAssunto() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarRoboAssuntoPayload }) =>
      atualizarAssunto(id, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: roboKeys.raiz }),
  });
}

export function useExcluirAssunto() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirAssunto(id),
    onSuccess: () => client.invalidateQueries({ queryKey: roboKeys.raiz }),
  });
}

export function useConfiguracaoRobo() {
  return useQuery({ queryKey: roboKeys.config, queryFn: obterConfiguracao });
}

export function useSalvarConfiguracaoRobo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: RoboConfiguracao) => salvarConfiguracao(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: roboKeys.config }),
  });
}

export function useListarErrosRobo(status?: StatusRoboErro) {
  return useQuery({
    queryKey: roboKeys.erros(status),
    queryFn: () => listarErrosRobo(status),
  });
}

export function useRevisarErroRobo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status, nota }: { id: string; status: 'Revisado' | 'Descartado'; nota?: string }) =>
      revisarErroRobo(id, { status, nota: nota || null }),
    onSuccess: () => client.invalidateQueries({ queryKey: ['robo-atendimento', 'erros'] }),
  });
}

export function useSimularRobo() {
  return useMutation({ mutationFn: (payload: SimularRoboPayload) => simularRobo(payload) });
}

// ---- Treinamento ----

/**
 * A análise roda em segundo plano; enquanto houver item em `Analisando`, a lista se recarrega
 * sozinha — sem isso o operador clica em "treinar" e fica olhando uma tela parada.
 */
export function useListarTreinamento(status?: StatusTreinamento) {
  return useQuery({
    queryKey: roboKeys.treinamento(status),
    queryFn: () => listarTreinamento(status),
    refetchInterval: (q) =>
      (q.state.data ?? []).some((i) => i.status === 'Analisando') ? 5000 : false,
  });
}

export function useObterTreinamento(id: string | null) {
  return useQuery({
    queryKey: roboKeys.treinamentoItem(id ?? ''),
    queryFn: () => obterTreinamento(id as string),
    enabled: !!id,
    refetchInterval: (q) => (q.state.data?.status === 'Analisando' ? 4000 : false),
  });
}

function invalidarTreinamento(client: ReturnType<typeof useQueryClient>, id?: string) {
  void client.invalidateQueries({ queryKey: ['robo-atendimento', 'treinamento'] });
  if (id) void client.invalidateQueries({ queryKey: roboKeys.treinamentoItem(id) });
  // A correção mexe em treinos e condições do assunto: a tela de assuntos ficaria desatualizada.
  void client.invalidateQueries({ queryKey: ['robo-atendimento', 'assuntos'] });
}

export function useAbrirTreinamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: { critica: string; roboAssuntoId?: string | null }) => abrirTreinamento(payload),
    onSuccess: () => invalidarTreinamento(client),
  });
}

export function useTreinarItem() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, observacao }: { id: string; observacao?: string }) => treinarItem(id, observacao),
    onSuccess: (_d, v) => invalidarTreinamento(client, v.id),
  });
}

export function useResponderPendenciaTreinamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ pendenciaId, resposta, autorizado }: {
      pendenciaId: string; itemId: string; resposta: string; autorizado?: boolean;
    }) => responderPendenciaTreinamento(pendenciaId, { resposta, autorizado: autorizado ?? null }),
    onSuccess: (_d, v) => invalidarTreinamento(client, v.itemId),
  });
}

export function useDispensarPendenciaTreinamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ pendenciaId, motivo }: { pendenciaId: string; itemId: string; motivo?: string }) =>
      dispensarPendenciaTreinamento(pendenciaId, motivo),
    onSuccess: (_d, v) => invalidarTreinamento(client, v.itemId),
  });
}

export function useDesfazerAlteracaoTreinamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ alteracaoId }: { alteracaoId: string; itemId: string }) =>
      desfazerAlteracaoTreinamento(alteracaoId),
    onSuccess: (_d, v) => invalidarTreinamento(client, v.itemId),
  });
}

export function useSimularTreinamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, mensagem }: { id: string; mensagem?: string }) =>
      simularTreinamento(id, { mensagem: mensagem || null }),
    onSuccess: (_d, v) => invalidarTreinamento(client, v.id),
  });
}

export function useDescartarTreinamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => descartarTreinamento(id),
    onSuccess: (_d, id) => invalidarTreinamento(client, id),
  });
}
