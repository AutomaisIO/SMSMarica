import { useMemo } from 'react';
import { AlertTriangle, Clock } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import type { SessaoElegivel } from '@/features/translados/types';

type Props = {
  sessoes: SessaoElegivel[];
  dataRota: string;
  sessaoSelecionada?: SessaoElegivel | null;
  aoSelecionar?: (s: SessaoElegivel | null) => void;
  carregando?: boolean;
};

function formatarData(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

function formatarHora(iso: string | null): string | null {
  if (!iso) return null;
  const m = iso.match(/^(\d{2}):(\d{2})/);
  return m ? `${m[1]}:${m[2]}` : iso;
}

export function ListaSessoesElegiveis({
  sessoes,
  dataRota,
  sessaoSelecionada,
  aoSelecionar,
  carregando,
}: Props) {
  const { doDia, atrasadas } = useMemo(() => {
    const doDia: SessaoElegivel[] = [];
    const atrasadas: SessaoElegivel[] = [];
    for (const s of sessoes) {
      (s.vencida ? atrasadas : doDia).push(s);
    }
    return { doDia, atrasadas };
  }, [sessoes]);

  if (carregando) {
    return <p className="text-sm text-gray-500">Carregando sessões elegíveis…</p>;
  }

  if (sessoes.length === 0) {
    return (
      <div className="rounded-md border border-dashed border-gray-200 bg-gray-50 px-3 py-6 text-center text-sm text-gray-500">
        Nenhum paciente elegível para este dia.
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {doDia.length > 0 ? (
        <Secao titulo={`Do dia (${formatarData(dataRota)})`} total={doDia.length}>
          {doDia.map((s) => (
            <Item
              key={s.sessaoId}
              sessao={s}
              selecionado={sessaoSelecionada?.sessaoId === s.sessaoId}
              aoSelecionar={aoSelecionar}
            />
          ))}
        </Secao>
      ) : null}

      {atrasadas.length > 0 ? (
        <Secao titulo="Pendências de outros dias" total={atrasadas.length} alerta>
          {atrasadas.map((s) => (
            <Item
              key={s.sessaoId}
              sessao={s}
              selecionado={sessaoSelecionada?.sessaoId === s.sessaoId}
              aoSelecionar={aoSelecionar}
            />
          ))}
        </Secao>
      ) : null}
    </div>
  );
}

function Secao({
  titulo,
  total,
  alerta,
  children,
}: {
  titulo: string;
  total: number;
  alerta?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div>
      <h3 className={cn(
        'mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide',
        alerta ? 'text-amber-700' : 'text-gray-600',
      )}>
        {alerta ? <AlertTriangle className="h-3.5 w-3.5" /> : null}
        {titulo}
        <span className="rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium text-gray-600">
          {total}
        </span>
      </h3>
      <ul className="space-y-1.5">{children}</ul>
    </div>
  );
}

function Item({
  sessao,
  selecionado,
  aoSelecionar,
}: {
  sessao: SessaoElegivel;
  selecionado?: boolean;
  aoSelecionar?: (s: SessaoElegivel | null) => void;
}) {
  const hora = formatarHora(sessao.horaPrevistaBusca);
  return (
    <li>
      <button
        type="button"
        onClick={() => aoSelecionar?.(selecionado ? null : sessao)}
        className={cn(
          'w-full rounded-md border px-3 py-2 text-left transition',
          selecionado
            ? 'border-red-500 bg-red-50 ring-1 ring-red-500'
            : 'border-gray-200 bg-white hover:border-red-300 hover:bg-red-50/30',
        )}
      >
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium text-gray-900">{sessao.pacienteNome}</p>
            <p className="truncate text-xs text-gray-500">{sessao.unidadeNome}</p>
          </div>
          {sessao.vencida ? (
            <span className="shrink-0 rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-semibold text-amber-800">
              {formatarData(sessao.dataPrevista)}
            </span>
          ) : null}
        </div>
        {hora ? (
          <div className="mt-1 flex items-center gap-1 text-xs text-gray-500">
            <Clock className="h-3 w-3" />
            {hora}
          </div>
        ) : null}
      </button>
    </li>
  );
}
