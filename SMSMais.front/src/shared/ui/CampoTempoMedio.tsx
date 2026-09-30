import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { formatarDuracao, paraMinutos } from '@/shared/lib/tempoMedio';

type Props = {
  horas: string;
  minutos: string;
  aoMudar: (v: { horas: string; minutos: string }) => void;
  erro?: string;
  className?: string;
};

/**
 * Tempo médio que o paciente fica no tratamento (da chegada à liberação). Entra como horas +
 * minutos e vai ao backend em minutos. É o número de onde sai a previsão da volta.
 */
export function CampoTempoMedio({ horas, minutos, aoMudar, erro, className }: Props) {
  const total = paraMinutos(horas, minutos);
  return (
    <Campo
      label="Tempo médio no tratamento"
      htmlFor="tempo-medio-horas"
      required
      erro={erro}
      className={className}
      dica={
        total != null
          ? `= ${formatarDuracao(total)} da chegada à liberação do paciente. Ex.: hemodiálise ≈ 4h00.`
          : 'Da chegada à liberação do paciente. Ex.: hemodiálise ≈ 4h00.'
      }
    >
      <div className="flex items-center gap-2">
        <Input
          id="tempo-medio-horas"
          type="number"
          min={0}
          max={24}
          inputMode="numeric"
          value={horas}
          onChange={(e) => aoMudar({ horas: e.target.value, minutos })}
          className="w-20"
          aria-label="Horas"
        />
        <span className="text-sm text-gray-600">h</span>
        <Input
          id="tempo-medio-minutos"
          type="number"
          min={0}
          max={59}
          step={5}
          inputMode="numeric"
          value={minutos}
          onChange={(e) => aoMudar({ horas, minutos: e.target.value })}
          className="w-20"
          aria-label="Minutos"
        />
        <span className="text-sm text-gray-600">min</span>
      </div>
    </Campo>
  );
}
