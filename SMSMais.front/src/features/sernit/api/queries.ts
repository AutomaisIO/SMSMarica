import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  buscarSolicitacoesSernit,
  dispararVarreduraSernit,
  listarNotificacoesSernit,
  marcarNotificacaoSernitVista,
  marcarNotificacoesDaSolicitacaoSernitVistas,
  anexarRascunhoSernit,
  excluirRascunhoSernit,
  listarRascunhosSernit,
  marcarRascunhoProntoSernit,
  obterCamposCatalogoSernit,
  obterFormularioCatalogoSernit,
  obterRascunhoSernit,
  removerAnexoRascunhoSernit,
  salvarRascunhoSernit,
  sincronizarCatalogoSernit,
  obterCamposNovaSernit,
  sugerirCidsSernit,
  obterFormularioNovaSernit,
  listarRecursosNovaSernit,
  obterResumoNotificacoesSernit,
  obterVarreduraAutomaticaSernit,
  salvarVarreduraAutomaticaSernit,
  listarExecucoesSernit,
  obterResumoSernit,
  obterSolicitacaoSernit,
  obterStatusMotorSernit,
  salvarCredencialSernit,
  testarCredencialSernit,
  obterSessaoOperadorSernit,
  entrarNoSernit,
  sairDoSernit,
  registrarFollowUpSernit,
  obterContatosSernit,
  alterarContatosSernit,
} from '@/features/sernit/api/sernitApi';
import type {
  AlterarContatosSernit,
  BuscaSernitFiltro,
  DispararVarreduraSernitPayload,
  NotificacoesSernitFiltro,
  VarreduraAutomaticaSernit,
  RascunhoSernitRequest,
  StatusRascunhoSernit,
  TipoRecursoSernit,
} from '@/features/sernit/types';

export const sernitKeys = {
  busca: (filtro: BuscaSernitFiltro) => ['sernit', 'busca', filtro] as const,
  resumo: ['sernit', 'resumo'] as const,
  solicitacao: (id: string) => ['sernit', 'solicitacao', id] as const,
  status: ['sernit', 'status'] as const,
  execucoes: ['sernit', 'execucoes'] as const,
  sessaoOperador: ['sernit', 'sessao-operador'] as const,
  contatos: (id: string) => ['sernit', 'contatos', id] as const,
};

export function useBuscaSernit(filtro: BuscaSernitFiltro) {
  return useQuery({
    queryKey: sernitKeys.busca(filtro),
    queryFn: () => buscarSolicitacoesSernit(filtro),
    placeholderData: (anterior) => anterior,
  });
}

export function useResumoSernit() {
  return useQuery({ queryKey: sernitKeys.resumo, queryFn: obterResumoSernit });
}

export function useSolicitacaoSernit(id: string | undefined) {
  return useQuery({
    queryKey: sernitKeys.solicitacao(id ?? ''),
    queryFn: () => obterSolicitacaoSernit(id!),
    enabled: Boolean(id),
  });
}

/**
 * Status do motor: 1s enquanto há varredura viva, 15s em repouso — o mesmo ritmo das telas
 * irmãs (Importação SISREG e Sincronização PEP), que acompanham job longo do mesmo jeito.
 */
export function useStatusMotorSernit() {
  return useQuery({
    queryKey: sernitKeys.status,
    queryFn: obterStatusMotorSernit,
    refetchInterval: (query) => (query.state.data?.varreduraEmAndamento ? 1000 : 15000),
  });
}

/**
 * A lista de rodadas é o que mostra fase, cursor e pendentes da execução corrente — ela também
 * precisa andar durante a varredura, senão o "Progresso" congela enquanto o cabeçalho atualiza.
 */
export function useExecucoesSernit(limite = 20, emAndamento = false) {
  return useQuery({
    queryKey: [...sernitKeys.execucoes, limite],
    queryFn: () => listarExecucoesSernit(limite),
    refetchInterval: emAndamento ? 1000 : false,
  });
}

export function useDispararVarreduraSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: DispararVarreduraSernitPayload) => dispararVarreduraSernit(payload),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: sernitKeys.status });
      void qc.invalidateQueries({ queryKey: sernitKeys.execucoes });
    },
  });
}

export function useTestarCredencialSernit() {
  return useMutation({
    mutationFn: ({ usuario, senha }: { usuario: string; senha: string }) =>
      testarCredencialSernit(usuario, senha),
  });
}

export function useSalvarCredencialSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ usuario, senha }: { usuario: string; senha: string }) =>
      salvarCredencialSernit(usuario, senha),
    onSuccess: () => void qc.invalidateQueries({ queryKey: sernitKeys.status }),
  });
}

// ---------------------------------------------------------------- notificações

export const notificacaoSernitKeys = {
  resumo: ['sernit', 'notificacoes', 'resumo'] as const,
  lista: (f: NotificacoesSernitFiltro) => ['sernit', 'notificacoes', 'lista', f] as const,
};

/**
 * Resumo com polling curto: notificação que chega tarde não serve de notificação. 10s é o
 * suficiente — a varredura que as produz roda de hora em hora, no melhor caso.
 */
export function useResumoNotificacoesSernit() {
  return useQuery({
    queryKey: notificacaoSernitKeys.resumo,
    queryFn: obterResumoNotificacoesSernit,
    refetchInterval: 10_000,
  });
}

export function useNotificacoesSernit(filtro: NotificacoesSernitFiltro) {
  return useQuery({
    queryKey: notificacaoSernitKeys.lista(filtro),
    queryFn: () => listarNotificacoesSernit(filtro),
    refetchInterval: 10_000,
  });
}

/**
 * Marcar como visto invalida lista E resumo: o contador da aba tem de cair junto com a linha,
 * senão o operador vê "3 novos" numa lista vazia.
 */
export function useMarcarNotificacaoSernitVista() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => marcarNotificacaoSernitVista(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['sernit', 'notificacoes'] });
      void qc.invalidateQueries({ queryKey: sernitKeys.status });
    },
  });
}

export function useMarcarSolicitacaoSernitVista() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (idSernit: string) => marcarNotificacoesDaSolicitacaoSernitVistas(idSernit),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['sernit', 'notificacoes'] });
      void qc.invalidateQueries({ queryKey: sernitKeys.status });
    },
  });
}

export function useVarreduraAutomaticaSernit() {
  return useQuery({
    queryKey: ['sernit', 'varredura-automatica'],
    queryFn: obterVarreduraAutomaticaSernit,
  });
}

export function useSalvarVarreduraAutomaticaSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (c: VarreduraAutomaticaSernit) => salvarVarreduraAutomaticaSernit(c),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['sernit', 'varredura-automatica'] }),
  });
}

// ---------------------------------------------------------------- nova solicitação
// Cada uma dessas consultas conversa com o SERNIT AO VIVO (abre a aba, troca combo). Por isso
// `staleTime` alto: o catálogo muda quando Niterói mexe numa especialidade, não a cada minuto.

export function useFormularioNovaSernit(habilitado: boolean) {
  return useQuery({
    queryKey: ['sernit', 'nova', 'formulario'],
    queryFn: obterFormularioNovaSernit,
    enabled: habilitado,
    staleTime: 10 * 60_000,
  });
}

export function useRecursosNovaSernit(tipo: string | undefined) {
  return useQuery({
    queryKey: ['sernit', 'nova', 'recursos', tipo],
    queryFn: () => listarRecursosNovaSernit(tipo!),
    enabled: Boolean(tipo),
    staleTime: 10 * 60_000,
  });
}

export function useCamposNovaSernit(tipo: string | undefined, recurso: string | undefined) {
  return useQuery({
    queryKey: ['sernit', 'nova', 'campos', tipo, recurso],
    queryFn: () => obterCamposNovaSernit(tipo!, recurso!),
    enabled: Boolean(tipo && recurso),
    staleTime: 10 * 60_000,
  });
}

// ---------------------------------------------------------------- catálogo local + rascunhos
// Sem polling e sem staleTime curto: isto é a NOSSA base, não muda sozinha.

export const rascunhoSernitKeys = {
  formulario: ['sernit', 'rascunhos', 'formulario'] as const,
  campos: (tipo?: string, recurso?: string) => ['sernit', 'rascunhos', 'campos', tipo, recurso] as const,
  cids: (tipo?: string, recurso?: string, termo?: string) =>
    ['sernit', 'rascunhos', 'cids', tipo, recurso, termo] as const,
  lista: (status?: string) => ['sernit', 'rascunhos', 'lista', status] as const,
  item: (id: string) => ['sernit', 'rascunhos', id] as const,
};

/** Enquanto a cópia roda, acompanha de 3 em 3s — é assim que o progresso aparece sem o
 *  operador precisar recarregar a página. Parada, não pergunta mais nada. */
export function useFormularioCatalogoSernit() {
  return useQuery({
    queryKey: rascunhoSernitKeys.formulario,
    queryFn: obterFormularioCatalogoSernit,
    refetchInterval: (q) => (q.state.data?.copiaEmAndamento ? 3000 : false),
  });
}

export function useCamposCatalogoSernit(tipo?: TipoRecursoSernit, recurso?: string) {
  return useQuery({
    queryKey: rascunhoSernitKeys.campos(tipo, recurso),
    queryFn: () => obterCamposCatalogoSernit(tipo!, recurso!),
    enabled: Boolean(tipo && recurso),
  });
}

/**
 * Sugestões de CID para a Hipótese — do espelho, com o SERNIT como reserva.
 *
 * <p>Quando a lista daquele recurso já foi copiada, a resposta sai da nossa base e é instantânea.
 * Enquanto não foi, o servidor cai no autocomplete ao vivo: lá cada busca abre uma conversa Seam
 * inteira (aba → tipo → recurso → sugestão) numa sessão única e serializada, a mesma da
 * varredura. Por isso o termo chega com atraso da tela, o mínimo é 2 caracteres, e a resposta
 * fica guardada.</p>
 */
export function useSugestoesCidSernit(tipo?: TipoRecursoSernit, recurso?: string, termo?: string) {
  const busca = (termo ?? '').trim();
  return useQuery({
    queryKey: rascunhoSernitKeys.cids(tipo, recurso, busca),
    queryFn: () => sugerirCidsSernit(tipo!, recurso!, busca),
    // O recurso entra no `enabled` porque é ele que define a lista: sem recurso o SERNIT responde
    // "Nenhum CID encontrado" para qualquer termo, inclusive o código exato. O TERMO não entra:
    // vazio é pedido válido e lista tudo, como no SERNIT.
    enabled: Boolean(tipo && recurso),
    staleTime: 6 * 60 * 60 * 1000,
    retry: false,
  });
}

export function useRascunhosSernit(status?: StatusRascunhoSernit) {
  return useQuery({
    queryKey: rascunhoSernitKeys.lista(status),
    queryFn: () => listarRascunhosSernit(status),
  });
}

export function useRascunhoSernit(id: string | null) {
  return useQuery({
    queryKey: rascunhoSernitKeys.item(id ?? ''),
    queryFn: () => obterRascunhoSernit(id!),
    enabled: Boolean(id),
  });
}

/** Salvar invalida a lista também: o cartão da lista mostra paciente, recurso e nº de anexos. */
function invalidarRascunhos(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: ['sernit', 'rascunhos'] });
}

export function useSalvarRascunhoSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, corpo }: { id: string | null; corpo: RascunhoSernitRequest }) =>
      salvarRascunhoSernit(id, corpo),
    onSuccess: () => invalidarRascunhos(qc),
  });
}

export function useExcluirRascunhoSernit() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: excluirRascunhoSernit, onSuccess: () => invalidarRascunhos(qc) });
}

export function useMarcarRascunhoProntoSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: marcarRascunhoProntoSernit,
    onSuccess: () => invalidarRascunhos(qc),
  });
}

export function useAnexarRascunhoSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, arquivo }: { id: string; arquivo: File }) => anexarRascunhoSernit(id, arquivo),
    onSuccess: () => invalidarRascunhos(qc),
  });
}

export function useRemoverAnexoRascunhoSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, anexoId }: { id: string; anexoId: string }) =>
      removerAnexoRascunhoSernit(id, anexoId),
    onSuccess: () => invalidarRascunhos(qc),
  });
}

export function useSincronizarCatalogoSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (refazerTudo: boolean) => sincronizarCatalogoSernit(refazerTudo),
    onSuccess: () => invalidarRascunhos(qc),
  });
}

// ---------------------------------------------------------------- escrita no SERNIT

/** A tela consulta isto ANTES de oferecer as ações de escrita — é o que decide abrir o modal. */
export function useSessaoOperadorSernit() {
  return useQuery({
    queryKey: sernitKeys.sessaoOperador,
    queryFn: obterSessaoOperadorSernit,
    staleTime: 60_000,
  });
}

export function useEntrarNoSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ usuario, senha }: { usuario: string; senha: string }) =>
      entrarNoSernit(usuario, senha),
    onSuccess: () => void qc.invalidateQueries({ queryKey: sernitKeys.sessaoOperador }),
  });
}

export function useSairDoSernit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: sairDoSernit,
    onSuccess: () => void qc.invalidateQueries({ queryKey: sernitKeys.sessaoOperador }),
  });
}

/**
 * Registra o FollowUP e recarrega a solicitação: o backend já gravou o evento novo no espelho,
 * então a trilha da tela passa a mostrar a ação do próprio operador sem esperar a varredura.
 */
export function useRegistrarFollowUpSernit(solicitacaoId: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (texto: string) => registrarFollowUpSernit(solicitacaoId!, texto),
    onSuccess: () => {
      if (solicitacaoId) {
        void qc.invalidateQueries({ queryKey: sernitKeys.solicitacao(solicitacaoId) });
      }
      void qc.invalidateQueries({ queryKey: ['sernit', 'notificacoes'] });
    },
  });
}

/**
 * Telefones lidos ao vivo do SERNIT. Fica desabilitado até o operador abrir o painel: a leitura
 * custa duas requisições na tela de Niterói, e não vale pagar isso ao abrir cada solicitação.
 */
export function useContatosSernit(id: string | undefined, habilitado: boolean) {
  return useQuery({
    queryKey: sernitKeys.contatos(id ?? ''),
    queryFn: () => obterContatosSernit(id!),
    enabled: Boolean(id) && habilitado,
    staleTime: 0,
  });
}

export function useAlterarContatosSernit(id: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (corpo: AlterarContatosSernit) => alterarContatosSernit(id!, corpo),
    onSuccess: () => {
      if (id) {
        void qc.invalidateQueries({ queryKey: sernitKeys.contatos(id) });
        // O espelho também mudou (só os telefones), então o detalhe da solicitação sai do cache.
        void qc.invalidateQueries({ queryKey: sernitKeys.solicitacao(id) });
      }
    },
  });
}
