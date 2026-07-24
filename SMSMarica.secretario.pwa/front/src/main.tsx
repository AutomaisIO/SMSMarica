import React from 'react';
import ReactDOM from 'react-dom/client';
import { registerSW } from 'virtual:pwa-register';

// Fontes self-hosted (@fontsource) — a PWA offline não depende do Google Fonts.
import '@fontsource/archivo/600.css';
import '@fontsource/archivo/700.css';
import '@fontsource/archivo/800.css';
import '@fontsource/instrument-sans/400.css';
import '@fontsource/instrument-sans/500.css';
import '@fontsource/instrument-sans/600.css';

import App from './App';
import './index.css';

// Após um deploy, um chunk antigo referenciado pelo index.html velho some do
// servidor e o import dinâmico falha. Recarregar pega o index.html novo (o nginx
// serve com no-store). O carimbo em sessionStorage evita laço de reload se o erro
// persistir por outro motivo.
window.addEventListener('vite:preloadError', (evento) => {
  const chave = 'versao:reload-preload-error';
  const ultimo = Number(sessionStorage.getItem(chave) ?? 0);
  if (Date.now() - ultimo < 60_000) return; // deixa o erro estourar — não é chunk velho
  evento.preventDefault();
  sessionStorage.setItem(chave, String(Date.now()));
  window.location.reload();
});

// O painel fica aberto por dias numa TV ou no celular: sem uma checagem periódica,
// o service worker só procuraria versão nova na próxima navegação — que nunca vem.
const INTERVALO_CHECAGEM_SW_MS = 30 * 60_000;

registerSW({
  immediate: true,
  onRegisteredSW(_url, registro) {
    if (!registro) return;
    window.setInterval(() => void registro.update(), INTERVALO_CHECAGEM_SW_MS);
  },
});

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
