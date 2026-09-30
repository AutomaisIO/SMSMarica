import { DIAS_SEMANA } from '@/shared/lib/diasSemana';

type Props = {
  /** Bitmask: bit 0 = domingo … bit 6 = sábado. */
  valor: number;
  aoMudar: (mascara: number) => void;
  desabilitado?: boolean;
  id?: string;
};

/** Botões Dom…Sáb que ligam e desligam cada dia da semana (valor em bitmask). */
export function SeletorDiasSemana({ valor, aoMudar, desabilitado, id }: Props) {
  return (
    <div id={id} className="flex flex-wrap gap-2" role="group" aria-label="Dias da semana">
      {DIAS_SEMANA.map((d) => {
        const marcado = (valor & d.bit) !== 0;
        return (
          <button
            key={d.bit}
            type="button"
            disabled={desabilitado}
            aria-pressed={marcado}
            title={d.label}
            onClick={() => aoMudar(valor ^ d.bit)}
            className={`min-w-[3rem] rounded-md border px-3 py-1.5 text-sm transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${
              marcado
                ? 'border-red-600 bg-red-50 font-medium text-red-700'
                : 'border-gray-300 text-gray-600 hover:bg-gray-50'
            }`}
          >
            {d.curto}
          </button>
        );
      })}
    </div>
  );
}
