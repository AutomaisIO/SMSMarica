import { useEffect, useState } from 'react';

/** Devolve `valor` só depois que ele fica `ms` sem mudar — para busca "ao vivo" não disparar a
 *  cada tecla. */
export function useDebounce<T>(valor: T, ms = 300): T {
  const [d, setD] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setD(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return d;
}
