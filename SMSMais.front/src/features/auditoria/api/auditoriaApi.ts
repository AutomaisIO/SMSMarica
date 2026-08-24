import { http } from '@/shared/api/httpClient';
import type { AuditoriaFiltro, PaginaAuditoria } from '@/features/auditoria/types';

export async function buscarAuditoria(filtro: AuditoriaFiltro): Promise<PaginaAuditoria> {
  // Remove chaves vazias/undefined para não poluir a query string.
  const params: Record<string, string | number> = {};
  for (const [k, v] of Object.entries(filtro)) {
    if (v !== undefined && v !== null && v !== '') params[k] = v as string | number;
  }
  const { data } = await http.get<PaginaAuditoria>('/auditoria', { params });
  return data;
}
