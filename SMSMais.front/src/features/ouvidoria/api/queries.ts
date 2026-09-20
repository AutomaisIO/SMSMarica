import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type {
  ArquivarRequest,
  EncaminharExternoRequest,
  EncaminharRequest,
  FiltroManifestacoes,
  OuvidoriaConfiguracaoDto,
  RegistrarManifestacaoRequest,
  ResponderCidadaoRequest,
  SalvarAssuntoRequest,
  SalvarMarcadorRequest,
  SalvarPontoRespostaRequest,
  TextoComAnexosRequest,
  TextoRequest,
  TriarRequest,
} from '@/features/ouvidoria/types';
import {
  anotar,
  arquivar,
  atualizarAssunto,
  atualizarConfiguracao,
  atualizarMarcador,
  atualizarPontoResposta,
  atualizarTeorPseudonimizado,
  cobrar,
  complementar,
  concluir,
  criarAssunto,
  criarMarcador,
  criarPontoResposta,
  devolverArea,
  encaminhar,
  encaminharExterno,
  escalonar,
  habilitarDenuncia,
  listarAssuntos,
  listarManifestacoes,
  listarMarcadores,
  listarPontosResposta,
  obterConfiguracao,
  obterManifestacao,
  obterPainel,
  obterResumo,
  pedirComplementacao,
  prorrogar,
  registrarManifestacao,
  registrarRecurso,
  responderArea,
  responderCidadao,
  revelarIdentidade,
  triar,
} from '@/features/ouvidoria/api/ouvidoriaApi';

/** Contadores das abas: polling leve (sem realtime). */
const INTERVALO_RESUMO = 30_000;

export const ouvidoriaKeys = {
  raiz: ['ouvidoria'] as const,
  lista: (filtro: FiltroManifestacoes) => ['ouvidoria', 'manifestacoes', filtro] as const,
  detalhe: (id: string) => ['ouvidoria', 'manifestacao', id] as const,
  resumo: ['ouvidoria', 'resumo'] as const,
  pontos: ['ouvidoria', 'pontos-resposta'] as const,
  assuntos: ['ouvidoria', 'assuntos'] as const,
  marcadores: ['ouvidoria', 'marcadores'] as const,
  configuracao: ['ouvidoria', 'configuracao'] as const,
  painel: (de: string, ate: string, unidadeId?: string) => ['ouvidoria', 'painel', de, ate, unidadeId ?? ''] as const,
};

// ---- Consultas ----

export function useManifestacoes(filtro: FiltroManifestacoes, habilitado = true) {
  return useQuery({
    queryKey: ouvidoriaKeys.lista(filtro),
    queryFn: () => listarManifestacoes(filtro),
    enabled: habilitado,
    placeholderData: (anterior) => anterior,
  });
}

export function useManifestacao(id: string | null | undefined) {
  return useQuery({
    queryKey: ouvidoriaKeys.detalhe(id ?? ''),
    queryFn: () => obterManifestacao(id as string),
    enabled: !!id,
  });
}

export function useResumoOuvidoria(habilitado = true) {
  return useQuery({
    queryKey: ouvidoriaKeys.resumo,
    queryFn: obterResumo,
    enabled: habilitado,
    refetchInterval: INTERVALO_RESUMO,
  });
}

export function usePontosResposta(habilitado = true) {
  return useQuery({ queryKey: ouvidoriaKeys.pontos, queryFn: listarPontosResposta, enabled: habilitado });
}

export function useAssuntos() {
  return useQuery({ queryKey: ouvidoriaKeys.assuntos, queryFn: listarAssuntos, staleTime: 5 * 60_000 });
}

export function useMarcadores() {
  return useQuery({ queryKey: ouvidoriaKeys.marcadores, queryFn: listarMarcadores, staleTime: 5 * 60_000 });
}

export function useConfiguracaoOuvidoria() {
  return useQuery({ queryKey: ouvidoriaKeys.configuracao, queryFn: obterConfiguracao });
}

export function usePainelOuvidoria(de: string, ate: string, unidadeId?: string) {
  return useQuery({
    queryKey: ouvidoriaKeys.painel(de, ate, unidadeId),
    queryFn: () => obterPainel(de, ate, unidadeId),
    enabled: !!de && !!ate,
  });
}

// ---- Mutations ----

function useInvalidarRaiz() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: ouvidoriaKeys.raiz });
}

export function useRegistrarManifestacao() {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: RegistrarManifestacaoRequest) => registrarManifestacao(p), onSuccess: invalidar });
}

export function useTriar(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TriarRequest) => triar(id, p), onSuccess: invalidar });
}

export function useEncaminhar(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: EncaminharRequest) => encaminhar(id, p), onSuccess: invalidar });
}

export function usePedirComplementacao(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoRequest) => pedirComplementacao(id, p), onSuccess: invalidar });
}

export function useComplementar(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoComAnexosRequest) => complementar(id, p), onSuccess: invalidar });
}

export function useResponderArea(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoComAnexosRequest) => responderArea(id, p), onSuccess: invalidar });
}

export function useDevolverArea(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoRequest) => devolverArea(id, p), onSuccess: invalidar });
}

export function useResponderCidadao(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: ResponderCidadaoRequest) => responderCidadao(id, p), onSuccess: invalidar });
}

export function useProrrogar(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoRequest) => prorrogar(id, p), onSuccess: invalidar });
}

export function useCobrar(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoRequest | null) => cobrar(id, p), onSuccess: invalidar });
}

export function useEscalonar(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoRequest) => escalonar(id, p), onSuccess: invalidar });
}

export function useRegistrarRecurso(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoRequest) => registrarRecurso(id, p), onSuccess: invalidar });
}

export function useConcluir(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: () => concluir(id), onSuccess: invalidar });
}

export function useArquivar(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: ArquivarRequest) => arquivar(id, p), onSuccess: invalidar });
}

export function useEncaminharExterno(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: EncaminharExternoRequest) => encaminharExterno(id, p), onSuccess: invalidar });
}

export function useHabilitarDenuncia(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoRequest) => habilitarDenuncia(id, p), onSuccess: invalidar });
}

export function useAtualizarTeorPseudonimizado(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoRequest) => atualizarTeorPseudonimizado(id, p), onSuccess: invalidar });
}

export function useAnotar(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: TextoRequest) => anotar(id, p), onSuccess: invalidar });
}

/** Revelar identidade NÃO invalida o detalhe: o resultado é exibido só na sessão, sem cache. */
export function useRevelarIdentidade(id: string) {
  const invalidar = useInvalidarRaiz();
  return useMutation({
    mutationFn: (justificativa: string) => revelarIdentidade(id, justificativa),
    // A linha do tempo ganha um evento "Acesso à identidade" — recarrega o detalhe.
    onSuccess: invalidar,
  });
}

export function useSalvarPontoResposta() {
  const invalidar = useInvalidarRaiz();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string | null; payload: SalvarPontoRespostaRequest }) =>
      id ? atualizarPontoResposta(id, payload).then(() => id) : criarPontoResposta(payload),
    onSuccess: invalidar,
  });
}

export function useSalvarAssunto() {
  const invalidar = useInvalidarRaiz();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string | null; payload: SalvarAssuntoRequest }) =>
      id ? atualizarAssunto(id, payload).then(() => id) : criarAssunto(payload),
    onSuccess: invalidar,
  });
}

export function useSalvarMarcador() {
  const invalidar = useInvalidarRaiz();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string | null; payload: SalvarMarcadorRequest }) =>
      id ? atualizarMarcador(id, payload).then(() => id) : criarMarcador(payload),
    onSuccess: invalidar,
  });
}

export function useAtualizarConfiguracaoOuvidoria() {
  const invalidar = useInvalidarRaiz();
  return useMutation({ mutationFn: (p: OuvidoriaConfiguracaoDto) => atualizarConfiguracao(p), onSuccess: invalidar });
}
