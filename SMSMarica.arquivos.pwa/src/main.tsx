import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { registrarPwa } from '@/lib/pwa';
import './index.css';

const container = document.getElementById('root');
if (!container) {
  throw new Error('Elemento raiz #root não encontrado.');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

// Service worker (cache offline + auto-update). Ver `@/lib/pwa`.
registrarPwa();

// Cara de app: bloqueia o menu de contexto (clique direito / long-press) fora de campos.
window.addEventListener('contextmenu', (e) => {
  const alvo = e.target as HTMLElement | null;
  if (alvo?.closest('input, textarea, [contenteditable="true"]')) return;
  e.preventDefault();
});
