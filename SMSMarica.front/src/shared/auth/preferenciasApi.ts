import { http } from '@/shared/api/httpClient';

/** Preferências de UI do usuário, persistidas no servidor (por usuário). */
export type PreferenciasUi = {
  /** Tela default de cada seção do menu: id da seção → rota. */
  menuDefaults: Record<string, string>;
  /** Altura (px) da caixa de digitação do chat de conversas. */
  alturaComposerChat?: number;
  /** Enter envia a mensagem no chat (Shift+Enter quebra linha). */
  enviarComEnter?: boolean;
};

export async function obterPreferencias(): Promise<PreferenciasUi> {
  const { data } = await http.get<PreferenciasUi>('/identidade/me/preferencias');
  return {
    menuDefaults: data?.menuDefaults ?? {},
    alturaComposerChat: data?.alturaComposerChat ?? undefined,
    enviarComEnter: data?.enviarComEnter ?? undefined,
  };
}

/**
 * Salva preferências. O servidor faz merge por campo: enviar só a parte que mudou
 * (ex.: `{ menuDefaults }` ou `{ enviarComEnter }`) preserva os demais campos.
 */
export async function salvarPreferencias(preferencias: Partial<PreferenciasUi>): Promise<void> {
  await http.put('/identidade/me/preferencias', preferencias);
}
