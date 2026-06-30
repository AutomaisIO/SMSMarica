import { http } from '@/shared/api/httpClient';

/** Preferências de UI do usuário, persistidas no servidor (por usuário). */
export type PreferenciasUi = {
  /** Tela default de cada seção do menu: id da seção → rota. */
  menuDefaults: Record<string, string>;
};

export async function obterPreferencias(): Promise<PreferenciasUi> {
  const { data } = await http.get<PreferenciasUi>('/identidade/me/preferencias');
  return { menuDefaults: data?.menuDefaults ?? {} };
}

export async function salvarPreferencias(preferencias: PreferenciasUi): Promise<void> {
  await http.put('/identidade/me/preferencias', preferencias);
}
