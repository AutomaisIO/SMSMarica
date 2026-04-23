import { cn } from '@/shared/lib/cn';

type Props = {
  className?: string;
  alt?: string;
  compact?: boolean;
};

export function BrandLogo({ className, alt = 'Prefeitura de Maricá', compact = false }: Props) {
  if (compact) {
    return (
      <div
        className={cn(
          'flex h-10 w-10 items-center justify-center rounded-md bg-white text-primary-700 font-bold shadow-marica',
          className,
        )}
      >
        M
      </div>
    );
  }

  return (
    <img
      src="/marica_logo.png"
      alt={alt}
      className={cn('object-contain', className)}
    />
  );
}
