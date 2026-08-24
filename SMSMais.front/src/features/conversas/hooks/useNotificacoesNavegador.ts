import { useCallback, useState } from 'react';

export type PermissaoNotificacao = 'default' | 'granted' | 'denied' | 'indisponivel';

/**
 * Notificações do navegador (Web Notifications API) enquanto a aba está viva. Cobre o caso
 * "fechei a janelinha do chat sem querer" — NÃO cobre a aba/navegador fechado (isso exigiria
 * Service Worker + Web Push, fora do MVP). `solicitar` deve ser chamado por gesto do usuário.
 */
export function useNotificacoesNavegador() {
  const [permissao, setPermissao] = useState<PermissaoNotificacao>(() =>
    typeof Notification === 'undefined' ? 'indisponivel' : Notification.permission);

  const solicitar = useCallback(async (): Promise<PermissaoNotificacao> => {
    if (typeof Notification === 'undefined') return 'indisponivel';
    const r = await Notification.requestPermission();
    setPermissao(r);
    return r;
  }, []);

  const notificar = useCallback(
    (titulo: string, corpo: string, tag?: string, onClick?: () => void) => {
      if (typeof Notification === 'undefined' || Notification.permission !== 'granted') return;
      try {
        const n = new Notification(titulo, { body: corpo, tag, icon: '/favicon.png' });
        if (onClick) {
          n.onclick = () => {
            window.focus();
            onClick();
            n.close();
          };
        }
      } catch {
        /* alguns navegadores exigem Service Worker para Notification — silencioso no MVP. */
      }
    },
    [],
  );

  return { permissao, solicitar, notificar };
}
