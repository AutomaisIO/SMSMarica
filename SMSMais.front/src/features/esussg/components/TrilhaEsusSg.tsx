import { formatarInstante, formatarInstanteData } from '@/shared/lib/datas';
import type { TipoEventoExterno } from '@/shared/regulacao/eventosExternos';
import type { EventoEsusSg } from '@/features/esussg/types';

/** Cor do marcador pelo verbo tipado: entrada azul, agendamento verde, pendência laranja. */
const COR_EVENTO: Partial<Record<TipoEventoExterno, string>> = {
  Solicitar: 'bg-blue-500',
  Agendar: 'bg-green-500',
  Reagendar: 'bg-violet-500',
  Pendenciar: 'bg-orange-500',
  RetornarParaFila: 'bg-amber-500',
  WhatsApp: 'bg-teal-500',
};

const MARCA_PERCEBIDO = 'percebido pela varredura';

/**
 * Os marcos das listas ("Inclusão na fila", "Agendamento") vêm só com a DATA — gravados à
 * meia-noite de Brasília. Mostrar "00:00" sugeriria uma hora que o ESUS nunca disse.
 */
function quando(iso: string): string {
  const hora = formatarInstante(iso, { timeStyle: 'short' });
  return hora === '00:00' ? formatarInstanteData(iso) : formatarInstante(iso);
}

function ehPercebido(e: EventoEsusSg): boolean {
  return (e.observacao ?? '').toLowerCase().includes(MARCA_PERCEBIDO);
}

/**
 * A trilha do pedido no ESUS. Não é o histórico do ESUS (a conta do município não o enxerga): é
 * MONTADA pela varredura — marcos das listas e diferenças entre duas varreduras.
 */
export function TrilhaEsusSg({ eventos, compacta = false }: { eventos: EventoEsusSg[]; compacta?: boolean }) {
  if (eventos.length === 0) {
    return <p className="text-sm text-slate-500">Nenhum marco registrado ainda.</p>;
  }

  if (compacta) {
    return (
      <ol className="space-y-2">
        {eventos.map((e) => (
          <li key={e.id} className="border-l-2 border-slate-200 pl-3">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium text-slate-800">{e.evento}</span>
              <span className="text-xs text-slate-500">{quando(e.dataEvento)}</span>
              {e.estadoAnterior && e.estadoAtual && (
                <span className="text-xs text-slate-500">
                  {e.estadoAnterior} → {e.estadoAtual}
                </span>
              )}
            </div>
            {e.observacao && <p className="text-xs text-slate-600">{e.observacao}</p>}
            <p className="text-[11px] text-slate-400">
              {[e.usuario, e.lotacaoEvento, e.unidadeExecutora].filter(Boolean).join(' · ')}
            </p>
          </li>
        ))}
      </ol>
    );
  }

  return (
    <ol className="relative space-y-3 border-l border-slate-200 pl-2">
      {eventos.map((e) => (
        <li key={e.id} className="relative pl-6">
          <span
            className={`absolute left-0 top-1.5 size-3 rounded-full ring-4 ring-white ${
              COR_EVENTO[e.tipoEvento] ?? 'bg-slate-400'
            }`}
          />
          <div className="rounded-lg border border-slate-200 bg-white p-3">
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
              <span className="text-sm font-semibold">{e.evento}</span>
              <span className="text-xs text-slate-500">{quando(e.dataEvento)}</span>
              {ehPercebido(e) && (
                <span
                  className="rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide text-slate-500"
                  title="A data é a da varredura que notou a mudança — não a hora exata em que ela aconteceu no ESUS."
                >
                  percebido pela varredura
                </span>
              )}
              {e.estadoAnterior && e.estadoAtual && (
                <span className="text-xs text-slate-500">
                  {e.estadoAnterior} → {e.estadoAtual}
                </span>
              )}
            </div>

            <div className="mt-1 flex flex-wrap gap-x-4 text-xs text-slate-500">
              {e.usuario && <span>por {e.usuario}</span>}
              {e.lotacaoEvento && <span>{e.lotacaoEvento}</span>}
              {e.unidadeExecutora && <span>{e.unidadeExecutora}</span>}
            </div>

            {e.observacao && !(ehPercebido(e) && e.observacao.trim().length < 30) && (
              <p className="mt-2 whitespace-pre-wrap rounded bg-slate-50 p-2 text-sm text-slate-700">
                {e.observacao}
              </p>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}
