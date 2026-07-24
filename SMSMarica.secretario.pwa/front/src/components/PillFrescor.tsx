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
 *
 * Vive dentro da faixa vermelha do header, então todas as variantes são
 * translúcidas sobre o vermelho — a cor sozinha nunca carrega o significado,
 * o texto sempre diz o estado.
 */
export function PillFrescor({ geradoEm, oracle, agora, usandoMock }: Props) {
  const nivel = calcularFrescor(geradoEm, oracle, agora);

  const base =
    'inline-flex items-center gap-2 rounded-full py-1 pl-2.5 pr-3 text-[12.5px] font-semibold';

  if (usandoMock) {
    return (
      <div className="flex items-center gap-2" aria-live="polite">
        <span
          className={`${base} border border-dashed border-white/50 font-medium text-white/85`}
        >
          dados de exemplo
        </span>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2" aria-live="polite">
      {nivel === 'vivo' && (
        <span className={`${base} bg-white text-vermelho-marica`}>
          <span className="anima-pulso h-2 w-2 rounded-full bg-vermelho-marica" />
          ao vivo
        </span>
      )}
      {nivel === 'defasado' && (
        <span className={`${base} bg-white/15 text-white ring-1 ring-inset ring-white/40`}>
          <span className="h-2 w-2 rounded-full bg-triagem-amarelo" />
          dados de {horaMinuto(geradoEm)}
        </span>
      )}
      {nivel === 'desconectado' && (
        <span className={`${base} bg-white/10 font-medium text-white/85 ring-1 ring-inset ring-white/30`}>
          <span className="h-2 w-2 rounded-full bg-white/60" />
          reconectando ao Salux…
        </span>
      )}
    </div>
  );
}
