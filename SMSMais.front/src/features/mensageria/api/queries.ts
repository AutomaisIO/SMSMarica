import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  alterarRegraUnidade,
  dispararLote,
  enviarTesteModelo,
  listarModelos,
  listarNotificacoes,
  listarRegrasUnidades,
  listarRespostas,
  obterConfiguracaoConfirmacao,
  obterConfiguracaoMensageria,
  obterNotificacao,
  obterPreviaLote,
  obterResumoDiario,
  reenviarNotificacao,
  salvarConfiguracaoConfirmacao,
  salvarConfiguracaoMensageria,
} from '@/features/mensageria/api/comunicacoesApi';
import type {
  DisparoLote,
  FiltroLote,
  FiltroRespostas,
  NotificacaoFiltro,
  SalvarConfirmacaoConfiguracao,
  SalvarMensageriaConfiguracao,
} from '@/features/mensageria/types';

export const mensageriaKeys = {
  raiz: ['mensageria'] as const,
  lista: (filtro: NotificacaoFiltro) => ['mensageria', 'lista', filtro] as const,
  detalhe: (id: string) => ['mensageria', 'detalhe', id] as const,
  resumoDiario: (filtro: object) => ['mensageria', 'resumo-diario', filtro] as const,
  configuracaoMensageria: ['mensageria', 'configuracao-mensageria'] as const,
  respostas: (filtro: FiltroRespostas) => ['mensageria', 'respostas', filtro] as const,
  configuracaoConfirmacao: ['mensageria', 'configuracao-confirmacao'] as const,
  previaLote: (filtro: FiltroLote) => ['mensageria', 'lote', 'previa', filtro] as const,
  regras: ['mensageria', 'regras', 'unidades'] as const,
};

/** @deprecated use mensageriaKeys */
export const notificacoesKeys = mensageriaKeys;

export function useNotificacoes(filtro: NotificacaoFiltro) {
  return useQuery({
    queryKey: mensageriaKeys.lista(filtro),
    queryFn: () => listarNotificacoes(filtro),
    placeholderData: (anterior) => anterior,
  });
}

export function useNotificacaoDetalhe(id: string | null) {
  return useQuery({
    queryKey: id ? mensageriaKeys.detalhe(id) : ['mensageria', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('Id não informado.');
      return obterNotificacao(id);
    },
    enabled: Boolean(id),
  });
}

export function useReenviarNotificacao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reenviarNotificacao(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: mensageriaKeys.raiz }),
  });
}

export function useResumoDiario(filtro: { de?: string; ate?: string; finalidade?: string; unidadeId?: string }) {
  return useQuery({
    queryKey: mensageriaKeys.resumoDiario(filtro),
    queryFn: () => obterResumoDiario(filtro),
    placeholderData: (anterior) => anterior,
  });
}

export function useConfiguracaoMensageria() {
  return useQuery({
    queryKey: mensageriaKeys.configuracaoMensageria,
    queryFn: obterConfiguracaoMensageria,
  });
}

export function useSalvarConfiguracaoMensageria() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dados: SalvarMensageriaConfiguracao) => salvarConfiguracaoMensageria(dados),
    onSuccess: (dados) => qc.setQueryData(mensageriaKeys.configuracaoMensageria, dados),
  });
}

export function useRespostas(filtro: FiltroRespostas) {
  return useQuery({
    queryKey: mensageriaKeys.respostas(filtro),
    queryFn: () => listarRespostas(filtro),
    placeholderData: (anterior) => anterior,
  });
}

export function useConfiguracaoConfirmacao() {
  return useQuery({
    queryKey: mensageriaKeys.configuracaoConfirmacao,
    queryFn: obterConfiguracaoConfirmacao,
  });
}

export function useSalvarConfiguracaoConfirmacao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dados: SalvarConfirmacaoConfiguracao) => salvarConfiguracaoConfirmacao(dados),
    onSuccess: (dados) => qc.setQueryData(mensageriaKeys.configuracaoConfirmacao, dados),
  });
}

export function useModelosWhatsApp() {
  return useQuery({
    queryKey: [...mensageriaKeys.raiz, 'modelos'],
    queryFn: listarModelos,
    staleTime: 5 * 60_000, // catálogo da Meta muda pouco
  });
}

export function useEnviarTesteModelo() {
  return useMutation({
    mutationFn: (dados: { telefone: string; modelo: string; parametros?: string[] }) =>
      enviarTesteModelo(dados),
  });
}

export function usePreviaLote(filtro: FiltroLote, ativo: boolean) {
  return useQuery({
    queryKey: mensageriaKeys.previaLote(filtro),
    queryFn: () => obterPreviaLote(filtro),
    enabled: ativo,
  });
}

export function useDispararLote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dados: DisparoLote) => dispararLote(dados),
    onSuccess: () => qc.invalidateQueries({ queryKey: mensageriaKeys.raiz }),
  });
}

export function useRegrasUnidades() {
  return useQuery({
    queryKey: mensageriaKeys.regras,
    queryFn: listarRegrasUnidades,
  });
}

export function useAlterarRegraUnidade() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ unidadeId, enviar }: { unidadeId: string; enviar: boolean }) =>
      alterarRegraUnidade(unidadeId, enviar),
    onSuccess: () => qc.invalidateQueries({ queryKey: mensageriaKeys.regras }),
  });
}
