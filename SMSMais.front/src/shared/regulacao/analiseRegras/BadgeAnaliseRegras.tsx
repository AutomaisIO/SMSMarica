import { cn } from '@/shared/lib/cn';
import { formatarInstante } from '@/shared/lib/datas';
import { ICONE_VEREDITO } from '@/shared/regulacao/analiseRegras/icones';
import {
  CLASSE_VEREDITO,
  DESCRICAO_VEREDITO,
  ROTULO_VEREDITO,
  type AnaliseRegrasResumo,
  type VereditoAnaliseRegras,
} from '@/shared/regulacao/analiseRegras/tipos';

/** O selo do veredito, sem dados — serve a fila (com análise) e os chips de filtro. */
export function SeloVeredito({
  veredito,
  titulo,
  className,
}: {
  veredito: VereditoAnaliseRegras;
  titulo?: string;
  className?: string;
}) {
  const Icone = ICONE_VEREDITO[veredito];
  return (
    <span
      title={titulo ?? DESCRICAO_VEREDITO[veredito]}
      className={cn(
        'inline-flex items-center gap-1 whitespace-nowrap rounded-full border px-2 py-0.5 text-xs font-medium',
        CLASSE_VEREDITO[veredito],
        className,
      )}
    >
      <Icone className="size-3.5" aria-hidden />
      {ROTULO_VEREDITO[veredito]}
    </span>
  );
}

/**
 * O veredito da análise de regras numa linha de fila. O tooltip traz a frase que decidiu —
 * é ela que diz "por quê" sem abrir o pedido.
 */
export function BadgeAnaliseRegras({
  analise,
  className,
}: {
  analise: AnaliseRegrasResumo | null | undefined;
  className?: string;
}) {
  if (!analise) {
    return (
      <span
        className="text-xs text-slate-400"
        title="A análise automática ainda não rodou para este pedido."
      >
        —
      </span>
    );
  }

  const titulo = [
    `${ROTULO_VEREDITO[analise.veredito]}: ${analise.resumo ?? DESCRICAO_VEREDITO[analise.veredito]}`,
    `Analisado em ${formatarInstante(analise.analisadoEm)}.`,
    'Parecer nosso — nada é escrito no sistema externo.',
  ].join('\n');

  return <SeloVeredito veredito={analise.veredito} titulo={titulo} className={className} />;
}
