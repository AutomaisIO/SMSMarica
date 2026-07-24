import clsx from 'clsx';
import type { StatusOracle } from '@/types/painel';
import { horaMinuto, idadeEmMinutos } from '@/lib/formatos';

export type NivelFrescor = 'vivo' | 'defasado' | 'desconectado';

export function calcularFrescor(
  geradoEm: string,
  oracle: StatusOracle,
  agora: Date,
): NivelFrescor {
  if (!oracle.ok) return 'desconectado';
  const idade = idadeEmMinutos(geradoEm, agora);
  if (idade < 3) return 'vivo';
  if (idade < 15) return 'defasado';
  return 'desconectado';
}

interface Props {
  geradoEm: string;
  oracle: StatusOracle;
  agora: Date;
  usandoMock: boolean;
}

/**
 * Indicador de frescor — nunca finge: "ao vivo" só com snapshot < 3 min e Oracle
 * ok; entre 3 e 15 min mostra a hora dos dados; acima disso (ou Oracle fora),
 * "reconectando ao Salux…". Mock é honesto: SÓ o selo "dados de exemplo",
 * nunca o pill de frescor por cima de dados de demonstração.
 */
export function PillFrescor({ geradoEm, oracle, agora, usandoMock }: Props) {
  const nivel = calcularFrescor(geradoEm, oracle, agora);

  if (usandoMock) {
    return (
      <div className="flex items-center gap-2" aria-live="polite">
        <span
          className={clsx(
            'inline-flex items-center rounded-full border border-dashed border-grafite/40',
            'px-2.5 py-1 text-[11.5px] font-medium text-grafite',
          )}
        >
          dados de exemplo
        </span>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2" aria-live="polite">
      {nivel === 'vivo' && (
        <span className="inline-flex items-center gap-2 rounded-full border border-linha bg-papel py-1 pl-2.5 pr-3 text-[12.5px] font-semibold text-tinta">
          <span className="anima-pulso h-2 w-2 rounded-full bg-vermelho-marica" />
          ao vivo
        </span>
      )}
      {nivel === 'defasado' && (
        <span className="inline-flex items-center gap-2 rounded-full border border-triagem-amarelo/40 bg-triagem-amarelo/10 py-1 pl-2.5 pr-3 text-[12.5px] font-semibold text-triagem-amarelo-apoio">
          <span className="h-2 w-2 rounded-full bg-triagem-amarelo" />
          dados de {horaMinuto(geradoEm)}
        </span>
      )}
      {nivel === 'desconectado' && (
        <span className="inline-flex items-center gap-2 rounded-full border border-linha bg-painel py-1 pl-2.5 pr-3 text-[12.5px] font-medium text-grafite">
          <span className="h-2 w-2 rounded-full bg-triagem-cinza" />
          reconectando ao Salux…
        </span>
      )}
    </div>
  );
}
