import { registerSW } from 'virtual:pwa-register';

/**
 * Registro do service worker (PWA).
 *
 * Estratégia de cache (ver `vite.config.ts`):
 * - `registerType: 'autoUpdate'` → quando um novo build é publicado, o SW novo
 *   assume na hora (skipWaiting + clientsClaim) e a página recarrega sozinha com a
 *   versão atualizada. Sem prompt "nova versão disponível".
 * - Precache (Workbox): JS/CSS/HTML/ícones/fontes, todos versionados por hash.
 * - Sem runtime caching: as chamadas à API (`/anexos/...`) são sempre rede.
 */
const INTERVALO_CHECAGEM_MS = 60 * 60 * 1000; // 1h

export function registrarPwa(): void {
  registerSW({
    immediate: true,
    onRegisteredSW(_url, registro) {
      if (!registro) return;
      setInterval(() => {
        if (registro.installing || !navigator.onLine) return;
        void registro.update();
      }, INTERVALO_CHECAGEM_MS);
    },
  });
}
