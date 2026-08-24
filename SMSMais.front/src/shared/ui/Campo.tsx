import type { ReactNode } from 'react';
import { cn } from '@/shared/lib/cn';

type CampoProps = {
  label: string;
  htmlFor: string;
  erro?: string;
  dica?: ReactNode;
  /** Botão de ajuda ("?") renderizado ao lado do rótulo — ver <AjudaCampo />. */
  ajuda?: ReactNode;
  children: ReactNode;
  className?: string;
  required?: boolean;
};

export function Campo({ label, htmlFor, erro, dica, ajuda, children, className, required }: CampoProps) {
  return (
    <div className={cn('flex flex-col', className)}>
      <label htmlFor={htmlFor} className="label inline-flex items-center gap-1">
        {label}
        {required && <span className="ml-0.5 text-red-500" aria-hidden="true">*</span>}
        {ajuda}
      </label>
      {children}
      {erro ? (
        <span className="mt-1 text-xs text-error-600">{erro}</span>
      ) : dica ? (
        <span className="mt-1 text-xs text-gray-500">{dica}</span>
      ) : null}
    </div>
  );
}
