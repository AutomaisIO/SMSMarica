import { useCallback, useEffect, useState } from 'react';

const INTERVALO_MS = 3 * 60_000;

/**
 * Sincronismo de versão do painel: compara o build embarcado no bundle
 * (`__VERSAO_BUILD__`) com o `/versao.json` publicado no deploy (a cada 3 min e ao
 * focar a aba). Quando diverge:
 *  - aba em segundo plano → recarrega sozinha na hora (nada em uso, zero impacto);
 *  - aba em uso → expõe `novaVersao` (banner no Layout) e recarrega quando a aba
 *    perder o foco ou na próxima navegação de rota (momento seguro).
 * O reload comum basta: o nginx serve o index.html com no-store e os bundles têm
 * hash no nome — não precisa de Ctrl+F5.
 */
export function useVersaoApp() {
  const [novaVersao, setNovaVersao] = useState(false);

  useEffect(() => {
    if (!import.meta.env.PROD) return;

    let ativo = true;
    let detectada = false;

    async function checar() {
      if (detectada) return;
      try {
        const r = await fetch('/versao.json', { cache: 'no-store' });
        if (!r.ok) return;
        const dados: { build?: string } = await r.json();
        if (!ativo || !dados.build || dados.build === __VERSAO_BUILD__) return;

        detectada = true;
        if (document.hidden) {
          window.location.reload();
          return;
        }
        setNovaVersao(true);
      } catch {
        /* offline/erro de rede — tenta no próximo tick */
      }
    }

    function aoMudarVisibilidade() {
      if (document.hidden) {
        // Operador saiu da aba com o banner pendente — atualiza sem atrapalhar.
        if (detectada) window.location.reload();
      } else {
        void checar();
      }
    }

    const timer = window.setInterval(() => void checar(), INTERVALO_MS);
    document.addEventListener('visibilitychange', aoMudarVisibilidade);
    window.addEventListener('focus', aoMudarVisibilidade);
    void checar();

    return () => {
      ativo = false;
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', aoMudarVisibilidade);
      window.removeEventListener('focus', aoMudarVisibilidade);
    };
  }, []);

  const atualizar = useCallback(() => window.location.reload(), []);

  return { novaVersao, atualizar };
}
