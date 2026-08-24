import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { carregarInstituicao } from '@/shared/tema/instituicao';
import './index.css';

// Após um deploy, chunk antigo (hash trocado) some do servidor e o import dinâmico
// falha. Recarregar pega o index.html novo (nginx serve com no-store). O carimbo em
// sessionStorage evita loop de reload se o erro persistir por outro motivo.
window.addEventListener('vite:preloadError', (evento) => {
  const chave = 'versao:reload-preload-error';
  const ultimo = Number(sessionStorage.getItem(chave) ?? 0);
  if (Date.now() - ultimo < 60_000) return; // deixa o erro estourar — não é chunk velho
  evento.preventDefault();
  sessionStorage.setItem(chave, String(Date.now()));
  window.location.reload();
});

const container = document.getElementById('root');
if (!container) {
  throw new Error('Elemento raiz #root não encontrado.');
}

// Identidade da instituição ANTES do primeiro render (ADR-0043): cores, título e logo saem
// daqui, e renderizar antes faria o painel piscar com a marca de outro município.
// `carregarInstituicao` nunca rejeita — sem backend, sobe com a identidade neutra.
carregarInstituicao().finally(() => {
  createRoot(container).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
});
