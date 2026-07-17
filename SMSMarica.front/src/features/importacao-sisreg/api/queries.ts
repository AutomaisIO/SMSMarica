import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cancelarLote,
  descartarFalhaImportacao,
  importarLote,
  listarExecucoesImportacao,
  listarFalhasImportacao,
  obterFalhaDetalhe,
  obterStatusLote,
  reprocessarFalhaImportacao,
} from '@/features/importacao-sisreg/api/importacaoApi';

export const importacaoKeys = {
  falhas: (somentePendentes: boolean) => ['importacao-sisreg', 'falhas', somentePendentes] as const,
  falhaDetalhe: (id: string) => ['importacao-sisreg', 'falha-detalhe', id] as const,
  statusLote: ['importacao-sisreg', 'lote-status'] as const,
  execucoes: ['importacao-sisreg', 'execucoes'] as const,
};

export function useFalhasImportacao(somentePendentes: boolean) {
  return useQuery({
    queryKey: importacaoKeys.falhas(somentePendentes),
    queryFn: () => listarFalhasImportacao(somentePendentes),
  });
}

export function useFalhaDetalhe(id: string | null) {
  return useQuery({
    queryKey: importacaoKeys.falhaDetalhe(id ?? ''),
    queryFn: () => obterFalhaDetalhe(id!),
    enabled: id != null,
  });
}

/** Tudo que muda o lado de uma falha precisa recarregar as falhas E o rastreio. */
function invalidarFalhasERastreio(client: ReturnType<typeof useQueryClient>) {
  client.invalidateQueries({ queryKey: ['importacao-sisreg', 'falhas'] });
  client.invalidateQueries({ queryKey: importacaoKeys.execucoes });
}

export function useReprocessarFalha() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: reprocessarFalhaImportacao,
    onSuccess: () => invalidarFalhasERastreio(client),
  });
}

export function useDescartarFalha() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, nota }: { id: string; nota?: string }) => descartarFalhaImportacao(id, nota),
    onSuccess: () => invalidarFalhasERastreio(client),
  });
}

/**
 * Status do lote com polling adaptativo: 1s enquanto há importação viva OU enquanto esperamos o
 * runner começar (`aguardandoInicio`) — o POST responde 202 antes de o lote iniciar, e nessa
 * janela o status ainda volta como o resumo do lote anterior (emExecucao=false). Sem o segundo
 * gatilho o polling se desligaria e o lote rodaria invisível. Fora disso, não fica batendo.
 */
export function useStatusLote(ativo: boolean, aguardandoInicio = false) {
  return useQuery({
    queryKey: importacaoKeys.statusLote,
    queryFn: obterStatusLote,
    enabled: ativo,
    refetchInterval: (query) => (query.state.data?.emExecucao || aguardandoInicio ? 1000 : false),
  });
}

export function useExecucoesImportacao() {
  return useQuery({
    queryKey: importacaoKeys.execucoes,
    queryFn: () => listarExecucoesImportacao(),
  });
}

export function useImportarLote() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: importarLote,
    onSuccess: () => {
      client.invalidateQueries({ queryKey: importacaoKeys.statusLote });
      client.invalidateQueries({ queryKey: importacaoKeys.execucoes });
    },
  });
}

export function useCancelarLote() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: cancelarLote,
    onSuccess: () => client.invalidateQueries({ queryKey: importacaoKeys.statusLote }),
  });
}
