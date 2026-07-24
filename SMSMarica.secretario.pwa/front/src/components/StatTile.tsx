import { Cartao } from '@/components/Cartao';
import { NumeroAnimado } from '@/components/NumeroAnimado';

interface Props {
  rotulo: string;
  valor: number;
  formatar?: (n: number) => string;
  /** Linhas de apoio sob o número (ex.: split urgência/eletiva). */
  detalhes?: string[];
}

/** Tile de indicador: eyebrow + número tabular animado + apoio discreto. */
export function StatTile({ rotulo, valor, formatar, detalhes }: Props) {
  return (
    <Cartao className="p-4 sm:p-5">
      <p className="eyebrow">{rotulo}</p>
      <p className="mt-1.5 font-display text-[28px] font-bold leading-none tracking-tight text-tinta sm:text-[32px]">
        <NumeroAnimado valor={valor} formatar={formatar} />
      </p>
      {detalhes && detalhes.length > 0 && (
        <div className="mt-2 space-y-0.5">
          {detalhes.map((linha) => (
            <p key={linha} className="text-[12.5px] leading-snug text-grafite">
              {linha}
            </p>
          ))}
        </div>
      )}
    </Cartao>
  );
}
