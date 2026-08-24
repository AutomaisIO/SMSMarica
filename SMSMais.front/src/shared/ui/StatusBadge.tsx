import { cn } from '@/shared/lib/cn';

type Props = { ativo: boolean };

export function StatusBadge({ ativo }: Props) {
  return (
    <span
      className={cn(
        'badge',
        ativo ? 'badge-success' : 'badge-gray',
      )}
    >
      {ativo ? 'Ativo' : 'Inativo'}
    </span>
  );
}
