import { User } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

type Props = {
  src?: string | null;
  nome?: string | null;
  tamanho?: 'xs' | 'sm' | 'md' | 'lg' | 'xl';
  className?: string;
};

const TAMANHOS: Record<NonNullable<Props['tamanho']>, string> = {
  xs: 'h-6 w-6 text-[10px]',
  sm: 'h-8 w-8 text-xs',
  md: 'h-10 w-10 text-sm',
  lg: 'h-16 w-16 text-base',
  xl: 'h-24 w-24 text-xl',
};

function iniciais(nome?: string | null): string {
  if (!nome) return '?';
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return '?';
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();
  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase();
}

/**
 * Avatar redondo com fallback de iniciais (ou ícone genérico). `src` é
 * tipicamente um base64 retornado pelo backend, mas qualquer URL válida
 * funciona.
 */
export function Avatar({ src, nome, tamanho = 'md', className }: Props) {
  const classeBase = cn(
    'inline-flex shrink-0 items-center justify-center overflow-hidden rounded-full border border-gray-200 bg-gray-100 font-semibold uppercase text-gray-600',
    TAMANHOS[tamanho],
    className,
  );

  if (src) {
    return (
      <span className={classeBase}>
        <img src={src} alt={nome ?? ''} className="h-full w-full object-cover" />
      </span>
    );
  }

  if (nome) {
    return <span className={classeBase}>{iniciais(nome)}</span>;
  }

  return (
    <span className={classeBase}>
      <User className="h-1/2 w-1/2 text-gray-400" />
    </span>
  );
}
