import clsx from 'clsx';

export interface OpcaoSegmento<T extends string> {
  valor: T;
  rotulo: string;
}

interface Props<T extends string> {
  opcoes: OpcaoSegmento<T>[];
  valor: T;
  aoMudar: (valor: T) => void;
  ariaLabel: string;
}

/** Segmented control — destaque ativo em vermelho-marica (uso de identidade). */
export function SegmentedControl<T extends string>({ opcoes, valor, aoMudar, ariaLabel }: Props<T>) {
  return (
    <div
      role="group"
      aria-label={ariaLabel}
      className="inline-flex items-center gap-0.5 rounded-full border border-linha bg-papel p-1 shadow-cartao"
    >
      {opcoes.map((opcao) => {
        const ativo = opcao.valor === valor;
        return (
          <button
            key={opcao.valor}
            type="button"
            aria-pressed={ativo}
            onClick={() => aoMudar(opcao.valor)}
            className={clsx(
              'rounded-full px-3.5 py-1.5 text-[13px] font-semibold transition-colors duration-150',
              ativo
                ? 'bg-vermelho-marica text-white'
                : 'text-grafite hover:bg-painel hover:text-tinta',
            )}
          >
            {opcao.rotulo}
          </button>
        );
      })}
    </div>
  );
}
