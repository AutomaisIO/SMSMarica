import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { MOBILIDADES, ROTULO_MOBILIDADE, type Necessidades } from '@/features/tratamentos/types';

type Props = {
  valor: Necessidades;
  aoMudar: (v: Necessidades) => void;
};

/**
 * Condição do paciente para a viagem. Por enquanto é registro para quem monta a rota — o gerador
 * automático ainda não escolhe veículo por ela.
 */
export function CamposNecessidades({ valor, aoMudar }: Props) {
  const marcar = (campo: keyof Necessidades, v: boolean) => aoMudar({ ...valor, [campo]: v });

  return (
    <div className="space-y-4">
      <fieldset className="space-y-1.5">
        <legend className="label">Como o paciente viaja</legend>
        {MOBILIDADES.map((m) => (
          <label key={m} className="flex items-center gap-2 text-sm text-gray-800">
            <input
              type="radio"
              name="mobilidade"
              checked={valor.mobilidade === m}
              onChange={() => aoMudar({ ...valor, mobilidade: m })}
            />
            {ROTULO_MOBILIDADE[m]}
          </label>
        ))}
      </fieldset>

      <fieldset className="space-y-1.5">
        <legend className="label">Cuidados na viagem</legend>
        <label className="flex items-center gap-2 text-sm text-gray-800">
          <input
            type="checkbox"
            checked={valor.dificuldadeVeiculoAlto}
            onChange={(e) => marcar('dificuldadeVeiculoAlto', e.target.checked)}
          />
          Tem dificuldade para subir em veículo alto (van, micro-ônibus)
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-800">
          <input type="checkbox" checked={valor.isolamento} onChange={(e) => marcar('isolamento', e.target.checked)} />
          Imunodeficiente — só pode viajar com o próprio acompanhante (veículo exclusivo)
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-800">
          <input type="checkbox" checked={valor.usaOxigenio} onChange={(e) => marcar('usaOxigenio', e.target.checked)} />
          Usa oxigênio
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-800">
          <input
            type="checkbox"
            checked={valor.necessitaAjuda}
            onChange={(e) =>
              aoMudar({ ...valor, necessitaAjuda: e.target.checked, ajudaDescricao: e.target.checked ? valor.ajudaDescricao : null })
            }
          />
          Precisa de ajuda (embarque, desembarque, escada…)
        </label>
      </fieldset>

      {valor.necessitaAjuda ? (
        <Campo label="Que ajuda" htmlFor="ajuda-descricao" required dica="Para o motorista saber antes de chegar.">
          <Input
            id="ajuda-descricao"
            maxLength={500}
            placeholder="Ex.: precisa de apoio para descer a escada de casa"
            value={valor.ajudaDescricao ?? ''}
            onChange={(e) => aoMudar({ ...valor, ajudaDescricao: e.target.value })}
          />
        </Campo>
      ) : null}
    </div>
  );
}
