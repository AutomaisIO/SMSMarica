import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  alternarEnvioConfirmacao,
  alternarTudoDaUnidade,
  alternarProcedimento,
  alternarProcedimentosDoProfissional,
  alternarProfissional,
  alternarProfissionaisEmLote,
  confirmarDeParaSigtap,
  listarDeParaSigtap,
  obterMapeamento,
  sugerirDeParaSigtap,
} from '@/features/sisreg-mapeamento/api/mapeamentoApi';
import {
  alternarHistoricoVarredura,
  cancelarVarredura,
  executarVarredura,
  executarVarreduraPeriodo,
  importarProcedimento,
  listarVarreduraExecucoes,
  listarVarreduraItens,
  obterStatusVarredura,
  obterVarreduraAgenda,
  salvarVarreduraAgenda,
} from '@/features/sisreg-mapeamento/api/varreduraApi';
import type {
  AlternarHistoricoPayload,
  ImportarAgendaPontualPayload,
  SalvarVarreduraAgendaPayload,
} from '@/features/sisreg-mapeamento/types';

export const mapeamentoKeys = {
  /** A unidade entra na chave: trocar de unidade tem que trocar de mapeamento. */
  mapeamento: (unidadeId: string | null) => ['sisreg-mapeamento', unidadeId] as const,
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
  varreduraItens: (execucaoId: string) =>
    ['sisreg-mapeamento', 'varredura-itens', execucaoId] as const,
};

export function useMapeamento(unidadeId: string | null) {
  return useQuery({
    queryKey: mapeamentoKeys.mapeamento(unidadeId),
    queryFn: () => obterMapeamento(unidadeId),
    enabled: Boolean(unidadeId),
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

/**
 * "Habilitar tudo" da unidade. Invalida mapeamento E agenda porque o número de combinações
 * habilitadas é o custo estimado da varredura — que aparece no bloco de sincronismo logo abaixo.
 */
export function useAlternarTudoDaUnidade(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ habilitados }: { habilitados: boolean }) =>
      alternarTudoDaUnidade(habilitados, unidadeId),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) });
      client.invalidateQueries({ queryKey: mapeamentoKeys.agenda(unidadeId) });
    },
  });
}

export function useAlternarProcedimentosDoProfissional(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      habilitados,
      enviarConfirmacao,
    }: {
      id: string;
      habilitados: boolean;
      enviarConfirmacao: boolean;
    }) => alternarProcedimentosDoProfissional(id, habilitados, enviarConfirmacao, unidadeId),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) });
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

/**
 * Liga/desliga a importação do passado da unidade. Invalida a agenda porque é lá que mora a
 * cobertura (`historicoCobertoDe`), que é o que a tela mostra para responder "já foi feito?".
 */
export function useAlternarHistoricoVarredura(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AlternarHistoricoPayload) => alternarHistoricoVarredura(payload, unidadeId),
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

export function useExecutarVarreduraPeriodo(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ dataInicio, dataFim }: { dataInicio: string; dataFim: string }) =>
      executarVarreduraPeriodo(dataInicio, dataFim, unidadeId),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: mapeamentoKeys.varreduraStatus(unidadeId) });
      client.invalidateQueries({ queryKey: mapeamentoKeys.varreduraExecucoes(unidadeId) });
    },
  });
}

/**
 * Import PONTUAL de um procedimento (o botão da árvore). Síncrono — a mutation resolve com o
 * resumo. Ao terminar, o histórico de execuções pode ter mudado; invalida-o para refletir.
 */
export function useImportarProcedimento(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: ImportarAgendaPontualPayload) => importarProcedimento(payload, unidadeId),
    onSuccess: () =>
      client.invalidateQueries({ queryKey: mapeamentoKeys.varreduraExecucoes(unidadeId) }),
  });
}

/** Detalhe de uma execução. Só busca quando o modal abre (execucaoId definido). */
export function useVarreduraItens(unidadeId: string | null, execucaoId: string | null) {
  return useQuery({
    queryKey: mapeamentoKeys.varreduraItens(execucaoId ?? ''),
    queryFn: () => listarVarreduraItens(execucaoId!, unidadeId),
    enabled: Boolean(execucaoId),
  });
}

/**
 * Progresso da varredura em curso.
 *
 * <p><b>Por que `acompanhando` existe:</b> a versão anterior só refazia a consulta enquanto já
 * houvesse dados. Ao disparar, a varredura leva 1–2 s para se registrar — o primeiro retorno vinha
 * vazio, o poll nunca começava, e a tela ficava congelada no que tinha visto por último. Quem
 * dispara passa a acompanhar por um tempo, mesmo sem resposta ainda.</p>
 *
 * <p>O custo é do nosso servidor, não do SISREG: este endpoint lê um objeto em memória.</p>
 */
export function useStatusVarredura(unidadeId: string | null, acompanhando = false) {
  return useQuery({
    queryKey: mapeamentoKeys.varreduraStatus(unidadeId),
    queryFn: () => obterStatusVarredura(unidadeId),
    enabled: Boolean(unidadeId),
    refetchInterval: (query) => (acompanhando || query.state.data ? 2000 : false),
  });
}

export function useCancelarVarredura(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => cancelarVarredura(unidadeId),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.varreduraStatus(unidadeId) }),
  });
}

/**
 * Varreduras recentes. Enquanto o operador acompanha, refaz sozinha — é onde o resultado final
 * aparece, e uma varredura pode terminar em 2 s. Sem isso a linha fica em "Rodando" para sempre.
 */
export function useVarreduraExecucoes(unidadeId: string | null, acompanhando = false) {
  return useQuery({
    queryKey: mapeamentoKeys.varreduraExecucoes(unidadeId),
    queryFn: () => listarVarreduraExecucoes(unidadeId),
    enabled: Boolean(unidadeId),
    refetchInterval: acompanhando ? 2000 : false,
  });
}
