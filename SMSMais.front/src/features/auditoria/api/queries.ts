import { useQuery } from '@tanstack/react-query';
import { buscarAuditoria } from '@/features/auditoria/api/auditoriaApi';
import type { AuditoriaFiltro } from '@/features/auditoria/types';

export const auditoriaKeys = {
  raiz: ['auditoria'] as const,
  busca: (filtro: AuditoriaFiltro) => ['auditoria', 'busca', filtro] as const,
};

export function useBuscarAuditoria(filtro: AuditoriaFiltro) {
  return useQuery({
    queryKey: auditoriaKeys.busca(filtro),
    queryFn: () => buscarAuditoria(filtro),
    placeholderData: (anterior) => anterior,
  });
}
