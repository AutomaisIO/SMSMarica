import type { Mobilidade, Necessidades } from '@/features/tratamentos/types';

const CHIP_MOBILIDADE: Partial<Record<Mobilidade, string>> = {
  CadeiranteTransfereParaBanco: 'Cadeirante (passa para o banco)',
  CadeiranteVeiculoAdaptado: 'Cadeirante — veículo adaptado',
  Maca: 'Maca',
};

type Props = {
  necessidades: Necessidades | null | undefined;
  /** Acompanhantes previstos na viagem e o limite do atendimento (opcional). */
  acompanhantes?: { previstos: number; limite: number };
  /** Quando não há nada a destacar, mostra "Sem necessidade especial"? */
  mostrarVazio?: boolean;
};

/** Condição do paciente em etiquetas curtas — para quem monta a rota bater o olho. */
export function ChipsNecessidades({ necessidades: n, acompanhantes, mostrarVazio = false }: Props) {
  const chips: { texto: string; titulo?: string; tom: 'alerta' | 'info' }[] = [];
  if (n) {
    const mob = CHIP_MOBILIDADE[n.mobilidade];
    if (mob) chips.push({ texto: mob, tom: 'alerta' });
    if (n.isolamento) chips.push({ texto: 'Veículo exclusivo', titulo: 'Imunodeficiente: só com o próprio acompanhante', tom: 'alerta' });
    if (n.dificuldadeVeiculoAlto) chips.push({ texto: 'Evitar veículo alto', tom: 'alerta' });
    if (n.usaOxigenio) chips.push({ texto: 'Oxigênio', tom: 'alerta' });
    if (n.necessitaAjuda) chips.push({ texto: 'Precisa de ajuda', titulo: n.ajudaDescricao ?? undefined, tom: 'alerta' });
  }
  if (acompanhantes && (acompanhantes.previstos > 0 || acompanhantes.limite > 1)) {
    const texto =
      acompanhantes.previstos > 0
        ? `${acompanhantes.previstos} acompanhante${acompanhantes.previstos > 1 ? 's' : ''}`
        : `até ${acompanhantes.limite} acompanhantes`;
    chips.push({ texto, tom: 'info' });
  }

  if (chips.length === 0) {
    return mostrarVazio ? <span className="text-xs text-gray-500">Sem necessidade especial</span> : null;
  }

  return (
    <span className="inline-flex flex-wrap gap-1">
      {chips.map((c) => (
        <span
          key={c.texto}
          title={c.titulo}
          className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${
            c.tom === 'alerta' ? 'bg-amber-50 text-amber-800 ring-1 ring-amber-200' : 'bg-sky-50 text-sky-800 ring-1 ring-sky-200'
          }`}
        >
          {c.texto}
        </span>
      ))}
    </span>
  );
}
