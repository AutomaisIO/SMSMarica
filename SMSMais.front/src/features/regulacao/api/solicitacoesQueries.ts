import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  anexarArquivo,
  assumirSolicitacao,
  cancelarSolicitacao,
  devolverSolicitacao,
  listarEventos,
  obterElegibilidade,
  listarNotificacoesRegulacao,
  listarSolicitacoes,
  confirmarOkInterno,
  marcarNotificacaoVista,
  marcarNotificacoesDaSolicitacaoVistas,
  obterResumoFila,
  obterResumoNotificacoesRegulacao,
  recusarSolicitacao,
  responderRegras,
  registrarEnvioSolicitacao,
  atualizarSolicitacao,
  criarSolicitacao,
  enviarParaFila,
  listarExigencias,
  obterFormularioRegulacao,
  obterPendencias,
  obterSolicitacao,
  removerArquivo,
  type AtualizarSolicitacaoPayload,
} from './solicitacoesApi';
import type {
  EscopoNotificacao,
  RespostaRegraRegulacao,
  FiltroSolicitacoesRegulacao,
  FluxoRegulacao,
} from '../tiposSolicitacao';
import { useTemConsulta } from '@/shared/auth/authStore';

const raiz = ['regulacao', 'solicitacoes'] as const;

export function useFormularioRegulacao(procedimentoId: string | null, fluxo: FluxoRegulacao | null) {
  return useQuery({
    queryKey: [...raiz, 'formulario', procedimentoId, fluxo],
    queryFn: () => obterFormularioRegulacao(procedimentoId!, fluxo!),
    enabled: !!procedimentoId && !!fluxo,
    // A definição só muda quando a SES recompila o catálogo — não vale rebuscar a cada foco.
    staleTime: 5 * 60_000,
  });
}

export function useSolicitacao(id: string | null) {
  return useQuery({
    queryKey: [...raiz, id],
    queryFn: () => obterSolicitacao(id!),
    enabled: !!id,
  });
}

export function usePendencias(id: string | null) {
  return useQuery({
    queryKey: [...raiz, id, 'pendencias'],
    queryFn: () => obterPendencias(id!),
    enabled: !!id,
  });
}

export function useExigencias(solicitacaoId: string | null) {
  return useQuery({
    queryKey: [...raiz, solicitacaoId, 'exigencias'],
    queryFn: () => listarExigencias(solicitacaoId!),
    enabled: !!solicitacaoId,
  });
}

function useInvalidar() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: raiz });
}

export function useCriarSolicitacao() {
  const invalidar = useInvalidar();
  return useMutation({ mutationFn: criarSolicitacao, onSuccess: invalidar });
}

export function useAtualizarSolicitacao() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: ({ id, ...p }: { id: string } & AtualizarSolicitacaoPayload) =>
      atualizarSolicitacao(id, p),
    onSuccess: invalidar,
  });
}

export function useEnviarParaFila() {
  const invalidar = useInvalidar();
  return useMutation({ mutationFn: enviarParaFila, onSuccess: invalidar });
}

export function useAnexarArquivo() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: (p: { solicitacaoId: string; exigenciaId: string; arquivo: File }) =>
      anexarArquivo(p.solicitacaoId, p.exigenciaId, p.arquivo),
    onSuccess: invalidar,
  });
}

export function useRemoverArquivo() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: (p: { solicitacaoId: string; arquivoId: string }) =>
      removerArquivo(p.solicitacaoId, p.arquivoId),
    onSuccess: invalidar,
  });
}

// ---------------------------------------------------------------- fila (plano 04)

export function useSolicitacoes(filtro: FiltroSolicitacoesRegulacao) {
  return useQuery({
    queryKey: [...raiz, 'lista', filtro],
    queryFn: () => listarSolicitacoes(filtro),
    // A fila é compartilhada: enquanto a tela está aberta, outro agente assume um caso e o
    // status muda. Dado velho aqui faz alguém clicar em "assumir" no que já é de outra pessoa.
    staleTime: 15_000,
    // Mantém a página anterior visível enquanto a nova carrega — sem isto, trocar de aba pisca
    // uma tabela vazia.
    placeholderData: (anterior) => anterior,
  });
}

/**
 * Contagem por status. Alimenta as abas da fila, e é também por aqui que a tela sabe se está
 * vendo o município inteiro ou só a própria unidade.
 */
export function useResumoFilaRegulacao() {
  const podeVer = useTemConsulta('Regulacao');
  return useQuery({
    queryKey: [...raiz, 'resumo'],
    queryFn: obterResumoFila,
    enabled: podeVer,
    staleTime: 15_000,
    refetchInterval: 60_000,
  });
}

export function useEventosSolicitacao(id: string | null) {
  return useQuery({
    queryKey: [...raiz, id, 'eventos'],
    queryFn: () => listarEventos(id!),
    enabled: !!id,
  });
}

// ---------------------------------------------------------------- ações do agente (módulo 48)

/**
 * Todas invalidam a raiz `['regulacao','solicitacoes']`: uma ação muda o detalhe, a fila e as
 * contagens das abas ao mesmo tempo, e invalidar só o detalhe deixaria a fila mentindo.
 */
function useAcaoDeSolicitacao<T>(fn: (v: T) => Promise<unknown>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => void qc.invalidateQueries({ queryKey: raiz }),
  });
}

export function useAssumirSolicitacao() {
  return useAcaoDeSolicitacao((id: string) => assumirSolicitacao(id));
}

export function useDevolverSolicitacao() {
  return useAcaoDeSolicitacao(({ id, motivo }: { id: string; motivo: string }) =>
    devolverSolicitacao(id, motivo),
  );
}

export function useRecusarSolicitacao() {
  return useAcaoDeSolicitacao(({ id, motivo }: { id: string; motivo: string }) =>
    recusarSolicitacao(id, motivo),
  );
}

export function useCancelarSolicitacao() {
  return useAcaoDeSolicitacao(({ id, motivo }: { id: string; motivo: string }) =>
    cancelarSolicitacao(id, motivo),
  );
}

export function useRegistrarEnvio() {
  return useAcaoDeSolicitacao(
    ({ id, sistema, numeroExterno }: { id: string; sistema: string; numeroExterno: string }) =>
      registrarEnvioSolicitacao(id, { sistema, numeroExterno }),
  );
}

export function useOkInterno() {
  return useAcaoDeSolicitacao((id: string) => confirmarOkInterno(id));
}

// ---------------------------------------------------------------- notificações (plano 05)

const raizNotificacoes = ['regulacao', 'notificacoes'] as const;

export function useNotificacoesRegulacao(escopo: EscopoNotificacao, soNaoVistas: boolean) {
  return useQuery({
    queryKey: [...raizNotificacoes, escopo, soNaoVistas],
    queryFn: () => listarNotificacoesRegulacao(escopo, soNaoVistas),
    // A varredura roda em segundo plano; sem o refetch, a tela ficaria mostrando o mundo de
    // quando foi aberta.
    refetchInterval: 60_000,
  });
}

export function useResumoNotificacoesRegulacao(escopo: EscopoNotificacao) {
  const podeVer = useTemConsulta('Regulacao');
  return useQuery({
    queryKey: [...raizNotificacoes, 'resumo', escopo],
    queryFn: () => obterResumoNotificacoesRegulacao(escopo),
    enabled: podeVer,
    refetchInterval: 60_000,
  });
}

export function useMarcarNotificacaoVista() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: marcarNotificacaoVista,
    onSuccess: () => void qc.invalidateQueries({ queryKey: raizNotificacoes }),
  });
}

export function useMarcarNotificacoesDaSolicitacaoVistas() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: marcarNotificacoesDaSolicitacaoVistas,
    onSuccess: () => void qc.invalidateQueries({ queryKey: raizNotificacoes }),
  });
}

// ---------------------------------------------------------------- elegibilidade (plano 03)

/**
 * A avaliação das regras. `enabled` pelo id porque ela só existe depois de a solicitação virar
 * rascunho — antes disso não há o que avaliar.
 */
export function useElegibilidade(id: string | null) {
  return useQuery({
    queryKey: [...raiz, id, 'elegibilidade'],
    queryFn: () => obterElegibilidade(id!),
    enabled: !!id,
  });
}

export function useResponderRegras() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, respostas }: { id: string; respostas: Record<string, RespostaRegraRegulacao> }) =>
      responderRegras(id, respostas),
    // Responder muda destinos e caixinhas: invalida a raiz inteira, não só a avaliação.
    onSuccess: () => void qc.invalidateQueries({ queryKey: raiz }),
  });
}
