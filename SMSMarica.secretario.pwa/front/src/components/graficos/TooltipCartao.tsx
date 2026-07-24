export interface LinhaTooltip {
  /** Ponto colorido de identidade (opcional — só quando há ≥ 2 séries). */
  cor?: string;
  rotulo: string;
  valor: string;
}

/** Tooltip custom padrão: fundo papel, borda linha, sombra suave, texto em tinta/grafite. */
export function TooltipCartao({ titulo, linhas }: { titulo: string; linhas: LinhaTooltip[] }) {
  return (
    <div className="rounded-xl border border-linha bg-papel px-3.5 py-2.5 shadow-flutuante">
      <p className="text-[12.5px] font-semibold text-tinta">{titulo}</p>
      <div className="mt-1 space-y-0.5">
        {linhas.map((linha) => (
          <p key={linha.rotulo} className="flex items-center gap-1.5 text-[13px] text-grafite">
            {linha.cor && (
              <span
                className="h-2 w-2 shrink-0 rounded-full"
                style={{ backgroundColor: linha.cor }}
              />
            )}
            <span>{linha.rotulo}</span>
            <span className="tnum font-semibold text-tinta">{linha.valor}</span>
          </p>
        ))}
      </div>
    </div>
  );
}
