import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  buscarPendenciasImportacao,
  obterPainelInicio,
  resolverPendenciaComPaciente,
} from '@/features/painel-inicio/api/painelApi';
import type { DirecaoPainel, LenteEscopo } from '@/features/painel-inicio/types';

/**
 * Polling leve, no mesmo idioma dos badges de tickets. 60s: o painel é situacional, não é chat —
 * atualizar mais rápido só gastaria requisição em link de unidade.
 */
const INTERVALO_PAINEL = 60_000;

export const painelKeys = {
  raiz: ['painel'] as const,
  inicio: (lente: LenteEscopo, direcao: DirecaoPainel) => ['painel', 'inicio', lente, direcao] as const,
  pendenciasBusca: (busca: string) => ['painel', 'pendencias-busca', busca] as const,
};

export function usePainelInicio(lente: LenteEscopo, direcao: DirecaoPainel) {
  return useQuery({
    queryKey: painelKeys.inicio(lente, direcao),
    queryFn: () => obterPainelInicio(lente, direcao),
    refetchInterval: INTERVALO_PAINEL,
  });
}

/**
 * O número do badge no item "Início" do menu. Sempre na lente/direção padrão: o badge é um aviso
 * de "tem coisa te esperando", não um reflexo do filtro que o usuário deixou aberto.
 *
 * Existe porque quem tem menu favorito é redirecionado ao entrar e NUNCA vê a home — sem o badge,
 * o painel nasce invisível justamente para os usuários mais frequentes (ADR-0033 §8).
 */
export function usePainelBadge() {
  const { data } = usePainelInicio('Unidade', 'Tudo');
  return data?.totalCritico ?? 0;
}

/** Pendências que casam com a busca da tela de Solicitações. Termo curto não consulta. */
export function usePendenciasImportacaoBusca(busca: string) {
  const termo = busca.trim();
  return useQuery({
    queryKey: painelKeys.pendenciasBusca(termo),
    queryFn: () => buscarPendenciasImportacao(termo),
    enabled: termo.length >= 3,
  });
}

export function useResolverPendenciaComPaciente() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ falhaId, cpf, pacienteId }: { falhaId: string; cpf?: string; pacienteId?: string }) =>
      resolverPendenciaComPaciente(falhaId, { cpf, pacienteId }),
    onSuccess: () => {
      // A pendência resolvida vira solicitação: some da raia, some do bloco de busca, e a
      // listagem de solicitações passa a ter uma linha nova.
      qc.invalidateQueries({ queryKey: painelKeys.raiz });
      qc.invalidateQueries({ queryKey: ['solicitacoes-exame'] });
      qc.invalidateQueries({ queryKey: ['importacao-sisreg'] });
    },
  });
}
