import { useRef, type ClipboardEvent, type KeyboardEvent } from 'react';
import { cn } from '@/lib/cn';

/**
 * Entrada de código OTP em caixinhas segmentadas (uma por dígito), estilo app.
 * Preenche da esquerda p/ a direita com auto-avanço; Backspace volta; aceita colar.
 * O valor é a string de dígitos já digitados (sem buracos).
 */
export function CodigoInput({
  valor,
  aoMudar,
  tamanho = 6,
  autoFocus,
  'aria-label': ariaLabel = 'Código de acesso',
}: {
  valor: string;
  aoMudar: (v: string) => void;
  tamanho?: number;
  autoFocus?: boolean;
  'aria-label'?: string;
}) {
  const refs = useRef<Array<HTMLInputElement | null>>([]);
  const foca = (i: number) => refs.current[Math.max(0, Math.min(i, tamanho - 1))]?.focus();

  function digitar(i: number, bruto: string) {
    const ds = bruto.replace(/\D/g, '');
    if (!ds) return;
    const arr = valor.split('');
    let pos = Math.min(i, arr.length); // nunca deixa buraco entre dígitos
    for (const d of ds) {
      if (pos >= tamanho) break;
      arr[pos] = d;
      pos++;
    }
    aoMudar(arr.join('').slice(0, tamanho));
    foca(pos);
  }

  function tecla(i: number, e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Backspace') {
      e.preventDefault();
      const arr = valor.split('');
      if (arr[i]) {
        arr[i] = '';
        aoMudar(arr.join(''));
      } else if (i > 0) {
        arr[i - 1] = '';
        aoMudar(arr.join(''));
        foca(i - 1);
      }
    } else if (e.key === 'ArrowLeft') {
      e.preventDefault();
      foca(i - 1);
    } else if (e.key === 'ArrowRight') {
      e.preventDefault();
      foca(i + 1);
    }
  }

  function colar(e: ClipboardEvent<HTMLInputElement>) {
    e.preventDefault();
    digitar(0, e.clipboardData.getData('text'));
  }

  return (
    <div className="flex justify-center gap-2.5" role="group" aria-label={ariaLabel}>
      {Array.from({ length: tamanho }).map((_, i) => {
        const preenchido = Boolean(valor[i]);
        return (
          <input
            key={i}
            ref={(el) => {
              refs.current[i] = el;
            }}
            inputMode="numeric"
            autoComplete={i === 0 ? 'one-time-code' : 'off'}
            autoFocus={autoFocus && i === 0}
            maxLength={1}
            value={valor[i] ?? ''}
            onChange={(e) => digitar(i, e.target.value)}
            onKeyDown={(e) => tecla(i, e)}
            onPaste={colar}
            onFocus={(e) => e.target.select()}
            aria-label={`Dígito ${i + 1}`}
            className={cn(
              'h-14 w-12 rounded-2xl border bg-white text-center font-display text-2xl font-semibold text-tinta',
              'transition focus:border-lagoa focus:outline-none focus:ring-4 focus:ring-lagoa/15',
              preenchido ? 'border-marica/40 shadow-carta' : 'border-areia',
            )}
          />
        );
      })}
    </div>
  );
}
