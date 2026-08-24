import { cn } from '@/shared/lib/cn';

type Variante = 'vermelho' | 'branco';

type Props = {
  valor: number;
  /** 'vermelho' = fundo vermelho/letra branca (sino); 'branco' = fundo branco/letra vermelha (menu). */
  variante?: Variante;
  /** Teto exibido antes de virar "N+". */
  max?: number;
  className?: string;
  titulo?: string;
};

/**
 * Badge circular de contador. Reaproveita o padrão do sino da Central de Atendimento.
 * Não renderiza nada quando o valor é 0 (ou negativo).
 */
export function CounterBadge({ valor, variante = 'vermelho', max = 99, className, titulo }: Props) {
  if (!valor || valor <= 0) return null;
  const texto = valor > max ? `${max}+` : String(valor);
  return (
    <span
      title={titulo}
      className={cn(
        'inline-flex h-4 min-w-4 items-center justify-center rounded-full px-1 text-[10px] font-bold leading-none',
        variante === 'branco'
          ? 'bg-white text-red-600 ring-1 ring-red-200'
          : 'bg-error-500 text-white',
        className,
      )}
    >
      {texto}
    </span>
  );
}
