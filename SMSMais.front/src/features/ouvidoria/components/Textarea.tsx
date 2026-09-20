import { forwardRef, type TextareaHTMLAttributes } from 'react';
import { cn } from '@/shared/lib/cn';

type Props = TextareaHTMLAttributes<HTMLTextAreaElement>;

/** Textarea com o mesmo visual do `Input` do sistema (classe `input`). */
export const Textarea = forwardRef<HTMLTextAreaElement, Props>(({ className, rows = 4, ...props }, ref) => (
  <textarea ref={ref} rows={rows} className={cn('input min-h-[2.5rem] resize-y', className)} {...props} />
));

Textarea.displayName = 'Textarea';
