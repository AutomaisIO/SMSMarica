import { useEffect, useState } from 'react';

/** Relógio compartilhado — 1 tick/segundo para o header e o cálculo de frescor. */
export function useRelogio(intervaloMs = 1000): Date {
  const [agora, setAgora] = useState(() => new Date());

  useEffect(() => {
    const id = window.setInterval(() => setAgora(new Date()), intervaloMs);
    return () => window.clearInterval(id);
  }, [intervaloMs]);

  return agora;
}
