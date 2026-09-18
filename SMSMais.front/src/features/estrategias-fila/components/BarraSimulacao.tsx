import { Lock, LockOpen, Plus, Trash2 } from 'lucide-react';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { OBJETIVOS } from '@/features/estrategias-fila/lib/parametros';
import type { Objetivo, ParametrosEstrategia } from '@/features/estrategias-fila/types';

type Props = {
  parametros: ParametrosEstrategia;
  aoMudar: (p: ParametrosEstrategia) => void;
  desabilitado?: boolean;
};

/**
 * O que não é o quadro: objetivo e prazo, aproveitamento, entrada por semana (travada por padrão
 * na média medida) e mutirões. Compacto de propósito — o quadro é o centro da tela.
 */
export function BarraSimulacao({ parametros: p, aoMudar, desabilitado }: Props) {
  const numero = (v: string) => (v === '' ? 0 : Number(v.replace(',', '.')));

  function Cadeado({ travado, onClick, titulo }: { travado: boolean; onClick: () => void; titulo: string }) {
    return (
      <button
        type="button"
        disabled={desabilitado}
        onClick={onClick}
        title={titulo}
        className={`inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-md border ${
          travado ? 'border-amber-300 bg-amber-100 text-amber-800' : 'border-gray-200 bg-white text-gray-400 hover:text-gray-700'
        }`}
      >
        {travado ? <Lock className="h-3.5 w-3.5" /> : <LockOpen className="h-3.5 w-3.5" />}
      </button>
    );
  }

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-3 shadow-sm">
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <label className="text-xs">
          <span className="mb-1 block text-gray-600">Objetivo</span>
          <Select value={p.objetivo} disabled={desabilitado} onChange={(e) => aoMudar({ ...p, objetivo: e.target.value as Objetivo })}>
            {OBJETIVOS.map((o) => (
              <option key={o.id} value={o.id}>
                {o.rotulo}
              </option>
            ))}
          </Select>
        </label>

        <div className="grid grid-cols-2 gap-2">
          <label className="text-xs">
            <span className="mb-1 block text-gray-600">Prazo (semanas)</span>
            <Input
              type="number"
              min={1}
              max={156}
              disabled={desabilitado || p.objetivo === 'equilibrio'}
              value={p.prazoAlvoSemanas ?? ''}
              placeholder="ex.: 26"
              onChange={(e) => aoMudar({ ...p, prazoAlvoSemanas: e.target.value === '' ? null : Math.max(1, Number(e.target.value)) })}
            />
          </label>
          <label className="text-xs">
            <span className="mb-1 block text-gray-600">Horizonte</span>
            <Select disabled={desabilitado} value={p.horizonteSemanas} onChange={(e) => aoMudar({ ...p, horizonteSemanas: Number(e.target.value) })}>
              <option value={52}>1 ano</option>
              <option value={104}>2 anos</option>
              <option value={156}>3 anos</option>
            </Select>
          </label>
        </div>

        <label className="text-xs" title="Fração das vagas ofertadas que viram atendimento, medida nas últimas 8 semanas. Escala viva com vaga morta aparece aqui.">
          <span className="mb-1 block text-gray-600">Aproveitamento das vagas ({Math.round(p.aproveitamento.valor * 100)}%)</span>
          <div className="flex items-center gap-2">
            <input
              type="range"
              min={0}
              max={1}
              step={0.05}
              className="w-full"
              disabled={desabilitado}
              value={p.aproveitamento.valor}
              onChange={(e) => aoMudar({ ...p, aproveitamento: { ...p.aproveitamento, valor: Number(e.target.value) } })}
            />
            <Cadeado
              travado={p.aproveitamento.travado}
              titulo={p.aproveitamento.travado ? 'Travado: o agente não mexe.' : 'Livre: o agente pode supor melhora no aproveitamento (com ação para isso).'}
              onClick={() => aoMudar({ ...p, aproveitamento: { ...p.aproveitamento, travado: !p.aproveitamento.travado } })}
            />
          </div>
        </label>

        <label className="text-xs" title="Pessoas novas por semana. Vem travada na média medida; destrave para simular a demanda crescendo ou caindo.">
          <span className="mb-1 block text-gray-600">Entram por semana</span>
          <div className="flex items-center gap-2">
            <Input
              type="number"
              min={0}
              step={1}
              disabled={desabilitado}
              value={p.entradaSemanal.valor}
              onChange={(e) => aoMudar({ ...p, entradaSemanal: { ...p.entradaSemanal, valor: numero(e.target.value) } })}
            />
            <Cadeado
              travado={p.entradaSemanal.travado}
              titulo={p.entradaSemanal.travado ? 'Travado: o agente usa a entrada medida.' : 'Livre: o agente pode variar a entrada.'}
              onClick={() => aoMudar({ ...p, entradaSemanal: { ...p.entradaSemanal, travado: !p.entradaSemanal.travado } })}
            />
          </div>
        </label>
      </div>

      {/* Mutirões */}
      <div className="mt-3 flex flex-wrap items-center gap-2">
        <p className="text-xs text-gray-600">
          <strong className="text-gray-800">Mutirões</strong>{' '}
          <span className="text-[10px] text-gray-400">vagas extras numa semana (semana 1 = próxima) — para a "corcova" da fila antiga</span>
        </p>
        <div className="ml-auto flex items-center gap-1">
          <Cadeado
            travado={p.mutiroesTravados}
            titulo={p.mutiroesTravados ? 'Travado: o agente não cria nem muda mutirões.' : 'Livre: o agente pode propor mutirões.'}
            onClick={() => aoMudar({ ...p, mutiroesTravados: !p.mutiroesTravados })}
          />
          <button
            type="button"
            disabled={desabilitado}
            onClick={() => aoMudar({ ...p, mutiroes: [...p.mutiroes, { semana: p.mutiroes.length + 1, vagas: 50, descricao: '' }] })}
            className="inline-flex h-8 items-center gap-1 rounded-md border border-gray-200 bg-white px-2 text-xs text-gray-700 hover:bg-gray-50"
          >
            <Plus className="h-3.5 w-3.5" /> Mutirão
          </button>
        </div>
      </div>
      {p.mutiroes.length > 0 ? (
        <div className="mt-2 space-y-1">
          {p.mutiroes.map((m, i) => (
            <div key={i} className="grid grid-cols-[5rem_6rem_1fr_2rem] items-center gap-2">
              <Input type="number" min={1} max={156} className="h-8 text-xs" value={m.semana} disabled={desabilitado} onChange={(e) => aoMudar({ ...p, mutiroes: p.mutiroes.map((x, idx) => (idx === i ? { ...x, semana: Math.max(1, Number(e.target.value)) } : x)) })} title="Semana" />
              <Input type="number" min={1} className="h-8 text-xs" value={m.vagas} disabled={desabilitado} onChange={(e) => aoMudar({ ...p, mutiroes: p.mutiroes.map((x, idx) => (idx === i ? { ...x, vagas: Math.max(1, Number(e.target.value)) } : x)) })} title="Vagas" />
              <Input className="h-8 text-xs" placeholder="descrição (ex.: sábado no CDT)" value={m.descricao ?? ''} disabled={desabilitado} onChange={(e) => aoMudar({ ...p, mutiroes: p.mutiroes.map((x, idx) => (idx === i ? { ...x, descricao: e.target.value } : x)) })} />
              <button type="button" disabled={desabilitado} onClick={() => aoMudar({ ...p, mutiroes: p.mutiroes.filter((_, idx) => idx !== i) })} className="inline-flex h-8 w-8 items-center justify-center text-gray-400 hover:text-red-600" title="Remover">
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
          ))}
        </div>
      ) : null}
    </section>
  );
}
