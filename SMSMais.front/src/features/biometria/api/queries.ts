import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  apagarBiometriaMotorista,
  gerarLiberacaoMotorista,
  listarVerificacoesFaciais,
  obterBiometriaMotorista,
  obterFotoBiometria,
  obterSelfieVerificacao,
} from '@/features/biometria/api/biometriaApi';
import type { FiltroVerificacoesFaciais } from '@/features/biometria/types';

// As conferências chegam do tablet quando ele tem rede (fila sem rede no tablet); o poll é o que
// faz a entrada pelo código da central aparecer aqui sem a central precisar recarregar a tela.
const INTERVALO_CONFERENCIAS_MS = 30_000;

export const biometriaKeys = {
  todas: () => ['biometria'] as const,
  situacao: (motoristaId: string) => ['biometria', 'situacao', motoristaId] as const,
  foto: (motoristaId: string) => ['biometria', 'foto', motoristaId] as const,
  conferencias: (filtro: FiltroVerificacoesFaciais) => ['biometria', 'conferencias', filtro] as const,
  selfie: (id: string) => ['biometria', 'selfie', id] as const,
};

/**
 * Foto de rosto é dado sensível (LGPD art. 5º, II): carrega só quando a pessoa pede e não fica
 * guardada no cache depois que o modal fecha.
 */
const SEM_CACHE = { staleTime: 0, gcTime: 0, retry: false } as const;

export function useBiometriaMotorista(motoristaId: string | null) {
  return useQuery({
    queryKey: biometriaKeys.situacao(motoristaId ?? 'nenhum'),
    queryFn: () => obterBiometriaMotorista(motoristaId as string),
    enabled: Boolean(motoristaId),
  });
}

export function useFotoBiometria(motoristaId: string, habilitado: boolean) {
  return useQuery({
    queryKey: biometriaKeys.foto(motoristaId),
    queryFn: () => obterFotoBiometria(motoristaId),
    enabled: habilitado,
    ...SEM_CACHE,
  });
}

export function useApagarBiometria(motoristaId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => apagarBiometriaMotorista(motoristaId),
    onSuccess: () => {
      client.removeQueries({ queryKey: biometriaKeys.foto(motoristaId) });
      client.invalidateQueries({ queryKey: biometriaKeys.situacao(motoristaId) });
    },
  });
}

export function useConferenciasFaciais(filtro: FiltroVerificacoesFaciais, habilitado = true) {
  return useQuery({
    queryKey: biometriaKeys.conferencias(filtro),
    queryFn: () => listarVerificacoesFaciais(filtro),
    enabled: habilitado,
    refetchInterval: habilitado ? INTERVALO_CONFERENCIAS_MS : false,
  });
}

export function useSelfieVerificacao(id: string, habilitado: boolean) {
  return useQuery({
    queryKey: biometriaKeys.selfie(id),
    queryFn: () => obterSelfieVerificacao(id),
    enabled: habilitado,
    ...SEM_CACHE,
  });
}

export function useGerarLiberacao(veiculoId: string) {
  return useMutation({
    mutationFn: (motoristaId: string) => gerarLiberacaoMotorista(veiculoId, motoristaId),
  });
}
