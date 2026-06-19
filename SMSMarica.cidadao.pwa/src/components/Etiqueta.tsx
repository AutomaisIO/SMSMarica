import { cn } from '@/lib/cn';

/** Etiqueta de status com tom semântico inferido do texto. */
export function Etiqueta({ status }: { status: string }) {
  const s = status.toLowerCase();
  const tom =
    /(pronto|conclu|assinad|dispon|realizad)/.test(s)
      ? 'bg-lagoa-claro text-lagoa-escuro'
      : /(pendente|aguard|process|agendad)/.test(s)
        ? 'bg-amber-50 text-amber-700'
        : 'bg-areia/60 text-tinta-mute';
  return (
    <span className={cn('shrink-0 rounded-full px-3 py-1 text-xs font-semibold', tom)}>{status}</span>
  );
}
