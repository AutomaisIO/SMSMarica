import { useState } from 'react';
import { AlertTriangle, Check, X } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useListarErrosRobo, useRevisarErroRobo } from '@/features/robo-atendimento/api/queries';
import type { RoboErro, StatusRoboErro } from '@/features/robo-atendimento/types';

const FILTROS: { rotulo: string; valor?: StatusRoboErro }[] = [
  { rotulo: 'Abertos', valor: 'Aberto' },
  { rotulo: 'Revisados', valor: 'Revisado' },
  { rotulo: 'Descartados', valor: 'Descartado' },
  { rotulo: 'Todos', valor: undefined },
];

function dataHora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit',
  });
}

function LinhaErro({ e }: { e: RoboErro }) {
  const revisar = useRevisarErroRobo();
  const [nota, setNota] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  function agir(status: 'Revisado' | 'Descartado') {
    setErro(null);
    revisar.mutate(
      { id: e.id, status, nota: nota.trim() || undefined },
      { onError: (err) => setErro(extrairMensagemDeErro(err)) },
    );
  }

  return (
    <li className="space-y-2 rounded-lg border border-gray-200 bg-white p-3">
      <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
        <span className="rounded-full bg-indigo-50 px-2 py-0.5 font-medium text-indigo-700">
          {e.assunto ?? 'sem assunto'}
        </span>
        <span>{dataHora(e.criadoEm)}</span>
        {e.criadoPorNome ? <span>· por {e.criadoPorNome}</span> : null}
        {e.status !== 'Aberto' ? (
          <span
            className={`rounded-full px-2 py-0.5 font-medium ${
              e.status === 'Revisado' ? 'bg-emerald-50 text-emerald-700' : 'bg-gray-100 text-gray-500'
            }`}
          >
            {e.status}
          </span>
        ) : null}
      </div>

      {e.trecho ? (
        <p className="whitespace-pre-wrap rounded-md bg-gray-50 p-2 text-sm text-gray-800 ring-1 ring-gray-100">
          {e.trecho}
        </p>
      ) : (
        <p className="text-sm italic text-gray-400">(sem trecho da mensagem)</p>
      )}

      {e.nota ? (
        <p className="text-sm text-gray-700">
          <span className="font-medium">Apontamento:</span> {e.nota}
        </p>
      ) : null}

      {e.status === 'Aberto' ? (
        <div className="flex flex-wrap items-center gap-2">
          <input
            value={nota}
            onChange={(ev) => setNota(ev.target.value)}
            placeholder="Nota da revisão (opcional)"
            className="min-w-0 flex-1 rounded-md border border-gray-200 px-2 py-1 text-sm outline-none focus:border-primary-400"
          />
          <button
            type="button"
            onClick={() => agir('Revisado')}
            disabled={revisar.isPending}
            className="inline-flex items-center gap-1 rounded-md bg-emerald-600 px-2.5 py-1 text-xs font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            <Check className="h-3.5 w-3.5" /> Revisado
          </button>
          <button
            type="button"
            onClick={() => agir('Descartado')}
            disabled={revisar.isPending}
            className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-50"
          >
            <X className="h-3.5 w-3.5" /> Descartar
          </button>
        </div>
      ) : e.revisaoNota ? (
        <p className="text-xs text-gray-500">
          <span className="font-medium">Revisão:</span> {e.revisaoNota}
        </p>
      ) : null}

      {erro ? <p className="text-xs text-error-600">{erro}</p> : null}
    </li>
  );
}

/** Erros do robô marcados pelos atendentes, para revisão e refino do treinamento. */
export function ErrosRoboCard() {
  const [status, setStatus] = useState<StatusRoboErro | undefined>('Aberto');
  const lista = useListarErrosRobo(status);

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <AlertTriangle className="h-4 w-4 text-amber-500" /> Erros do robô (treinamento)
        </h2>
        <div className="flex overflow-hidden rounded-md border border-gray-200">
          {FILTROS.map((f) => (
            <button
              key={f.rotulo}
              type="button"
              onClick={() => setStatus(f.valor)}
              className={`border-r border-gray-200 px-2.5 py-1 text-xs last:border-r-0 ${
                status === f.valor ? 'bg-primary-50 font-medium text-primary-700' : 'text-gray-600 hover:bg-gray-50'
              }`}
            >
              {f.rotulo}
            </button>
          ))}
        </div>
      </div>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      {lista.isPending ? (
        <p className="text-sm text-gray-400">Carregando…</p>
      ) : (lista.data?.length ?? 0) === 0 ? (
        <p className="rounded-md border border-dashed border-gray-200 px-3 py-4 text-center text-sm text-gray-400">
          Nenhum erro {status ? status.toLowerCase() : ''} marcado.
        </p>
      ) : (
        <ul className="space-y-2">
          {lista.data!.map((e) => (
            <LinhaErro key={e.id} e={e} />
          ))}
        </ul>
      )}
    </section>
  );
}
