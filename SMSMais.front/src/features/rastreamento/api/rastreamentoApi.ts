import { http } from '@/shared/api/httpClient';
import type { Geofence, FrotaVeiculo } from '@/features/rastreamento/types';

export async function listarGeofences(): Promise<Geofence[]> {
  const { data } = await http.get<Geofence[]>('/rastreamento/geofences');
  return data;
}

/** Snapshot da frota do dia (`data` opcional no formato yyyy-MM-dd; default = hoje). */
export async function listarFrota(data?: string): Promise<FrotaVeiculo[]> {
  const { data: resp } = await http.get<FrotaVeiculo[]>('/rastreamento/frota', {
    params: data ? { data } : undefined,
  });
  return resp;
}
