import { useEffect, useState } from 'react';

const INTERVALO_MS = 3 * 60_000;
/** Tempo que o aviso fica na tela antes do recarregamento automático. */
const ESPERA_VISIVEL_MS = 6_000;

/**
 * Auto-atualização depois de um deploy — mesma mecânica do painel smsmarica:
 * compara o build embarcado no bundle (`__VERSAO_BUILD__`) com o `/versao.json`
 * publicado junto do deploy, a cada 3 min e ao focar a aba.
 *
 * Aqui o app é só leitura (não há nada digitado para perder), então a atualização
 * é automática de ponta a ponta — o Secretário nunca precisa saber o que é cache:
 *  - aba em segundo plano → recarrega na hora, sem ninguém ver;
 *  - aba em uso → mostra o aviso e recarrega sozinha em poucos segundos.
 *
 * O reload comum basta: o nginx serve index.html e sw.js com no-store e os
 * bundles têm hash no nome — nada de Ctrl+F5.
 */
export function useVersaoApp() {
  const [novaVersao, setNovaVersao] = useState(false);

  useEffect(() => {
    if (!import.meta.env.PROD) return;

    let ativo = true;
    let detectada = false;

    function recarregar() {
      window.location.reload();
    }

    async function checar() {
      if (detectada) return;
      try {
        const r = await fetch('/versao.json', { cache: 'no-store' });
        if (!r.ok) return;
        const dados: { build?: string } = await r.json();
        if (!ativo || !dados.build || dados.build === __VERSAO_BUILD__) return;

        detectada = true;
        if (document.hidden) {
          recarregar();
          return;
        }
        setNovaVersao(true);
        window.setTimeout(recarregar, ESPERA_VISIVEL_MS);
      } catch {
        /* offline/erro de rede — tenta no próximo tick */
      }
    }

    function aoMudarVisibilidade() {
      if (document.hidden) {
        // Saiu da aba com atualização pendente: aproveita para trocar sem interromper.
        if (detectada) recarregar();
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

  return { novaVersao };
}
