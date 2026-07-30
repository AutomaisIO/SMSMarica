import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarSolicitacao,
  cadastrarSolicitacao,
  cancelarSolicitacao,
  excluirSolicitacao,
  listarSolicitacoes,
  obterSolicitacao,
  obterSolicitacaoPorStudy,
  reenviarWorklist,
  alterarEquipamentoDestino,
  autorizarSolicitacao,
  listarEquipamentosDoExame,
  obterHistorico,
  reenviarComunicacao,
  registrarContato,
  enviarComunicacaoManual,
  type FinalidadeEnvioManual,
} from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import type {
  AtualizarSolicitacaoPayload,
  CadastrarSolicitacaoPayload,
  FiltroSolicitacoes,
} from '@/features/solicitacoes-exame/types';

export const solicitacoesKeys = {
  raiz: ['solicitacoes-exame'] as const,
  lista: (filtro: FiltroSolicitacoes) => ['solicitacoes-exame', 'lista', filtro] as const,
  porId: (id: string) => ['solicitacoes-exame', 'detalhe', id] as const,
  porStudy: (uid: string) => ['solicitacoes-exame', 'por-study', uid] as const,
};

export function useListarSolicitacoes(filtro: FiltroSolicitacoes) {
  return useQuery({
    queryKey: solicitacoesKeys.lista(filtro),
    queryFn: () => listarSolicitacoes(filtro),
    // Auto-refresh assíncrono: status/checks mudam no servidor (sincronizador PACS,
    // recibos do zap) sem ação do usuário. Refetch em background não pisca a tabela
    // (isPending fica false) e pausa quando a aba perde o foco (default do react-query).
    refetchInterval: 10_000,
  });
}

/**
 * Solicitações do paciente a partir de uma data (para o aviso de duplicada na
 * criação). Só dispara quando há paciente selecionado.
 */
export function useSolicitacoesRecentesPaciente(pacienteId: string | null, dataInicial: string) {
  return useQuery({
    queryKey: ['solicitacoes-exame', 'recentes-paciente', pacienteId, dataInicial],
    queryFn: () => listarSolicitacoes({ pacienteId: pacienteId!, dataInicial, limite: 50 }),
    enabled: Boolean(pacienteId),
    staleTime: 30_000,
  });
}

export function useSolicitacaoPorId(id: string | null) {
  return useQuery({
    queryKey: id ? solicitacoesKeys.porId(id) : ['solicitacoes-exame', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterSolicitacao(id);
    },
    enabled: Boolean(id),
    refetchInterval: 30_000,
  });
}

export function useSolicitacaoPorStudy(studyInstanceUID: string | null) {
  return useQuery({
    queryKey: studyInstanceUID
      ? solicitacoesKeys.porStudy(studyInstanceUID)
      : ['solicitacoes-exame', 'por-study', 'nenhum'],
    queryFn: () => {
      if (!studyInstanceUID) throw new Error('StudyInstanceUID não informado.');
      return obterSolicitacaoPorStudy(studyInstanceUID);
    },
    enabled: Boolean(studyInstanceUID),
  });
}

export function useCadastrarSolicitacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarSolicitacaoPayload) => cadastrarSolicitacao(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: solicitacoesKeys.raiz }),
  });
}

export function useAtualizarSolicitacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarSolicitacaoPayload }) =>
      atualizarSolicitacao(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: solicitacoesKeys.raiz });
      client.invalidateQueries({ queryKey: solicitacoesKeys.porId(v.id) });
    },
  });
}

export function useCancelarSolicitacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, motivo }: { id: string; motivo: string }) => cancelarSolicitacao(id, motivo),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: solicitacoesKeys.raiz });
      client.invalidateQueries({ queryKey: solicitacoesKeys.porId(v.id) });
    },
  });
}

export function useReenviarWorklist() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reenviarWorklist(id),
    onSuccess: (_d, id) => {
      client.invalidateQueries({ queryKey: solicitacoesKeys.raiz });
      client.invalidateQueries({ queryKey: solicitacoesKeys.porId(id) });
    },
  });
}

/** Troca a estação (equipamento) de destino de um exame já enviado à worklist (ticket #72). */
export function useAlterarEquipamentoDestino() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, equipamentoId }: { id: string; equipamentoId: string }) =>
      alterarEquipamentoDestino(id, equipamentoId),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: solicitacoesKeys.raiz });
      client.invalidateQueries({ queryKey: solicitacoesKeys.porId(v.id) });
      client.invalidateQueries({ queryKey: [...solicitacoesKeys.raiz, 'equipamentos', v.id] });
    },
  });
}

/**
 * Estações elegíveis para o exame. Só busca quando o card de autorização está em uso
 * (`habilitado`) — a lista muda pouco, então 5 min de cache evitam ida a cada render.
 */
export function useEquipamentosDoExame(id: string | null, habilitado = true) {
  return useQuery({
    queryKey: [...solicitacoesKeys.raiz, 'equipamentos', id ?? 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('Id não informado.');
      return listarEquipamentosDoExame(id);
    },
    enabled: !!id && habilitado,
    staleTime: 5 * 60_000,
  });
}

export function useAutorizarSolicitacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      chaveConfirmacao,
      equipamentoId,
    }: {
      id: string;
      chaveConfirmacao: string;
      equipamentoId?: string | null;
    }) => autorizarSolicitacao(id, chaveConfirmacao, equipamentoId),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: solicitacoesKeys.raiz });
      client.invalidateQueries({ queryKey: solicitacoesKeys.porId(v.id) });
    },
  });
}

export function useHistoricoSolicitacao(id: string | null) {
  return useQuery({
    queryKey: id ? [...solicitacoesKeys.raiz, 'historico', id] : [...solicitacoesKeys.raiz, 'historico', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('Id não informado.');
      return obterHistorico(id);
    },
    enabled: Boolean(id),
  });
}

export function useReenviarComunicacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comunicacaoId }: { id: string; comunicacaoId: string }) =>
      reenviarComunicacao(id, comunicacaoId),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: [...solicitacoesKeys.raiz, 'historico', v.id] });
      client.invalidateQueries({ queryKey: ['solicitacoes-exame', 'lista'] }); // checks da lista
    },
  });
}

export function useEnviarComunicacaoManual() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, finalidade, assumirRisco }: { id: string; finalidade: FinalidadeEnvioManual; assumirRisco: boolean }) =>
      enviarComunicacaoManual(id, finalidade, assumirRisco),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: [...solicitacoesKeys.raiz, 'historico', v.id] });
      client.invalidateQueries({ queryKey: ['solicitacoes-exame', 'lista'] }); // checks da lista
    },
  });
}

export function useRegistrarContato() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, meio, resultado, observacao }: { id: string; meio: string; resultado: string; observacao: string | null }) =>
      registrarContato(id, { meio, resultado, observacao }),
    onSuccess: (_d, v) =>
      client.invalidateQueries({ queryKey: [...solicitacoesKeys.raiz, 'historico', v.id] }),
  });
}

export function useExcluirSolicitacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, force }: { id: string; force?: boolean }) => excluirSolicitacao(id, force ?? false),
    onSuccess: () => client.invalidateQueries({ queryKey: solicitacoesKeys.raiz }),
  });
}
