import { Fragment, useState } from 'react';
import { Building2, Lock, LockOpen, Trash2, UserPlus } from 'lucide-react';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { DIAS_CURTOS, atendimentosPorTurnoPadrao, capacidadeSemanal, n, turnosSemanais, vagasSemanais } from '@/features/estrategias-fila/lib/parametros';
import type { LinhaQuadro, ParametrosEstrategia } from '@/features/estrategias-fila/types';

const ORDEM_DIAS = [1, 2, 3, 4, 5, 6, 0];
const UNIDADE_SIMULACAO = 'Unidade simulação';

type Props = {
  parametros: ParametrosEstrategia;
  aoMudar: (p: ParametrosEstrategia) => void;
  desabilitado?: boolean;
};

/**
 * O quadro: uma linha por profissional (real, vindo da escala, ou de simulação), sete botões de dia
 * que acendem e apagam, atendimentos por turno e um cadeado. É onde a simulação acontece — a
 * projeção embaixo refaz a cada clique.
 *
 * <p>Regras visuais: dia aceso hoje na escala tem um anel; dia em que o médico real já tem outra
 * escala mostra um ponto âmbar com o motivo — a tela deixa acender (é simulação), mas avisa; o
 * agente não pode.</p>
 */
export function QuadroSimulacao({ parametros: p, aoMudar, desabilitado }: Props) {
  const [novaUnidade, setNovaUnidade] = useState('');

  const unidades = Array.from(
    new Set([...p.quadro.map((l) => l.unidade), ...p.unidadesSimuladas].filter((u) => u && u.length > 0)),
  );

  function mudarLinha(id: string, patch: Partial<LinhaQuadro>) {
    aoMudar({ ...p, quadro: p.quadro.map((l) => (l.id === id ? { ...l, ...patch } : l)) });
  }

  function alternarDia(l: LinhaQuadro, dia: number) {
    const dias = l.dias.includes(dia) ? l.dias.filter((d) => d !== dia) : [...l.dias, dia].sort((a, b) => a - b);
    mudarLinha(l.id, { dias });
  }

  function adicionarMedico(unidade?: string) {
    const n = p.quadro.filter((l) => l.simulado).length + 1;
    const nova: LinhaQuadro = {
      id: `sim-${Date.now().toString(36)}${n}`,
      nome: `Médico simulação ${n}`,
      simulado: true,
      unidadeId: null,
      unidade: unidade ?? unidades[0] ?? UNIDADE_SIMULACAO,
      dias: [1, 2, 3, 4, 5],
      atendimentosPorTurno: atendimentosPorTurnoPadrao(p),
      travado: false,
      diasReais: [],
      outrasEscalas: {},
    };
    const unidadesSimuladas =
      nova.unidade && !unidades.includes(nova.unidade) ? [...p.unidadesSimuladas, nova.unidade] : p.unidadesSimuladas;
    aoMudar({ ...p, quadro: [...p.quadro, nova], unidadesSimuladas });
  }

  function adicionarUnidade() {
    const nome = (novaUnidade.trim() || UNIDADE_SIMULACAO).slice(0, 200);
    let rotulo = nome;
    let i = 2;
    while (unidades.some((u) => u.toLowerCase() === rotulo.toLowerCase())) rotulo = `${nome} ${i++}`;
    setNovaUnidade('');
    // A unidade nova já nasce com um médico de simulação — unidade sem gente não muda a fila.
    const n = p.quadro.filter((l) => l.simulado).length + 1;
    const nova: LinhaQuadro = {
      id: `sim-${Date.now().toString(36)}${n}`,
      nome: `Médico simulação ${n}`,
      simulado: true,
      unidadeId: null,
      unidade: rotulo,
      dias: [1, 2, 3, 4, 5],
      atendimentosPorTurno: atendimentosPorTurnoPadrao(p),
      travado: false,
      diasReais: [],
      outrasEscalas: {},
    };
    aoMudar({ ...p, quadro: [...p.quadro, nova], unidadesSimuladas: [...p.unidadesSimuladas, rotulo] });
  }

  function remover(id: string) {
    aoMudar({ ...p, quadro: p.quadro.filter((l) => l.id !== id) });
  }

  const porUnidade = unidades.map((u) => ({ unidade: u, linhas: p.quadro.filter((l) => l.unidade === u) }));
  const semUnidade = p.quadro.filter((l) => !l.unidade);
  if (semUnidade.length > 0) porUnidade.push({ unidade: '(sem unidade)', linhas: semUnidade });

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
        <div>
          <h2 className="text-sm font-semibold text-gray-900">Quadro de oferta</h2>
          <p className="text-[11px] text-gray-500">
            Clique nos dias para acender e apagar. Anel = dia que a escala publica hoje. Ponto âmbar = o médico já tem
            outra escala nesse dia (a simulação deixa; o agente não).
          </p>
        </div>
        <p className="text-xs text-gray-600">
          <strong className="text-gray-900">{n(turnosSemanais(p))} turnos/semana</strong> · {n(vagasSemanais(p), 1)} vagas →{' '}
          <strong className="text-gray-900">{n(capacidadeSemanal(p), 1)} atendidos/semana</strong>
        </p>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-xs">
          <thead>
            <tr className="text-left text-[10px] uppercase tracking-wide text-gray-500">
              <th className="py-1 pr-2">Profissional</th>
              {ORDEM_DIAS.map((d) => (
                <th key={d} className="px-0.5 py-1 text-center">
                  {DIAS_CURTOS[d]}
                </th>
              ))}
              <th className="px-2 py-1 text-right">Por turno</th>
              <th className="px-2 py-1 text-right">Vagas/sem</th>
              <th className="py-1" />
            </tr>
          </thead>
          <tbody>
            {porUnidade.map((g) => renderGrupo(g.unidade, g.linhas))}
          </tbody>
        </table>
      </div>

      {p.quadro.length === 0 ? (
        <p className="mt-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          Nenhum profissional na escala. Adicione médicos de simulação para ver o que seria preciso.
        </p>
      ) : null}

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <button
          type="button"
          disabled={desabilitado}
          onClick={() => adicionarMedico()}
          className="inline-flex h-8 items-center gap-1 rounded-md border border-violet-200 bg-violet-50 px-2 text-xs text-violet-800 hover:bg-violet-100"
        >
          <UserPlus className="h-3.5 w-3.5" /> Médico simulação
        </button>
        <div className="inline-flex h-8 items-stretch overflow-hidden rounded-md border border-violet-200">
          <Input
            className="h-full w-44 rounded-none border-0 text-xs"
            placeholder="nome da unidade nova"
            value={novaUnidade}
            disabled={desabilitado}
            onChange={(e) => setNovaUnidade(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && adicionarUnidade()}
          />
          <button
            type="button"
            disabled={desabilitado}
            onClick={adicionarUnidade}
            className="inline-flex items-center gap-1 bg-violet-50 px-2 text-xs text-violet-800 hover:bg-violet-100"
            title="Cria uma unidade de simulação já com um médico de simulação"
          >
            <Building2 className="h-3.5 w-3.5" /> Unidade simulação
          </button>
        </div>
        <label className="ml-auto inline-flex items-center gap-2 text-[11px] text-gray-600" title="Se desligado, o agente não pode criar médicos de simulação — só mexer nos dias de quem existe.">
          <input
            type="checkbox"
            checked={p.permitirNovosProfissionais}
            disabled={desabilitado}
            onChange={(e) => aoMudar({ ...p, permitirNovosProfissionais: e.target.checked })}
          />
          agente pode criar médicos, até
          <Input
            type="number"
            min={0}
            max={50}
            className="h-7 w-14 text-xs"
            value={p.maxNovosProfissionais}
            disabled={desabilitado || !p.permitirNovosProfissionais}
            onChange={(e) => aoMudar({ ...p, maxNovosProfissionais: Math.max(0, Number(e.target.value)) })}
          />
        </label>
      </div>
    </section>
  );

  // Função de render, não componente: componente definido dentro do render remonta a cada
  // tecla e o campo de nome do médico de simulação perderia o foco.
  function renderGrupo(unidade: string, linhas: LinhaQuadro[]) {
    const simulada = p.unidadesSimuladas.some((u) => u.toLowerCase() === unidade.toLowerCase());
    return (
      <Fragment key={unidade}>
        <tr className="bg-gray-50">
          <td colSpan={11} className="px-2 py-1 text-[10px] font-semibold uppercase tracking-wide text-gray-600">
            {unidade}
            {simulada ? <span className="ml-1 rounded bg-violet-100 px-1 font-normal normal-case text-violet-800">simulação</span> : null}
            <span className="ml-2 font-normal normal-case text-gray-400">
              {linhas.reduce((s, l) => s + l.dias.length, 0)} turnos · {n(linhas.reduce((s, l) => s + l.dias.length * l.atendimentosPorTurno, 0), 1)} vagas/sem
            </span>
          </td>
        </tr>
        {linhas.map((l) => (
          <tr key={l.id} className={`border-t border-gray-100 ${l.travado ? 'bg-amber-50/40' : ''}`}>
            <td className="py-1 pr-2">
              <div className="flex items-center gap-1">
                {l.simulado ? (
                  <Input
                    className="h-7 w-44 text-xs"
                    value={l.nome}
                    disabled={desabilitado}
                    onChange={(e) => mudarLinha(l.id, { nome: e.target.value.slice(0, 200) })}
                  />
                ) : (
                  <span className="font-medium text-gray-900">{l.nome}</span>
                )}
                {l.simulado ? (
                  <Select
                    className="h-7 w-36 text-xs"
                    value={l.unidade}
                    disabled={desabilitado}
                    onChange={(e) => mudarLinha(l.id, { unidade: e.target.value })}
                  >
                    {unidades.map((u) => (
                      <option key={u} value={u}>
                        {u}
                      </option>
                    ))}
                  </Select>
                ) : null}
              </div>
            </td>
            {ORDEM_DIAS.map((d) => {
              const aceso = l.dias.includes(d);
              const real = l.diasReais.includes(d);
              const ocupado = l.outrasEscalas[d];
              return (
                <td key={d} className="px-0.5 py-1 text-center">
                  <button
                    type="button"
                    disabled={desabilitado}
                    onClick={() => alternarDia(l, d)}
                    title={
                      (ocupado ? `Já tem escala: ${ocupado}\n` : '') +
                      (real ? 'Dia publicado na escala hoje. ' : '') +
                      (aceso ? 'Clique para apagar' : 'Clique para acender')
                    }
                    className={`relative inline-flex h-7 w-8 items-center justify-center rounded text-[10px] font-semibold transition-colors ${
                      aceso
                        ? real
                          ? 'bg-primary-600 text-white ring-2 ring-primary-200'
                          : 'bg-primary-500 text-white'
                        : real
                          ? 'bg-white text-gray-400 ring-2 ring-gray-200 line-through'
                          : 'bg-gray-100 text-gray-400 hover:bg-gray-200'
                    }`}
                  >
                    {DIAS_CURTOS[d]}
                    {ocupado ? <span className="absolute -right-0.5 -top-0.5 h-2 w-2 rounded-full bg-amber-500 ring-1 ring-white" /> : null}
                  </button>
                </td>
              );
            })}
            <td className="px-2 py-1 text-right">
              <Input
                type="number"
                min={0}
                step={1}
                className="h-7 w-16 text-right text-xs"
                value={l.atendimentosPorTurno}
                disabled={desabilitado}
                onChange={(e) => mudarLinha(l.id, { atendimentosPorTurno: Math.max(0, Number(e.target.value)) })}
                title="Vagas de regulação por turno"
              />
            </td>
            <td className="px-2 py-1 text-right font-medium text-gray-900">{n(l.dias.length * l.atendimentosPorTurno, 1)}</td>
            <td className="py-1">
              <div className="flex items-center gap-1">
                <button
                  type="button"
                  disabled={desabilitado}
                  onClick={() => mudarLinha(l.id, { travado: !l.travado })}
                  title={l.travado ? 'Travado: o agente não mexe nesta linha.' : 'Livre: o agente pode mudar dias e vagas desta linha.'}
                  className={`inline-flex h-7 w-7 items-center justify-center rounded-md border ${
                    l.travado ? 'border-amber-300 bg-amber-100 text-amber-800' : 'border-gray-200 bg-white text-gray-400 hover:text-gray-700'
                  }`}
                >
                  {l.travado ? <Lock className="h-3 w-3" /> : <LockOpen className="h-3 w-3" />}
                </button>
                {l.simulado ? (
                  <button type="button" disabled={desabilitado} onClick={() => remover(l.id)} className="inline-flex h-7 w-7 items-center justify-center text-gray-400 hover:text-red-600" title="Remover">
                    <Trash2 className="h-3 w-3" />
                  </button>
                ) : (
                  <span className="inline-flex h-7 w-7" />
                )}
              </div>
            </td>
          </tr>
        ))}
        {linhas.length === 0 ? (
          <tr>
            <td colSpan={11} className="px-2 py-1 text-[11px] text-gray-400">
              Sem profissionais. <button type="button" className="text-violet-700 underline" onClick={() => adicionarMedico(unidade)}>Adicionar médico simulação aqui</button>
            </td>
          </tr>
        ) : null}
      </Fragment>
    );
  }
}
