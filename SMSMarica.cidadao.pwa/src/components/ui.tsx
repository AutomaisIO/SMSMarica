import { Loader2, type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/cn';

/** Iniciais a partir do nome, para o avatar sem foto. */
function iniciais(nome?: string | null): string {
  if (!nome) return '?';
  const p = nome.trim().split(/\s+/);
  return ((p[0]?.[0] ?? '') + (p.length > 1 ? p[p.length - 1][0] : '')).toUpperCase();
}

export function Avatar({
  src,
  nome,
  size = 48,
  className,
}: {
  src?: string | null;
  nome?: string | null;
  size?: number;
  className?: string;
}) {
  return (
    <span
      className={cn(
        'inline-grid place-items-center overflow-hidden rounded-full bg-vinho/10 font-display font-semibold text-vinho ring-1 ring-black/5',
        className,
      )}
      style={{ width: size, height: size, fontSize: size * 0.38 }}
    >
      {src ? (
        <img src={src} alt={nome ?? 'Foto'} className="h-full w-full object-cover" />
      ) : (
        iniciais(nome)
      )}
    </span>
  );
}

export function Card({
  className,
  children,
  ...rest
}: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn('rounded-2xl border border-areia bg-white shadow-carta', className)}
      {...rest}
    >
      {children}
    </div>
  );
}

export function PrimaryButton({
  className,
  carregando,
  children,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement> & { carregando?: boolean }) {
  return (
    <button
      className={cn(
        'inline-flex min-h-[52px] w-full items-center justify-center gap-2 rounded-2xl bg-marica px-5',
        'text-base font-semibold text-white shadow-carta transition active:scale-[.99] active:bg-marica-escuro',
        'disabled:pointer-events-none disabled:opacity-50',
        className,
      )}
      disabled={carregando || props.disabled}
      {...props}
    >
      {carregando && <Loader2 className="h-5 w-5 animate-spin" />}
      {children}
    </button>
  );
}

export function GhostButton({
  className,
  children,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      className={cn(
        'inline-flex min-h-[48px] items-center justify-center gap-2 rounded-2xl border border-areia bg-white px-4',
        'text-sm font-semibold text-tinta transition active:scale-[.99] active:bg-papel',
        'disabled:pointer-events-none disabled:opacity-50',
        className,
      )}
      {...props}
    >
      {children}
    </button>
  );
}

export function Field({
  label,
  hint,
  ...props
}: React.InputHTMLAttributes<HTMLInputElement> & { label: string; hint?: string }) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium text-tinta">{label}</span>
      <input
        className={cn(
          'min-h-[52px] w-full rounded-2xl border border-areia bg-white px-4 text-base text-tinta',
          'placeholder:text-tinta-mute/60 focus:border-lagoa focus:outline-none',
          'disabled:bg-papel disabled:text-tinta-mute',
        )}
        {...props}
      />
      {hint && <span className="mt-1 block text-xs text-tinta-mute">{hint}</span>}
    </label>
  );
}

export function SectionHeader({ eyebrow, title }: { eyebrow?: string; title: string }) {
  return (
    <div className="mb-3">
      {eyebrow && (
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-marica">{eyebrow}</p>
      )}
      <h2 className="font-display text-xl font-semibold text-tinta">{title}</h2>
    </div>
  );
}

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('animate-pulse rounded-xl bg-areia/70', className)} />;
}

export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={cn('h-6 w-6 animate-spin text-marica', className)} />;
}

export function EmptyState({
  icon: Icon,
  titulo,
  descricao,
  acao,
}: {
  icon: LucideIcon;
  titulo: string;
  descricao: string;
  acao?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col items-center rounded-2xl border border-dashed border-areia bg-white/60 px-6 py-12 text-center">
      <span className="mb-4 grid h-16 w-16 place-items-center rounded-2xl bg-lagoa-claro text-lagoa">
        <Icon className="h-8 w-8" />
      </span>
      <p className="font-display text-lg font-semibold text-tinta">{titulo}</p>
      <p className="mt-1 max-w-xs text-sm text-tinta-mute">{descricao}</p>
      {acao && <div className="mt-5">{acao}</div>}
    </div>
  );
}

export function ErroCard({ mensagem, aoTentar }: { mensagem: string; aoTentar?: () => void }) {
  return (
    <Card className="border-marica/20 bg-marica/[0.03] p-5 text-center">
      <p className="text-sm text-tinta">{mensagem}</p>
      {aoTentar && (
        <GhostButton className="mx-auto mt-4 w-auto" onClick={aoTentar}>
          Tentar de novo
        </GhostButton>
      )}
    </Card>
  );
}
