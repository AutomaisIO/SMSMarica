import clsx from 'clsx';

function Bloco({ className }: { className?: string }) {
  return <div className={clsx('shimmer rounded-2xl', className)} />;
}

/** Skeleton da seção "agora" — cartão herói + fileira de tiles. */
export function SkeletonSecaoAgora() {
  return (
    <section className="space-y-4" aria-hidden="true">
      <Bloco className="h-64 rounded-2xl" />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Bloco className="h-28" />
        <Bloco className="h-28" />
        <Bloco className="h-28" />
        <Bloco className="h-28" />
      </div>
    </section>
  );
}

/** Skeleton das pulseiras de classificação (tempo de espera). */
export function SkeletonSecaoPulseiras() {
  return (
    <section className="space-y-3" aria-hidden="true">
      <Bloco className="h-8 w-72 rounded-lg" />
      <Bloco className="h-16 rounded-full" />
      <Bloco className="h-16 rounded-full" />
      <Bloco className="h-16 rounded-full" />
      <Bloco className="h-16 rounded-full" />
      <Bloco className="h-16 rounded-full" />
    </section>
  );
}

/** Skeleton de seção com cartões de mês + gráfico (atendimentos/internações). */
export function SkeletonSecaoGraficos() {
  return (
    <section className="space-y-4" aria-hidden="true">
      <Bloco className="h-8 w-56 rounded-lg" />
      <div className="grid gap-4 lg:grid-cols-2">
        <Bloco className="h-36" />
        <Bloco className="h-36" />
      </div>
      <Bloco className="h-72" />
    </section>
  );
}

/** Skeleton da primeira carga — mesma silhueta do painel, shimmer discreto. */
export function SkeletonPainel() {
  return (
    <div className="space-y-10" aria-hidden="true">
      <SkeletonSecaoAgora />
      <SkeletonSecaoPulseiras />
      <SkeletonSecaoGraficos />
    </div>
  );
}
