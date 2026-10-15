import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTemConsulta } from '@/shared/auth/authStore';
import {
  atenderOcorrencia,
  encerrarOcorrencia,
  listarOcorrencias,
} from '@/features/ocorrencias/api/ocorrenciasApi';

// O hub (/hubs/rastreamento) invalida na hora; o poll é só a rede de segurança se o socket cair.
const INTERVALO_FALLBACK_MS = 30_000;

export const ocorrenciasKeys = {
  todas: () => ['ocorrencias'] as const,
  lista: (encerradas: boolean) => ['ocorrencias', 'lista', encerradas] as const,
};

export function useOcorrencias(encerradas: boolean) {
  return useQuery({
    queryKey: ocorrenciasKeys.lista(encerradas),
    queryFn: () => listarOcorrencias(encerradas),
    refetchInterval: INTERVALO_FALLBACK_MS,
  });
}

/** Contador do menu: ocorrências em aberto (0 sem o módulo Rastreamento). */
export function useOcorrenciasAbertasBadge(): number {
  const podeVer = useTemConsulta('Rastreamento');
  const { data } = useQuery({
    queryKey: ocorrenciasKeys.lista(false),
    queryFn: () => listarOcorrencias(false),
    refetchInterval: INTERVALO_FALLBACK_MS,
    enabled: podeVer,
  });
  return podeVer ? data?.length ?? 0 : 0;
}

function useInvalidarOcorrencias() {
  const client = useQueryClient();
  return () => {
    client.invalidateQueries({ queryKey: ocorrenciasKeys.todas() });
    // O Mapa da frota destaca o veículo com ocorrência em aberto.
    client.invalidateQueries({ queryKey: ['rastreamento', 'frota'] });
  };
}

export function useAtenderOcorrencia() {
  const invalidar = useInvalidarOcorrencias();
  return useMutation({ mutationFn: atenderOcorrencia, onSuccess: invalidar });
}

export function useEncerrarOcorrencia() {
  const invalidar = useInvalidarOcorrencias();
  return useMutation({
    mutationFn: ({ id, observacao }: { id: string; observacao: string | null }) =>
      encerrarOcorrencia(id, observacao),
    onSuccess: invalidar,
  });
}
