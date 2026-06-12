import { Plus, Trash2 } from 'lucide-react';
import { DIAS_SEMANA, type AdicionarRecorrenciaPayload, type DiaSemana } from '@/features/agendamentos/types';

type Props = {
  faixas: AdicionarRecorrenciaPayload[];
  aoMudar: (faixas: AdicionarRecorrenciaPayload[]) => void;
};

/**
 * Editor de grade semanal no padrão de mercado: por dia da semana, uma ou mais
 * faixas de atendimento (ex.: manhã 08:00–12:00 e tarde 13:00–17:00). O backend
 * fatia cada faixa em slots pela duração configurada na agenda.
 */
export function GradeSemanalEditor({ faixas, aoMudar }: Props) {
  function faixasDoDia(dia: DiaSemana) {
    return faixas
      .map((f, indice) => ({ ...f, indice }))
      .filter((f) => f.diaSemana === dia);
  }

  function adicionar(dia: DiaSemana) {
    const doDia = faixasDoDia(dia);
    // Sugere a próxima faixa a partir da última do dia (manhã → tarde).
    const sugestao: AdicionarRecorrenciaPayload =
      doDia.length > 0
        ? { diaSemana: dia, horaInicio: '13:00', horaFim: '17:00' }
        : { diaSemana: dia, horaInicio: '08:00', horaFim: '12:00' };
    aoMudar([...faixas, sugestao]);
  }

  function remover(indice: number) {
    aoMudar(faixas.filter((_, i) => i !== indice));
  }

  function mudarHora(indice: number, campo: 'horaInicio' | 'horaFim', valor: string) {
    aoMudar(faixas.map((f, i) => (i === indice ? { ...f, [campo]: valor } : f)));
  }

  return (
    <div className="overflow-hidden rounded-md border border-gray-200">
      {DIAS_SEMANA.map(({ id, rotulo }) => {
        const doDia = faixasDoDia(id);
        return (
          <div key={id} className="flex items-start gap-3 border-b border-gray-100 px-3 py-2 last:border-b-0">
            <span className="w-20 pt-1.5 text-sm font-medium text-gray-700">{rotulo}</span>
            <div className="flex flex-1 flex-wrap items-center gap-2">
              {doDia.length === 0 ? (
                <span className="pt-1.5 text-xs text-gray-400">Sem atendimento</span>
              ) : (
                doDia.map((f) => (
                  <span key={f.indice} className="inline-flex items-center gap-1 rounded-md border border-gray-200 bg-gray-50 px-2 py-1">
                    <input
                      type="time"
                      value={f.horaInicio}
                      onChange={(e) => mudarHora(f.indice, 'horaInicio', e.target.value)}
                      className="rounded border-0 bg-transparent text-sm"
                    />
                    <span className="text-xs text-gray-400">às</span>
                    <input
                      type="time"
                      value={f.horaFim}
                      onChange={(e) => mudarHora(f.indice, 'horaFim', e.target.value)}
                      className="rounded border-0 bg-transparent text-sm"
                    />
                    <button
                      type="button"
                      onClick={() => remover(f.indice)}
                      title="Remover faixa"
                      className="ml-1 text-gray-400 hover:text-red-600"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </span>
                ))
              )}
              <button
                type="button"
                onClick={() => adicionar(id)}
                className="inline-flex items-center gap-1 rounded-md border border-dashed border-gray-300 px-2 py-1 text-xs text-gray-600 hover:border-primary-400 hover:text-primary-700"
              >
                <Plus className="h-3 w-3" />
                Faixa
              </button>
            </div>
          </div>
        );
      })}
    </div>
  );
}
