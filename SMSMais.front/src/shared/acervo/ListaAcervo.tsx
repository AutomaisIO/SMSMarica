import { FileImage, FileText, ScanLine, Stethoscope } from 'lucide-react';

import { cn } from '@/shared/lib/cn';
import type { ItemAcervo } from './tipos';

/** Ícone por natureza do item: laudo, imagens de exame, imagem solta ou documento. */
export function IconeItemAcervo({ item, className }: { item: ItemAcervo; className?: string }) {
  const Icone =
    item.tipo === 'Laudo'
      ? Stethoscope
      : item.tipo === 'ImagensExame'
        ? ScanLine
        : item.mimeType.startsWith('image/')
          ? FileImage
          : FileText;
  const cor =
    item.tipo === 'Laudo' ? 'text-emerald-600' : item.tipo === 'ImagensExame' ? 'text-sky-600' : 'text-slate-400';
  return <Icone className={cn('size-5 shrink-0', cor, className)} />;
}

export function formatarDataAcervo(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('pt-BR');
}

export function formatarTamanhoAcervo(bytes: number | null | undefined): string {
  if (!bytes || bytes <= 0) return '';
  const kb = bytes / 1024;
  return kb < 1024 ? `${kb.toFixed(0)} KB` : `${(kb / 1024).toFixed(1)} MB`;
}
