import { registerSW } from 'virtual:pwa-register';

/**
 * Registro do service worker (PWA).
 *
 * Estratégia de cache (ver `vite.config.ts`):
 * - `registerType: 'autoUpdate'` → quando um novo build é publicado, o SW novo
 *   assume na hora (skipWaiting + clientsClaim) e a página recarrega sozinha com a
 *   versão atualizada. Sem prompt "nova versão disponível".
 * - Precache (Workbox): JS/CSS/HTML/ícones/fontes, todos versionados por hash.
 *   Entradas antigas são limpas ao ativar o SW novo.
 * - Sem runtime caching: chamadas à API (`/auth/paciente/...`) são sempre rede,
 *   nunca servidas do cache — dados clínicos do cidadão não ficam defasados.
 *
 * O `autoUpdate` só checa atualização na carga/navegação. Como a PWA instalada pode
 * ficar aberta por horas, forçamos uma checagem periódica para detectar novos deploys.
 */
const INTERVALO_CHECAGEM_MS = 60 * 60 * 1000; // 1h

export function registrarPwa(): void {
  registerSW({
    immediate: true,
    onRegisteredSW(_url, registro) {
      if (!registro) return;
      setInterval(() => {
        // Evita checar offline ou durante uma instalação já em curso.
        if (registro.installing || !navigator.onLine) return;
        void registro.update();
      }, INTERVALO_CHECAGEM_MS);
    },
  });
}
