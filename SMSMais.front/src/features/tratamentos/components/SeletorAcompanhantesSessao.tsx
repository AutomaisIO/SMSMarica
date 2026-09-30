import { useAcompanhantes } from '@/features/acompanhantes/api/queries';
import { ROTULO_PARENTESCO } from '@/features/acompanhantes/types';

type Props = {
  pacienteId: string;
  limite: number;
  selecionados: string[];
  aoMudar: (ids: string[]) => void;
};

/** Quem vai acompanhar o paciente nesta viagem — da lista dele, até o limite do atendimento. */
export function SeletorAcompanhantesSessao({ pacienteId, limite, selecionados, aoMudar }: Props) {
  const lista = useAcompanhantes(pacienteId);
  const acompanhantes = lista.data ?? [];
  const cheio = selecionados.length >= limite;

  function alternar(id: string) {
    aoMudar(selecionados.includes(id) ? selecionados.filter((x) => x !== id) : [...selecionados, id]);
  }

  return (
    <fieldset className="space-y-1.5">
      <legend className="label">
        Acompanhantes nesta viagem <span className="font-normal text-gray-500">(até {limite})</span>
      </legend>
      {lista.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : acompanhantes.length === 0 ? (
        <p className="text-sm text-gray-500">
          O paciente não tem acompanhante cadastrado. Cadastre no atendimento (seção Acompanhantes).
        </p>
      ) : (
        acompanhantes.map((a) => {
          const marcado = selecionados.includes(a.id);
          return (
            <label key={a.id} className="flex items-center gap-2 text-sm text-gray-800">
              <input
                type="checkbox"
                checked={marcado}
                disabled={!marcado && cheio}
                onChange={() => alternar(a.id)}
              />
              {a.nome}
              {a.parentesco ? <span className="text-xs text-gray-500">· {ROTULO_PARENTESCO[a.parentesco]}</span> : null}
            </label>
          );
        })
      )}
      {cheio && acompanhantes.length > selecionados.length ? (
        <p className="text-xs text-gray-500">
          Limite do atendimento atingido.{limite === 1 ? ' Para 2, é preciso liberar no atendimento.' : ''}
        </p>
      ) : null}
    </fieldset>
  );
}
