import { useEffect, useRef, useState } from 'react';
import clsx from 'clsx';
import { formatarInteiro } from '@/lib/formatos';
import { prefereMenosMovimento } from '@/lib/movimento';

interface Props {
  valor: number;
  /** Formatador pt-BR (padrão: inteiro). */
  formatar?: (n: number) => string;
  className?: string;
  duracaoMs?: number;
}

/**
 * Count-up de ~600 ms, disparado uma única vez por mudança real de valor.
 * Com prefers-reduced-motion o número troca direto, sem animação.
 */
export function NumeroAnimado({ valor, formatar = formatarInteiro, className, duracaoMs = 600 }: Props) {
  const [exibido, setExibido] = useState(valor);
  const anterior = useRef(valor);

  useEffect(() => {
    const de = anterior.current;
    if (de === valor) return;
    anterior.current = valor;

    if (prefereMenosMovimento()) {
      setExibido(valor);
      return;
    }

    let raf = 0;
    const t0 = performance.now();
    const passo = (t: number) => {
      const progresso = Math.min(1, (t - t0) / duracaoMs);
      const suave = 1 - Math.pow(1 - progresso, 3); // ease-out cúbico
      setExibido(de + (valor - de) * suave);
      if (progresso < 1) raf = requestAnimationFrame(passo);
    };
    raf = requestAnimationFrame(passo);
    return () => cancelAnimationFrame(raf);
  }, [valor, duracaoMs]);

  return <span className={clsx('tnum', className)}>{formatar(exibido)}</span>;
}
