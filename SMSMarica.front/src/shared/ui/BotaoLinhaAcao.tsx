import type { ButtonHTMLAttributes } from 'react';
import { cn } from '@/shared/lib/cn';

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  tom?: 'padrao' | 'perigo';
};

export function BotaoLinhaAcao({ tom = 'padrao', className, ...props }: Props) {
  return (
    <button
      type="button"
      className={cn(
        'inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium transition-colors',
        tom === 'perigo'
          ? 'text-red-700 hover:bg-red-50'
          : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
        className,
      )}
      {...props}
    />
  );
}
