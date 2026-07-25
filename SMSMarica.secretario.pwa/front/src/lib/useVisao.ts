import { useCallback, useEffect, useState } from 'react';
import type { VisaoPainel } from '@/types/painel';

const CHAVE = 'secretario:visao';
const PADRAO: VisaoPainel = 'emergencia';
const VALIDAS: VisaoPainel[] = ['emergencia', 'leitos', 'maternidade'];

function ehValida(valor: string | null | undefined): valor is VisaoPainel {
  return valor != null && (VALIDAS as string[]).includes(valor);
}

/**
 * Emergência ou Leitos. Mesma regra da unidade: guarda por dispositivo, e o
 * `?visao=` na URL vence sem gravar — quiosque não contamina quem usa depois.
 */
export function useVisao(): [VisaoPainel, (valor: VisaoPainel) => void] {
  const [visao, setVisao] = useState<VisaoPainel>(() => {
    const daUrl = new URLSearchParams(window.location.search).get('visao');
    if (ehValida(daUrl)) return daUrl;
    try {
      const guardada = window.localStorage.getItem(CHAVE);
      if (ehValida(guardada)) return guardada;
    } catch {
      // Safari em modo privado pode barrar o storage — o padrão resolve.
    }
    return PADRAO;
  });

  const escolher = useCallback((valor: VisaoPainel) => {
    setVisao(valor);
    try {
      window.localStorage.setItem(CHAVE, valor);
    } catch {
      // Sem persistência a escolha ainda vale para a sessão.
    }
  }, []);

  useEffect(() => {
    const aoNavegar = () => {
      const daUrl = new URLSearchParams(window.location.search).get('visao');
      if (ehValida(daUrl)) setVisao(daUrl);
    };
    window.addEventListener('popstate', aoNavegar);
    return () => window.removeEventListener('popstate', aoNavegar);
  }, []);

  return [visao, escolher];
}
