import { useCallback, useEffect, useState } from 'react';
import type { UnidadeId } from '@/types/painel';

const CHAVE = 'secretario:unidade';
const PADRAO: UnidadeId = 'geral';
const VALIDOS: UnidadeId[] = ['geral', 'conde', 'upa', 'santarita'];

function ehValida(valor: string | null | undefined): valor is UnidadeId {
  return valor != null && (VALIDOS as string[]).includes(valor);
}

/**
 * Qual unidade o painel está mostrando.
 *
 * A escolha vale por dispositivo (localStorage): quem deixa o painel na TV da UPA
 * não quer reabrir no "geral" toda manhã. O `?unidade=` na URL vence o guardado e
 * NÃO grava — é o modo quiosque, para fixar uma tela sem contaminar quem usa o
 * mesmo aparelho depois.
 */
export function useUnidade(): [UnidadeId, (valor: UnidadeId) => void] {
  const [unidade, setUnidade] = useState<UnidadeId>(() => {
    const daUrl = new URLSearchParams(window.location.search).get('unidade');
    if (ehValida(daUrl)) return daUrl;
    try {
      const guardada = window.localStorage.getItem(CHAVE);
      if (ehValida(guardada)) return guardada;
    } catch {
      // Safari em modo privado pode barrar o storage — o padrão resolve.
    }
    return PADRAO;
  });

  const escolher = useCallback((valor: UnidadeId) => {
    setUnidade(valor);
    try {
      window.localStorage.setItem(CHAVE, valor);
    } catch {
      // Sem persistência a escolha ainda vale para a sessão.
    }
  }, []);

  // Voltar/avançar do navegador com ?unidade= na URL precisa refletir na tela.
  useEffect(() => {
    const aoNavegar = () => {
      const daUrl = new URLSearchParams(window.location.search).get('unidade');
      if (ehValida(daUrl)) setUnidade(daUrl);
    };
    window.addEventListener('popstate', aoNavegar);
    return () => window.removeEventListener('popstate', aoNavegar);
  }, []);

  return [unidade, escolher];
}
