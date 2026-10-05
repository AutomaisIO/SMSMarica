import { formatarInstante } from '@/shared/lib/datas';

import { ROTULO_FLUXO, StatusRegulacaoBadge } from './StatusRegulacaoBadge';
import type { SolicitacaoRegulacao } from '../tiposSolicitacao';

/**
 * O cabeçalho do caso — o mesmo para quem pede e para quem regula.
 *
 * <p>As duas telas (o detalhe da unidade e a análise da regulação) diferem no que se pode
 * <b>fazer</b>, não em de quem é o caso: número, paciente, procedimento e situação têm de ser
 * lidos igual dos dois lados, senão o telefonema entre a unidade e o regulador vira "o meu aqui
 * mostra outra coisa".</p>
 */
export function CabecalhoSolicitacao({ s }: { s: SolicitacaoRegulacao }) {
  return (
    <header className="rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-start gap-3">
        <div className="min-w-0 flex-1">
          {/* Depois que existe número externo, é ele que identifica o caso lá fora. */}
          <p className="font-mono text-lg font-semibold text-slate-900">
            {s.numeroExterno ?? `PR-${s.numeroLocal}`}
          </p>
          <h1 className="text-lg font-semibold text-slate-900">{s.pacienteNome}</h1>
          <p className="text-sm text-slate-600">{s.procedimentoNome}</p>
        </div>
        <StatusRegulacaoBadge status={s.status} />
      </div>

      <dl className="mt-3 grid grid-cols-2 gap-x-6 gap-y-1 text-sm sm:grid-cols-3">
        <div>
          <dt className="text-xs text-slate-500">Fluxo</dt>
          <dd className="text-slate-800">{ROTULO_FLUXO[s.fluxo]}</dd>
        </div>
        <div>
          <dt className="text-xs text-slate-500">Destino</dt>
          <dd className="text-slate-800">{s.sistemaDestino ?? '—'}</dd>
        </div>
        <div>
          <dt className="text-xs text-slate-500">Aberta em</dt>
          <dd className="text-slate-800">{formatarInstante(s.criadoEm)}</dd>
        </div>
        {s.pacienteCpf && (
          <div>
            <dt className="text-xs text-slate-500">CPF</dt>
            <dd className="text-slate-800">{s.pacienteCpf}</dd>
          </div>
        )}
      </dl>

      {s.statusMotivo && (
        <p className="mt-3 rounded bg-amber-50 p-2 text-sm text-amber-900">{s.statusMotivo}</p>
      )}
    </header>
  );
}
