import { forwardRef, type ButtonHTMLAttributes } from 'react';
import { cn } from '@/shared/lib/cn';

type Variante = 'primaria' | 'secundaria' | 'outline' | 'ghost' | 'danger';
type Tamanho = 'sm' | 'md' | 'lg';

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variante?: Variante;
  tamanho?: Tamanho;
};

const classesPorVariante: Record<Variante, string> = {
  primaria: 'btn-primary',
  secundaria: 'btn-secondary',
  outline: 'btn-outline',
  ghost: 'btn-ghost',
  danger: 'btn-danger',
};

const classesPorTamanho: Record<Tamanho, string> = {
  sm: 'btn-sm',
  md: '',
  lg: 'btn-lg',
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variante = 'primaria', tamanho = 'md', type = 'button', ...props }, ref) => (
    <button
      ref={ref}
      type={type}
      className={cn('btn', classesPorVariante[variante], classesPorTamanho[tamanho], className)}
      {...props}
    />
  ),
);

Button.displayName = 'Button';
