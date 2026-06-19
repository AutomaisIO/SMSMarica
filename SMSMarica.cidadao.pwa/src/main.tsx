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
