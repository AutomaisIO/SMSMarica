import { useEffect, useState } from 'react';

/**
 * Retorna o valor "atrasado": só muda depois de `ms` sem novas mudanças. Base das barras de
 * busca ao vivo — evita disparar uma requisição a cada tecla. O cancelamento da requisição
 * anterior fica por conta do React Query (queryFn recebe `signal`); aqui só controla a cadência.
 *
 * Default 500ms (padrão das telas de busca do painel — solicitações, laudos, exames de imagem).
 */
export function useDebounce<T>(valor: T, ms = 500): T {
  const [debounced, setDebounced] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return debounced;
}
