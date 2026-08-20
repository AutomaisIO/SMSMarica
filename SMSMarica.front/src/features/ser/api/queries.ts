import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  buscarSolicitacoesSer,
  dispararVarreduraSer,
  listarNotificacoesSer,
  marcarNotificacaoVista,
  marcarNotificacoesDaSolicitacaoVistas,
  anexarRascunhoSer,
  excluirRascunhoSer,
  listarRascunhosSer,
  marcarRascunhoProntoSer,
  obterCamposCatalogoSer,
  obterFormularioCatalogoSer,
  obterRascunhoSer,
  removerAnexoRascunhoSer,
  salvarRascunhoSer,
  sincronizarCatalogoSer,
  obterCamposNovaSer,
  sugerirCidsSer,
  obterFormularioNovaSer,
  listarRecursosNovaSer,
  obterResumoNotificacoesSer,
  obterVarreduraAutomaticaSer,
  salvarVarreduraAutomaticaSer,
  listarExecucoesSer,
  obterResumoSer,
  obterSolicitacaoSer,
  obterStatusMotorSer,
  salvarCredencialSer,
  testarCredencialSer,
  obterSessaoOperadorSer,
  entrarNoSer,
  sairDoSer,
  registrarFollowUpSer,
  obterContatosSer,
  alterarContatosSer,
} from '@/features/ser/api/serApi';
import type {
  AlterarContatosSer,
  BuscaSerFiltro,
  DispararVarreduraPayload,
  NotificacoesFiltro,
  VarreduraAutomaticaSer,
  RascunhoSerRequest,
  StatusRascunhoSer,
  TipoRecursoSer,
} from '@/features/ser/types';

export const serKeys = {
  busca: (filtro: BuscaSerFiltro) => ['ser', 'busca', filtro] as const,
  resumo: ['ser', 'resumo'] as const,
  solicitacao: (id: string) => ['ser', 'solicitacao', id] as const,
  status: ['ser', 'status'] as const,
  execucoes: ['ser', 'execucoes'] as const,
  sessaoOperador: ['ser', 'sessao-operador'] as const,
  contatos: (id: string) => ['ser', 'contatos', id] as const,
};

export function useBuscaSer(filtro: BuscaSerFiltro) {
  return useQuery({
    queryKey: serKeys.busca(filtro),
    queryFn: () => buscarSolicitacoesSer(filtro),
    placeholderData: (anterior) => anterior,
  });
}

export function useResumoSer() {
  return useQuery({ queryKey: serKeys.resumo, queryFn: obterResumoSer });
}

export function useSolicitacaoSer(id: string | undefined) {
  return useQuery({
    queryKey: serKeys.solicitacao(id ?? ''),
    queryFn: () => obterSolicitacaoSer(id!),
    enabled: Boolean(id),
  });
}

/**
 * Status do motor: 1s enquanto há varredura viva, 15s em repouso — o mesmo ritmo das telas
 * irmãs (Importação SISREG e Sincronização PEP), que acompanham job longo do mesmo jeito.
 *
 * Antes eram 3s/30s "para não martelar o servidor", mas o problema real nunca foi o intervalo:
 * era o backend só gravar os contadores quando uma SITUAÇÃO INTEIRA terminava, então a tela
 * ficava minutos exibindo o mesmo número e parecia travada. Corrigido isso (contadores por
 * lote), 1s dá a sensação de vivo que o operador espera.
 */
export function useStatusMotorSer() {
  return useQuery({
    queryKey: serKeys.status,
    queryFn: obterStatusMotorSer,
    refetchInterval: (query) => (query.state.data?.varreduraEmAndamento ? 1000 : 15000),
  });
}

/**
 * A lista de rodadas é o que mostra fase, cursor e pendentes da execução corrente — ela também
 * precisa andar durante a varredura, senão o "Progresso" congela enquanto o cabeçalho atualiza.
 */
export function useExecucoesSer(limite = 20, emAndamento = false) {
  return useQuery({
    queryKey: [...serKeys.execucoes, limite],
    queryFn: () => listarExecucoesSer(limite),
    refetchInterval: emAndamento ? 1000 : false,
  });
}

export function useDispararVarreduraSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: DispararVarreduraPayload) => dispararVarreduraSer(payload),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: serKeys.status });
      void qc.invalidateQueries({ queryKey: serKeys.execucoes });
    },
  });
}

export function useTestarCredencialSer() {
  return useMutation({
    mutationFn: ({ usuario, senha }: { usuario: string; senha: string }) =>
      testarCredencialSer(usuario, senha),
  });
}

export function useSalvarCredencialSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ usuario, senha }: { usuario: string; senha: string }) =>
      salvarCredencialSer(usuario, senha),
    onSuccess: () => void qc.invalidateQueries({ queryKey: serKeys.status }),
  });
}

// ---------------------------------------------------------------- notificações

export const notificacaoKeys = {
  resumo: ['ser', 'notificacoes', 'resumo'] as const,
  lista: (f: NotificacoesFiltro) => ['ser', 'notificacoes', 'lista', f] as const,
};

/**
 * Resumo com polling curto: notificação que chega tarde não serve de notificação. 10s é o
 * suficiente — a varredura que as produz roda de hora em hora, no melhor caso.
 */
export function useResumoNotificacoesSer() {
  return useQuery({
    queryKey: notificacaoKeys.resumo,
    queryFn: obterResumoNotificacoesSer,
    refetchInterval: 10_000,
  });
}

export function useNotificacoesSer(filtro: NotificacoesFiltro) {
  return useQuery({
    queryKey: notificacaoKeys.lista(filtro),
    queryFn: () => listarNotificacoesSer(filtro),
    refetchInterval: 10_000,
  });
}

/**
 * Marcar como visto invalida lista E resumo: o contador da aba tem de cair junto com a linha,
 * senão o operador vê "3 novos" numa lista vazia.
 */
export function useMarcarNotificacaoVista() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => marcarNotificacaoVista(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['ser', 'notificacoes'] });
      void qc.invalidateQueries({ queryKey: serKeys.status });
    },
  });
}

export function useMarcarSolicitacaoVista() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (idSer: string) => marcarNotificacoesDaSolicitacaoVistas(idSer),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['ser', 'notificacoes'] });
      void qc.invalidateQueries({ queryKey: serKeys.status });
    },
  });
}

export function useVarreduraAutomaticaSer() {
  return useQuery({
    queryKey: ['ser', 'varredura-automatica'],
    queryFn: obterVarreduraAutomaticaSer,
  });
}

export function useSalvarVarreduraAutomaticaSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (c: VarreduraAutomaticaSer) => salvarVarreduraAutomaticaSer(c),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['ser', 'varredura-automatica'] }),
  });
}

// ---------------------------------------------------------------- nova solicitação
// Cada uma dessas consultas conversa com o SER AO VIVO (abre a aba, troca combo). Por isso
// `staleTime` alto: o catálogo muda quando a SES-RJ mexe numa especialidade, não a cada minuto.

export function useFormularioNovaSer(habilitado: boolean) {
  return useQuery({
    queryKey: ['ser', 'nova', 'formulario'],
    queryFn: obterFormularioNovaSer,
    enabled: habilitado,
    staleTime: 10 * 60_000,
  });
}

export function useRecursosNovaSer(tipo: string | undefined) {
  return useQuery({
    queryKey: ['ser', 'nova', 'recursos', tipo],
    queryFn: () => listarRecursosNovaSer(tipo!),
    enabled: Boolean(tipo),
    staleTime: 10 * 60_000,
  });
}

export function useCamposNovaSer(tipo: string | undefined, recurso: string | undefined) {
  return useQuery({
    queryKey: ['ser', 'nova', 'campos', tipo, recurso],
    queryFn: () => obterCamposNovaSer(tipo!, recurso!),
    enabled: Boolean(tipo && recurso),
    staleTime: 10 * 60_000,
  });
}

// ---------------------------------------------------------------- catálogo local + rascunhos
// Sem polling e sem staleTime curto: isto é a NOSSA base, não muda sozinha.

export const rascunhoKeys = {
  formulario: ['ser', 'rascunhos', 'formulario'] as const,
  campos: (tipo?: string, recurso?: string, ramo?: boolean) =>
    ['ser', 'rascunhos', 'campos', tipo, recurso, ramo] as const,
  cids: (tipo?: string, recurso?: string, ramo?: boolean, termo?: string) =>
    ['ser', 'rascunhos', 'cids', tipo, recurso, ramo, termo] as const,
  lista: (status?: string) => ['ser', 'rascunhos', 'lista', status] as const,
  item: (id: string) => ['ser', 'rascunhos', id] as const,
};

/** Enquanto a cópia roda, acompanha de 3 em 3s — é assim que o progresso aparece sem o
 *  operador precisar recarregar a página. Parada, não pergunta mais nada. */
export function useFormularioCatalogoSer() {
  return useQuery({
    queryKey: rascunhoKeys.formulario,
    queryFn: obterFormularioCatalogoSer,
    refetchInterval: (q) => (q.state.data?.copiaEmAndamento ? 3000 : false),
  });
}

export function useCamposCatalogoSer(
  tipo?: TipoRecursoSer,
  recurso?: string,
  ambulatorioEstadual?: boolean,
) {
  return useQuery({
    queryKey: rascunhoKeys.campos(tipo, recurso, ambulatorioEstadual),
    queryFn: () => obterCamposCatalogoSer(tipo!, recurso!, ambulatorioEstadual!),
    // O ramo entra no `enabled`: sem ele o formulário buscado seria o do outro ramo.
    enabled: Boolean(tipo && recurso && ambulatorioEstadual !== undefined),
  });
}

/**
 * Sugestões de CID para a Hipótese — do espelho, com o SER como reserva.
 *
 * <p>Quando a lista daquele recurso já foi copiada, a resposta sai da nossa base e é instantânea.
 * Enquanto não foi, o servidor cai no autocomplete ao vivo: lá cada busca abre uma conversa Seam
 * inteira (aba → ramo → tipo → recurso → sugestão) numa sessão única e serializada, a mesma da
 * varredura. Por isso o termo chega com atraso da tela, o mínimo é 2 caracteres, e a resposta
 * fica guardada.</p>
 */
export function useSugestoesCidSer(
  tipo?: TipoRecursoSer,
  recurso?: string,
  ambulatorioEstadual?: boolean,
  termo?: string,
) {
  const busca = (termo ?? '').trim();
  return useQuery({
    queryKey: rascunhoKeys.cids(tipo, recurso, ambulatorioEstadual, busca),
    queryFn: () => sugerirCidsSer(tipo!, recurso!, ambulatorioEstadual!, busca),
    // O recurso entra no `enabled` porque é ele que define a lista: sem recurso o SER responde
    // "Nenhum CID encontrado" para qualquer termo, inclusive o código exato.
    enabled: Boolean(tipo && recurso && ambulatorioEstadual !== undefined && busca.length >= 2),
    staleTime: 6 * 60 * 60 * 1000,
    retry: false,
  });
}

export function useRascunhosSer(status?: StatusRascunhoSer) {
  return useQuery({ queryKey: rascunhoKeys.lista(status), queryFn: () => listarRascunhosSer(status) });
}

export function useRascunhoSer(id: string | null) {
  return useQuery({
    queryKey: rascunhoKeys.item(id ?? ''),
    queryFn: () => obterRascunhoSer(id!),
    enabled: Boolean(id),
  });
}

/** Salvar invalida a lista também: o cartão da lista mostra paciente, recurso e nº de anexos. */
function invalidarRascunhos(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: ['ser', 'rascunhos'] });
}

export function useSalvarRascunhoSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, corpo }: { id: string | null; corpo: RascunhoSerRequest }) =>
      salvarRascunhoSer(id, corpo),
    onSuccess: () => invalidarRascunhos(qc),
  });
}

export function useExcluirRascunhoSer() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: excluirRascunhoSer, onSuccess: () => invalidarRascunhos(qc) });
}

export function useMarcarRascunhoPronto() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: marcarRascunhoProntoSer, onSuccess: () => invalidarRascunhos(qc) });
}

export function useAnexarRascunhoSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, arquivo }: { id: string; arquivo: File }) => anexarRascunhoSer(id, arquivo),
    onSuccess: () => invalidarRascunhos(qc),
  });
}

export function useRemoverAnexoRascunhoSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, anexoId }: { id: string; anexoId: string }) =>
      removerAnexoRascunhoSer(id, anexoId),
    onSuccess: () => invalidarRascunhos(qc),
  });
}

export function useSincronizarCatalogoSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (refazerTudo: boolean) => sincronizarCatalogoSer(refazerTudo),
    onSuccess: () => invalidarRascunhos(qc),
  });
}

// ---------------------------------------------------------------- escrita no SER

/** A tela consulta isto ANTES de oferecer as ações de escrita — é o que decide abrir o modal. */
export function useSessaoOperadorSer() {
  return useQuery({
    queryKey: serKeys.sessaoOperador,
    queryFn: obterSessaoOperadorSer,
    staleTime: 60_000,
  });
}

export function useEntrarNoSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ usuario, senha }: { usuario: string; senha: string }) =>
      entrarNoSer(usuario, senha),
    onSuccess: () => void qc.invalidateQueries({ queryKey: serKeys.sessaoOperador }),
  });
}

export function useSairDoSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: sairDoSer,
    onSuccess: () => void qc.invalidateQueries({ queryKey: serKeys.sessaoOperador }),
  });
}

/**
 * Registra o FollowUP e recarrega a solicitação: o backend já gravou o evento novo no espelho,
 * então a trilha da tela passa a mostrar a ação do próprio operador sem esperar a varredura.
 */
export function useRegistrarFollowUpSer(solicitacaoId: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (texto: string) => registrarFollowUpSer(solicitacaoId!, texto),
    onSuccess: () => {
      if (solicitacaoId) {
        void qc.invalidateQueries({ queryKey: serKeys.solicitacao(solicitacaoId) });
      }
      void qc.invalidateQueries({ queryKey: ['ser', 'notificacoes'] });
    },
  });
}

/**
 * Telefones lidos ao vivo do SER. Fica desabilitado até o operador abrir o painel: a leitura
 * custa duas requisições na tela do Estado, e não vale pagar isso ao abrir cada solicitação.
 */
export function useContatosSer(id: string | undefined, habilitado: boolean) {
  return useQuery({
    queryKey: serKeys.contatos(id ?? ''),
    queryFn: () => obterContatosSer(id!),
    enabled: Boolean(id) && habilitado,
    staleTime: 0,
  });
}

export function useAlterarContatosSer(id: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (corpo: AlterarContatosSer) => alterarContatosSer(id!, corpo),
    onSuccess: () => {
      if (id) {
        void qc.invalidateQueries({ queryKey: serKeys.contatos(id) });
        // O espelho também mudou (só os telefones), então o detalhe da solicitação sai do cache.
        void qc.invalidateQueries({ queryKey: serKeys.solicitacao(id) });
      }
    },
  });
}
