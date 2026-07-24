import clsx from 'clsx';
import type { AguardandoPorCor } from '@/types/painel';
import { TRIAGEM } from '@/lib/triagem';
import { formatarInteiro, minutosLegiveis } from '@/lib/formatos';

/**
 * Chip de fila por cor: ponto colorido + nome escrito + quantidade, com a espera
 * média desde a chegada como apoio. Cor nunca aparece sem o nome ao lado.
 */
export function ChipCor({ item }: { item: AguardandoPorCor }) {
  const estilo = TRIAGEM[item.cor];
  const vazio = item.qtd === 0;

  return (
    <span
      className={clsx(
        'inline-flex items-baseline gap-2 rounded-full border border-linha bg-papel px-3 py-1.5',
        'text-[13px] leading-none',
        vazio && 'opacity-55',
      )}
      title={
        item.minMedioEspera != null
          ? `${estilo.nome}: espera média de ${minutosLegiveis(item.minMedioEspera)} desde a chegada`
          : `${estilo.nome}: fila vazia`
      }
    >
      <span
        className="h-2.5 w-2.5 shrink-0 self-center rounded-full"
        style={{ backgroundColor: estilo.cor }}
      />
      <span className="font-medium text-tinta">{estilo.nome}</span>
      <span className="tnum font-display text-[15px] font-bold text-tinta">
        {formatarInteiro(item.qtd)}
      </span>
      {item.minMedioEspera != null && (
        <span className="tnum text-[12px] text-grafite">
          · {minutosLegiveis(item.minMedioEspera)}
        </span>
      )}
    </span>
  );
}
