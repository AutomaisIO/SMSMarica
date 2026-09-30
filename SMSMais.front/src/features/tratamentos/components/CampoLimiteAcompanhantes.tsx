import { useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Modal } from '@/shared/ui/Modal';
import type { RegraAcompanhantesPayload } from '@/features/tratamentos/types';

type Props = {
  valor: RegraAcompanhantesPayload;
  aoMudar: (v: RegraAcompanhantesPayload) => void;
  /** Quem liberou o 2º (já gravado), para mostrar. */
  liberadoPorNome?: string | null;
};

/**
 * Quantos acompanhantes podem ir em cada viagem: 1 por direito; 2 só com liberação — o modal pede a
 * justificativa, e quem salva fica registrado como quem liberou.
 */
export function CampoLimiteAcompanhantes({ valor, aoMudar, liberadoPorNome }: Props) {
  const [liberando, setLiberando] = useState(false);
  const [justificativa, setJustificativa] = useState(valor.justificativaSegundo ?? '');

  function confirmarLiberacao() {
    aoMudar({ quantidade: 2, justificativaSegundo: justificativa.trim() });
    setLiberando(false);
  }

  return (
    <div className="space-y-2">
      <fieldset className="space-y-1.5">
        <legend className="label">Acompanhantes por viagem</legend>
        <label className="flex items-center gap-2 text-sm text-gray-800">
          <input
            type="radio"
            name="limite-acompanhantes"
            checked={valor.quantidade === 1}
            onChange={() => aoMudar({ quantidade: 1, justificativaSegundo: null })}
          />
          1 acompanhante (direito de todo paciente)
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-800">
          <input
            type="radio"
            name="limite-acompanhantes"
            checked={valor.quantidade === 2}
            onChange={() => {
              setJustificativa(valor.justificativaSegundo ?? '');
              setLiberando(true);
            }}
          />
          2 acompanhantes — exige liberação com justificativa
        </label>
      </fieldset>

      {valor.quantidade === 2 && valor.justificativaSegundo ? (
        <div className="rounded-md border border-sky-200 bg-sky-50 px-3 py-2 text-sm text-sky-900">
          <p className="flex items-center gap-1.5 font-medium">
            <ShieldCheck className="h-4 w-4" /> Segundo acompanhante liberado
            {liberadoPorNome ? <span className="font-normal">por {liberadoPorNome}</span> : null}
          </p>
          <p className="mt-1">{valor.justificativaSegundo}</p>
          <button
            type="button"
            className="mt-1 text-xs text-sky-800 underline"
            onClick={() => {
              setJustificativa(valor.justificativaSegundo ?? '');
              setLiberando(true);
            }}
          >
            Alterar justificativa
          </button>
        </div>
      ) : null}

      <Modal
        aberto={liberando}
        aoFechar={() => setLiberando(false)}
        titulo="Liberar segundo acompanhante"
        descricao="Todo paciente tem direito a 1 acompanhante. O segundo precisa de uma justificativa, e você fica registrado como quem liberou."
        largura="md"
      >
        <div className="space-y-4">
          <Campo label="Justificativa" htmlFor="just-segundo" required>
            <textarea
              id="just-segundo"
              className="input min-h-[90px]"
              maxLength={500}
              placeholder="Ex.: paciente acamado — são precisos dois para o embarque e o desembarque."
              value={justificativa}
              onChange={(e) => setJustificativa(e.target.value)}
            />
          </Campo>
          <div className="flex justify-end gap-2">
            <Button variante="ghost" onClick={() => setLiberando(false)}>Cancelar</Button>
            <Button onClick={confirmarLiberacao} disabled={justificativa.trim().length === 0}>
              Liberar 2 acompanhantes
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
