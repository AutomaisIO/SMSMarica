import { Lock, LockOpen, Plus, Trash2 } from 'lucide-react';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { CHAVES_NUMERICAS, OBJETIVOS, ROTULOS, capacidadeSemanal, n, turnosSemanais, vagasSemanais } from '@/features/estrategias-fila/lib/parametros';
import type { ChaveNumerica, Objetivo, ParametrosEstrategia } from '@/features/estrategias-fila/types';

type Props = {
  parametros: ParametrosEstrategia;
  aoMudar: (p: ParametrosEstrategia) => void;
  desabilitado?: boolean;
};

/**
 * Cada parâmetro tem um cadeado: <b>travado</b> = fato para o agente (e o backend rejeita
 * proposta que o mude); <b>livre</b> = o agente pode escolher dentro de mín/máx. Mexer no valor
 * não trava — travar é decisão explícita, senão o operador que ajusta um número para "ver o que
 * dá" estaria, sem saber, tirando aquele parâmetro do jogo.
 */
export function PainelParametros({ parametros: p, aoMudar, desabilitado }: Props) {
  function mudar(chave: ChaveNumerica, patch: Partial<ParametrosEstrategia[ChaveNumerica]>) {
    aoMudar({ ...p, [chave]: { ...p[chave], ...patch } });
  }

  function mudarMutirao(i: number, patch: Partial<ParametrosEstrategia['mutiroes'][number]>) {
    const lista = p.mutiroes.map((m, idx) => (idx === i ? { ...m, ...patch } : m));
    aoMudar({ ...p, mutiroes: lista });
  }

  const cap = capacidadeSemanal(p);
  const numero = (v: string) => (v === '' ? 0 : Number(v.replace(',', '.')));

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-sm font-semibold text-gray-900">Parâmetros</h2>
        <p className="text-xs text-gray-500">
          {n(turnosSemanais(p), 1)} turnos × {n(p.atendimentosPorTurno.valor, 1)} = {n(vagasSemanais(p), 1)} vagas/semana →{' '}
          <strong className="text-gray-900">{n(cap, 1)} atendidos/semana</strong> com {Math.round(p.aproveitamento.valor * 100)}% de aproveitamento
        </p>
      </div>

      {/* Objetivo */}
      <div className="mb-3 grid gap-2 sm:grid-cols-[1fr_auto_auto]">
        <label className="text-xs">
          <span className="mb-1 block text-gray-600">Objetivo</span>
          <Select
            value={p.objetivo}
            disabled={desabilitado}
            onChange={(e) => aoMudar({ ...p, objetivo: e.target.value as Objetivo })}
          >
            {OBJETIVOS.map((o) => (
              <option key={o.id} value={o.id}>
                {o.rotulo}
              </option>
            ))}
          </Select>
          <span className="mt-0.5 block text-[10px] text-gray-400">{OBJETIVOS.find((o) => o.id === p.objetivo)?.dica}</span>
        </label>
        <label className="text-xs">
          <span className="mb-1 block text-gray-600">Prazo alvo (semanas)</span>
          <Input
            type="number"
            min={1}
            max={156}
            className="w-28"
            disabled={desabilitado || p.objetivo === 'equilibrio'}
            value={p.prazoAlvoSemanas ?? ''}
            onChange={(e) => aoMudar({ ...p, prazoAlvoSemanas: e.target.value === '' ? null : Math.max(1, Number(e.target.value)) })}
            placeholder="ex.: 26"
          />
        </label>
        <label className="text-xs">
          <span className="mb-1 block text-gray-600">Horizonte</span>
          <Select
            className="w-28"
            disabled={desabilitado}
            value={p.horizonteSemanas}
            onChange={(e) => aoMudar({ ...p, horizonteSemanas: Number(e.target.value) })}
          >
            <option value={52}>1 ano</option>
            <option value={104}>2 anos</option>
            <option value={156}>3 anos</option>
          </Select>
        </label>
      </div>

      {/* Numéricos */}
      <div className="divide-y divide-gray-100 rounded-lg border border-gray-100">
        <div className="grid grid-cols-[1fr_7rem_2.5rem] items-center gap-2 bg-gray-50 px-2 py-1 text-[10px] uppercase tracking-wide text-gray-500 sm:grid-cols-[1fr_7rem_5rem_5rem_2.5rem]">
          <span>Parâmetro</span>
          <span>Valor</span>
          <span className="hidden sm:block">Mín</span>
          <span className="hidden sm:block">Máx</span>
          <span>Trava</span>
        </div>
        {CHAVES_NUMERICAS.map((chave) => {
          const par = p[chave];
          const meta = ROTULOS[chave];
          return (
            <div
              key={chave}
              className={`grid grid-cols-[1fr_7rem_2.5rem] items-center gap-2 px-2 py-1.5 sm:grid-cols-[1fr_7rem_5rem_5rem_2.5rem] ${par.travado ? 'bg-amber-50/50' : ''}`}
            >
              <div>
                <p className="text-xs font-medium text-gray-800">{meta.rotulo}</p>
                <p className="text-[10px] leading-tight text-gray-400">{meta.dica}</p>
              </div>
              <Input
                type="number"
                step={meta.passo}
                min={0}
                disabled={desabilitado}
                value={par.valor}
                onChange={(e) => mudar(chave, { valor: numero(e.target.value) })}
                className="h-8 text-xs"
              />
              <Input
                type="number"
                step={meta.passo}
                disabled={desabilitado || par.travado}
                value={par.min ?? ''}
                placeholder="—"
                onChange={(e) => mudar(chave, { min: e.target.value === '' ? null : numero(e.target.value) })}
                className="hidden h-8 text-xs sm:block"
                title="Mínimo que o agente pode escolher"
              />
              <Input
                type="number"
                step={meta.passo}
                disabled={desabilitado || par.travado}
                value={par.max ?? ''}
                placeholder="—"
                onChange={(e) => mudar(chave, { max: e.target.value === '' ? null : numero(e.target.value) })}
                className="hidden h-8 text-xs sm:block"
                title="Máximo que o agente pode escolher"
              />
              <button
                type="button"
                disabled={desabilitado}
                onClick={() => mudar(chave, { travado: !par.travado })}
                title={par.travado ? 'Travado: o agente não mexe. Clique para liberar.' : 'Livre: o agente pode ajustar. Clique para travar.'}
                className={`inline-flex h-8 w-8 items-center justify-center rounded-md border ${
                  par.travado ? 'border-amber-300 bg-amber-100 text-amber-800' : 'border-gray-200 bg-white text-gray-400 hover:text-gray-700'
                }`}
              >
                {par.travado ? <Lock className="h-3.5 w-3.5" /> : <LockOpen className="h-3.5 w-3.5" />}
              </button>
            </div>
          );
        })}
      </div>

      {/* Mutirões */}
      <div className="mt-3">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-gray-800">Mutirões (vagas extras numa semana)</p>
            <p className="text-[10px] text-gray-400">Para a "corcova" da fila antiga. Semana 1 é a próxima.</p>
          </div>
          <div className="flex items-center gap-1">
            <button
              type="button"
              disabled={desabilitado}
              onClick={() => aoMudar({ ...p, mutiroesTravados: !p.mutiroesTravados })}
              title={p.mutiroesTravados ? 'Travado: o agente não cria nem muda mutirões.' : 'Livre: o agente pode propor mutirões.'}
              className={`inline-flex h-8 w-8 items-center justify-center rounded-md border ${
                p.mutiroesTravados ? 'border-amber-300 bg-amber-100 text-amber-800' : 'border-gray-200 bg-white text-gray-400 hover:text-gray-700'
              }`}
            >
              {p.mutiroesTravados ? <Lock className="h-3.5 w-3.5" /> : <LockOpen className="h-3.5 w-3.5" />}
            </button>
            <button
              type="button"
              disabled={desabilitado}
              onClick={() => aoMudar({ ...p, mutiroes: [...p.mutiroes, { semana: p.mutiroes.length + 1, vagas: 50, descricao: '' }] })}
              className="inline-flex h-8 items-center gap-1 rounded-md border border-gray-200 bg-white px-2 text-xs text-gray-700 hover:bg-gray-50"
            >
              <Plus className="h-3.5 w-3.5" /> Adicionar
            </button>
          </div>
        </div>
        {p.mutiroes.length > 0 ? (
          <div className="mt-2 space-y-1">
            {p.mutiroes.map((m, i) => (
              <div key={i} className="grid grid-cols-[5rem_6rem_1fr_2rem] items-center gap-2">
                <Input type="number" min={1} max={156} className="h-8 text-xs" value={m.semana} disabled={desabilitado} onChange={(e) => mudarMutirao(i, { semana: Math.max(1, Number(e.target.value)) })} title="Semana" />
                <Input type="number" min={1} className="h-8 text-xs" value={m.vagas} disabled={desabilitado} onChange={(e) => mudarMutirao(i, { vagas: Math.max(1, Number(e.target.value)) })} title="Vagas" />
                <Input className="h-8 text-xs" placeholder="descrição (ex.: sábado no CDT)" value={m.descricao ?? ''} disabled={desabilitado} onChange={(e) => mudarMutirao(i, { descricao: e.target.value })} />
                <button type="button" disabled={desabilitado} onClick={() => aoMudar({ ...p, mutiroes: p.mutiroes.filter((_, idx) => idx !== i) })} className="inline-flex h-8 w-8 items-center justify-center text-gray-400 hover:text-red-600" title="Remover">
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            ))}
          </div>
        ) : null}
      </div>
    </section>
  );
}
