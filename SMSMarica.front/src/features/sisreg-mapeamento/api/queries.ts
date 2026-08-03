import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  alternarEnvioConfirmacao,
  alternarProcedimento,
  alternarProfissional,
  alternarProfissionaisEmLote,
  atualizarMapeamento,
  confirmarDeParaSigtap,
  listarDeParaSigtap,
  obterCredencial,
  obterMapeamento,
  removerCredencial,
  salvarCredencial,
  sincronizarFhir,
  sugerirDeParaSigtap,
  testarCredencial,
} from '@/features/sisreg-mapeamento/api/mapeamentoApi';
import {
  cancelarVarredura,
  executarVarredura,
  listarVarreduraExecucoes,
  obterStatusVarredura,
  obterVarreduraAgenda,
  salvarVarreduraAgenda,
} from '@/features/sisreg-mapeamento/api/varreduraApi';
import type {
  SalvarCredencialPayload,
  SalvarVarreduraAgendaPayload,
} from '@/features/sisreg-mapeamento/types';

export const mapeamentoKeys = {
  /** A unidade entra na chave: trocar de unidade tem que trocar de mapeamento. */
  mapeamento: (unidadeId: string | null) => ['sisreg-mapeamento', unidadeId] as const,
  credencial: (unidadeId: string | null) => ['sisreg-mapeamento', 'credencial', unidadeId] as const,
  /** O de-para é catálogo GLOBAL — sem unidade na chave, de propósito. */
  dePara: (somenteNaoConfirmados?: boolean) =>
    somenteNaoConfirmados === undefined
      ? (['sisreg-mapeamento', 'de-para'] as const)
      : (['sisreg-mapeamento', 'de-para', somenteNaoConfirmados] as const),
  agenda: (unidadeId: string | null) => ['sisreg-mapeamento', 'varredura-agenda', unidadeId] as const,
  varreduraStatus: (unidadeId: string | null) =>
    ['sisreg-mapeamento', 'varredura-status', unidadeId] as const,
  varreduraExecucoes: (unidadeId: string | null) =>
    ['sisreg-mapeamento', 'varredura-execucoes', unidadeId] as const,
};

export function useMapeamento(unidadeId: string | null) {
  return useQuery({
    queryKey: mapeamentoKeys.mapeamento(unidadeId),
    queryFn: () => obterMapeamento(unidadeId),
    enabled: Boolean(unidadeId),
  });
}

export function useCredencialUnidade(unidadeId: string | null) {
  return useQuery({
    queryKey: mapeamentoKeys.credencial(unidadeId),
    queryFn: () => obterCredencial(unidadeId),
    enabled: Boolean(unidadeId),
  });
}

export function useAtualizarMapeamento(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => atualizarMapeamento(unidadeId),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) });
      // Atualizar o mapeamento cataloga procedimentos novos no de-para; a lista de pendentes muda.
      client.invalidateQueries({ queryKey: mapeamentoKeys.dePara() });
    },
  });
}

export function useAlternarProfissional(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, habilitado }: { id: string; habilitado: boolean }) =>
      alternarProfissional(id, habilitado, unidadeId),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) });
      client.invalidateQueries({ queryKey: mapeamentoKeys.agenda(unidadeId) });
    },
  });
}

export function useAlternarProcedimento(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, habilitado }: { id: string; habilitado: boolean }) =>
      alternarProcedimento(id, habilitado, unidadeId),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) });
      // Habilitar/desabilitar muda o custo estimado da varredura mostrado no bloco de sincronismo.
      client.invalidateQueries({ queryKey: mapeamentoKeys.agenda(unidadeId) });
    },
  });
}

export function useAlternarProfissionaisEmLote(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ ids, habilitado }: { ids: string[]; habilitado: boolean }) =>
      alternarProfissionaisEmLote(ids, habilitado, unidadeId),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) });
      client.invalidateQueries({ queryKey: mapeamentoKeys.agenda(unidadeId) });
    },
  });
}

export function useSincronizarFhir(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => sincronizarFhir(unidadeId),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) }),
  });
}

// ------------------------------------------------------- de-para pa → SIGTAP

export function useDeParaSigtap(somenteNaoConfirmados = false) {
  return useQuery({
    queryKey: mapeamentoKeys.dePara(somenteNaoConfirmados),
    queryFn: () => listarDeParaSigtap(somenteNaoConfirmados),
  });
}

export function useSugerirDeParaSigtap() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: sugerirDeParaSigtap,
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.dePara() }),
  });
}

export function useAlternarEnvioConfirmacao(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, enviar }: { id: string; enviar: boolean }) =>
      alternarEnvioConfirmacao(id, enviar, unidadeId),
    // A decisão é desta unidade, e o back propaga para todas as linhas do mesmo procedimento
    // nela — por isso recarrega o mapeamento inteiro da unidade, não só a linha clicada.
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) }),
  });
}

export function useConfirmarDeParaSigtap() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, procedimentoSigtapId }: { id: string; procedimentoSigtapId: string }) =>
      confirmarDeParaSigtap(id, procedimentoSigtapId),
    onSuccess: () => {
      // Confirmar o de-para libera procedimentos para a varredura em TODAS as unidades — o
      // catálogo é global. Por isso invalida o mapeamento inteiro, não só o da unidade aberta.
      client.invalidateQueries({ queryKey: ['sisreg-mapeamento'] });
    },
  });
}

// ------------------------------------------------------- varredura da agenda

export function useVarreduraAgenda(unidadeId: string | null) {
  return useQuery({
    queryKey: mapeamentoKeys.agenda(unidadeId),
    queryFn: () => obterVarreduraAgenda(unidadeId),
    enabled: Boolean(unidadeId),
  });
}

export function useSalvarVarreduraAgenda(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarVarreduraAgendaPayload) => salvarVarreduraAgenda(payload, unidadeId),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.agenda(unidadeId) }),
  });
}

export function useExecutarVarredura(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => executarVarredura(unidadeId),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: mapeamentoKeys.varreduraStatus(unidadeId) });
      client.invalidateQueries({ queryKey: mapeamentoKeys.varreduraExecucoes(unidadeId) });
    },
  });
}

/**
 * Enquanto há varredura viva, faz poll de 3s. Parada, o poll cessa — a varredura leva minutos e
 * o custo aqui é do nosso servidor, não do SISREG.
 */
export function useStatusVarredura(unidadeId: string | null) {
  return useQuery({
    queryKey: mapeamentoKeys.varreduraStatus(unidadeId),
    queryFn: () => obterStatusVarredura(unidadeId),
    enabled: Boolean(unidadeId),
    refetchInterval: (query) => (query.state.data ? 3000 : false),
  });
}

export function useCancelarVarredura(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => cancelarVarredura(unidadeId),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.varreduraStatus(unidadeId) }),
  });
}

export function useVarreduraExecucoes(unidadeId: string | null) {
  return useQuery({
    queryKey: mapeamentoKeys.varreduraExecucoes(unidadeId),
    queryFn: () => listarVarreduraExecucoes(unidadeId),
    enabled: Boolean(unidadeId),
  });
}

export function useSalvarCredencial(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarCredencialPayload) => salvarCredencial(payload, unidadeId),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.credencial(unidadeId) }),
  });
}

export function useTestarCredencial(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => testarCredencial(unidadeId),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.credencial(unidadeId) }),
  });
}

export function useRemoverCredencial(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => removerCredencial(unidadeId),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.credencial(unidadeId) }),
  });
}
