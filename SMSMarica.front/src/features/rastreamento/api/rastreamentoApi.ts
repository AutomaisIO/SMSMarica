import { http } from '@/shared/api/httpClient';
import type { Geofence } from '@/features/rastreamento/types';

export async function listarGeofences(): Promise<Geofence[]> {
  const { data } = await http.get<Geofence[]>('/rastreamento/geofences');
  return data;
}
