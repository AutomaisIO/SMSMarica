import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  alternarSincronismoAutomatico,
  atualizarConfiguracaoSisreg,
  cancelarMapeamentoLote,
  listarExecucoesMapeamentoLote,
  listarItensMapeamentoLote,
  obterAgendamentoMapeamentoLote,
  obterConfiguracaoSisreg,
  alternarAgendamentoRede,
  listarTelefonesNotificacao,
  obterStatusMapeamentoLote,
  prepararRedeSisreg,
  preverAgendamentoSisreg,
  salvarAgendamentoMapeamentoLote,
  salvarTelefonesNotificacao,
  sincronizarMapeamentoLote,
  testarNotificacaoSincronismo,
} from '@/features/sisreg/api/sisregApi';
import type {
  AtualizarSisregConfiguracaoPayload,
  PrepararRedePayload,
  PreverAgendamentoPayload,
  SalvarMapeamentoLoteAgendamento,
} from '@/features/sisreg/types';

export const sisregKeys = {
  configuracao: ['sisreg', 'configuracao'] as const,
  loteStatus: ['sisreg', 'lote', 'status'] as const,
  loteAgendamento: ['sisreg', 'lote', 'agendamento'] as const,
  loteExecucoes: ['sisreg', 'lote', 'execucoes'] as const,
  loteItens: (id: string) => ['sisreg', 'lote', 'execucoes', id, 'itens'] as const,
  telefonesNotificacao: (provedor: string) => ['integracoes', provedor, 'notificacoes'] as const,
};

export function useConfiguracaoSisreg() {
  return useQuery({
    queryKey: sisregKeys.configuracao,
    queryFn: obterConfiguracaoSisreg,
  });
}

export function useAtualizarConfiguracaoSisreg() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AtualizarSisregConfiguracaoPayload) => atualizarConfiguracaoSisreg(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.configuracao }),
  });
}

/**
 * Invalida a configuração no sucesso para a tela refletir o estado que o servidor confirmou —
 * e não o que o clique supôs.
 */
export function useAlternarSincronismoAutomatico() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (ativo: boolean) => alternarSincronismoAutomatico(ativo),
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.configuracao }),
  });
}

// ----------------------------------------------------------- lote "sincroniza tudo" (#118)

/**
 * Enquanto há um lote rodando, refaz o status a cada 3s para a barra de progresso andar.
 *
 * `acompanhando` existe porque o lote leva 1–2s para se registrar depois do clique: a primeira
 * resposta ainda vem `null`, e sem esse empurrão a tela parava de perguntar e ficava congelada em
 * "nada rodando" durante a sincronização inteira. Mesma solução da varredura por unidade.
 */
export function useStatusMapeamentoLote(acompanhando = false) {
  return useQuery({
    queryKey: sisregKeys.loteStatus,
    queryFn: obterStatusMapeamentoLote,
    refetchInterval: (query) => (query.state.data?.emExecucao || acompanhando ? 3000 : false),
  });
}

/** Histórico das sincronizações. Acompanha junto do status para a linha nova aparecer sozinha. */
export function useExecucoesMapeamentoLote(acompanhando = false) {
  return useQuery({
    queryKey: sisregKeys.loteExecucoes,
    queryFn: () => listarExecucoesMapeamentoLote(10),
    refetchInterval: acompanhando ? 5000 : false,
  });
}

export function useItensMapeamentoLote(id: string | null) {
  return useQuery({
    queryKey: sisregKeys.loteItens(id ?? ''),
    queryFn: () => listarItensMapeamentoLote(id!),
    enabled: Boolean(id),
  });
}

export function useSincronizarMapeamentoLote() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: sincronizarMapeamentoLote,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: sisregKeys.loteStatus });
      void client.invalidateQueries({ queryKey: sisregKeys.loteExecucoes });
    },
  });
}

export function useCancelarMapeamentoLote() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: cancelarMapeamentoLote,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: sisregKeys.loteStatus });
      void client.invalidateQueries({ queryKey: sisregKeys.loteExecucoes });
    },
  });
}

export function useAgendamentoMapeamentoLote() {
  return useQuery({
    queryKey: sisregKeys.loteAgendamento,
    queryFn: obterAgendamentoMapeamentoLote,
  });
}

export function useSalvarAgendamentoMapeamentoLote() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarMapeamentoLoteAgendamento) => salvarAgendamentoMapeamentoLote(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.loteAgendamento }),
  });
}

/**
 * Programa a rede inteira. Invalida o agendamento porque a contagem de pendentes e o orçamento
 * mudam junto — e é por eles que a tela decide o que oferecer.
 */
export function usePrepararRedeSisreg() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: PrepararRedePayload) => prepararRedeSisreg(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.loteAgendamento }),
  });
}

export function useTelefonesNotificacao(provedor: string) {
  return useQuery({
    queryKey: sisregKeys.telefonesNotificacao(provedor),
    queryFn: () => listarTelefonesNotificacao(provedor),
  });
}

export function useSalvarTelefonesNotificacao(provedor: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (telefones: string[]) => salvarTelefonesNotificacao(provedor, telefones),
    onSuccess: () =>
      client.invalidateQueries({ queryKey: sisregKeys.telefonesNotificacao(provedor) }),
  });
}

export function useTestarNotificacaoSincronismo(provedor: string) {
  return useMutation({ mutationFn: () => testarNotificacaoSincronismo(provedor) });
}

/**
 * Prévia da distribuição. `keepPreviousData` deixa o resumo anterior na tela enquanto o novo
 * carrega: sem isso, cada tecla digitada no intervalo apagava a linha e ela piscava.
 */
export function usePreverAgendamento(payload: PreverAgendamentoPayload, habilitado: boolean) {
  return useQuery({
    queryKey: ['sisreg', 'lote', 'prever', payload.intervaloMinutos, payload.horaInicialLocal],
    queryFn: () => preverAgendamentoSisreg(payload),
    enabled: habilitado,
    placeholderData: (anterior) => anterior,
  });
}

export function useAlternarAgendamentoRede() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (ativo: boolean) => alternarAgendamentoRede(ativo),
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.loteAgendamento }),
  });
}
