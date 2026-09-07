import { FileCheck2, Loader2 } from 'lucide-react';

import { formatarInstanteData } from '@/shared/lib/datas';

import { useExamesInternos, useUsarExameInterno } from '../api/solicitacoesQueries';

/**
 * Oferece, dentro da caixinha, os exames que a própria rede já fez (R-09).
 *
 * <p><b>Por que isto existe:</b> pedir ao paciente um laudo que o município produziu e guarda é
 * mandá-lo buscar papel de um exame que está aqui do lado. O sistema oferece; <b>quem confirma
 * que aquele exame é o pedido é uma pessoa</b> — daí serem botões, e não um preenchimento
 * automático.</p>
 */
export function ExamesInternosSugeridos({
  solicitacaoId,
  exigenciaId,
  aoUsar,
}: {
  solicitacaoId: string;
  exigenciaId: string;
  aoUsar?: () => void;
}) {
  const exames = useExamesInternos(solicitacaoId, exigenciaId);
  const usar = useUsarExameInterno();

  if (exames.isLoading) {
    return (
      <p className="flex items-center gap-1 text-xs text-slate-500">
        <Loader2 className="size-3 animate-spin" /> procurando exames nossos…
      </p>
    );
  }

  const lista = exames.data ?? [];
  if (lista.length === 0) return null;

  return (
    <div className="mt-2 rounded border border-emerald-200 bg-emerald-50 p-2">
      <p className="text-xs font-medium text-emerald-900">
        O SMSMais já tem {lista.length} exame(s) que podem servir aqui:
      </p>
      <ul className="mt-1.5 space-y-1">
        {lista.map((e) => (
          <li key={e.id} className="flex flex-wrap items-center gap-2 text-sm">
            <FileCheck2 className="size-4 shrink-0 text-emerald-700" />
            <span className="min-w-0 flex-1 truncate text-slate-800">
              {e.descricao}{' '}
              <span className="text-slate-500">— {formatarInstanteData(e.realizadoEm)}</span>
              {/* O laudo é o que a regulação lê; sem ele vai o PDF das imagens. */}
              {e.laudado && <span className="text-emerald-700"> (com laudo)</span>}
            </span>
            <button
              type="button"
              className="rounded bg-emerald-700 px-2 py-1 text-xs font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
              disabled={usar.isPending}
              onClick={async () => {
                await usar.mutateAsync({
                  solicitacaoId,
                  exigenciaId,
                  exameId: e.id,
                  laudoId: e.laudoId,
                });
                aoUsar?.();
              }}
            >
              Usar este
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
