import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Loader2, Search, Target } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Input } from '@/shared/ui/Input';
import { Tabs } from '@/shared/ui/Tabs';
import { useEstrategias, useProcedimentosComFila } from '@/features/estrategias-fila/api/queries';
import { STATUS_ROTULO, dataHoraBr, fraseRodada, n } from '@/features/estrategias-fila/lib/parametros';
import type { EstrategiaResumo, ProcedimentoComFila } from '@/features/estrategias-fila/types';

const ORDENS = [
  { id: 'fila', rotulo: 'Maior fila' },
  { id: 'espera', rotulo: 'Maior espera' },
  { id: 'nome', rotulo: 'Nome' },
] as const;

function classeEspera(dias: number | null) {
  if (dias === null) return 'text-gray-400';
  if (dias > 180) return 'text-red-700';
  if (dias > 90) return 'text-orange-600';
  if (dias > 30) return 'text-amber-600';
  return 'text-emerald-700';
}

/** A lista que se escolhe para simular — com o tamanho da fila ao lado do nome. */
function AbaProcedimentos() {
  const navigate = useNavigate();
  const [busca, setBusca] = useState('');
  const [ordenar, setOrdenar] = useState<(typeof ORDENS)[number]['id']>('fila');
  const lista = useProcedimentosComFila(busca.trim(), ordenar);

  const maximo = Math.max(1, ...(lista.data?.map((p) => p.naFila) ?? [1]));

  function abrir(p: ProcedimentoComFila) {
    const q = new URLSearchParams({ nome: p.nome });
    if (p.codigo) q.set('codigo', p.codigo);
    navigate(`/app/agenda/estrategias/nova?${q.toString()}`);
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-gray-400" />
          <Input
            className="pl-8"
            placeholder="Buscar procedimento (nome ou código do SISREG)"
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
          />
        </div>
        <div className="flex gap-1">
          {ORDENS.map((o) => (
            <button
              key={o.id}
              type="button"
              onClick={() => setOrdenar(o.id)}
              className={`rounded-full px-3 py-1 text-xs ${ordenar === o.id ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-700'}`}
            >
              {o.rotulo}
            </button>
          ))}
        </div>
      </div>

      {lista.isError ? (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{extrairMensagemDeErro(lista.error)}</p>
      ) : null}

      <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        {lista.isPending ? (
          <div className="flex items-center justify-center py-10 text-gray-400">
            <Loader2 className="h-5 w-5 animate-spin" />
            <span className="ml-2 text-xs">Contando a fila de cada procedimento…</span>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left text-[11px] uppercase tracking-wide text-gray-500">
              <tr>
                <th className="px-3 py-2">Procedimento</th>
                <th className="px-3 py-2 text-right">Na fila</th>
                <th className="hidden px-3 py-2 sm:table-cell" />
                <th className="px-3 py-2 text-right">Espera mediana</th>
                <th className="hidden px-3 py-2 text-right md:table-cell">Vagas reg./sem</th>
                <th className="hidden px-3 py-2 text-right md:table-cell">Unid. / prof.</th>
                <th className="px-3 py-2" />
              </tr>
            </thead>
            <tbody>
              {(lista.data ?? []).map((p) => (
                <tr
                  key={`${p.codigo ?? ''}|${p.nome}`}
                  onClick={() => abrir(p)}
                  className="cursor-pointer border-t border-gray-100 hover:bg-gray-50"
                  title="Simular estratégias para este procedimento"
                >
                  <td className="px-3 py-2">
                    <p className="font-medium text-gray-900">
                      {p.nome}
                      {p.ehGrupo ? <span className="ml-1 rounded bg-gray-100 px-1 text-[10px] text-gray-600">grupo</span> : null}
                      {p.codigo === null ? <span className="ml-1 rounded bg-amber-100 px-1 text-[10px] text-amber-800">sem escala</span> : null}
                    </p>
                    <p className="text-[11px] text-gray-500">
                      {p.codigo ?? 'só na fila'}
                      {p.nomeCanonico && p.nomeCanonico !== p.nome ? ` · ${p.nomeCanonico}` : ''}
                    </p>
                  </td>
                  <td className="px-3 py-2 text-right text-base font-semibold text-red-700">{n(p.naFila)}</td>
                  <td className="hidden px-3 py-2 sm:table-cell">
                    <div className="h-1.5 w-24 overflow-hidden rounded-full bg-gray-100">
                      <div className="h-full bg-red-400" style={{ width: `${Math.round((p.naFila / maximo) * 100)}%` }} />
                    </div>
                  </td>
                  <td className={`px-3 py-2 text-right ${classeEspera(p.esperaMedianaDias)}`}>
                    {p.esperaMedianaDias === null ? '—' : `${p.esperaMedianaDias} d`}
                  </td>
                  <td className="hidden px-3 py-2 text-right md:table-cell">{n(p.vagasRegulacaoSemana)}</td>
                  <td className="hidden px-3 py-2 text-right text-gray-500 md:table-cell">
                    {p.unidades} / {p.profissionais}
                  </td>
                  <td className="px-3 py-2 text-right">
                    {p.temEstrategia ? <span className="rounded-full bg-sky-100 px-2 py-0.5 text-[10px] text-sky-800">tem estratégia</span> : null}
                  </td>
                </tr>
              ))}
              {lista.data?.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-3 py-8 text-center text-xs text-gray-500">
                    Nenhum procedimento com esse nome.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

function AbaSalvas() {
  const navigate = useNavigate();
  const [incluirArquivadas, setIncluirArquivadas] = useState(false);
  const lista = useEstrategias(incluirArquivadas);

  return (
    <div className="space-y-3">
      <label className="inline-flex items-center gap-2 text-xs text-gray-600">
        <input type="checkbox" checked={incluirArquivadas} onChange={(e) => setIncluirArquivadas(e.target.checked)} />
        Mostrar arquivadas
      </label>

      <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        {lista.isPending ? (
          <Loader2 className="mx-auto my-10 h-5 w-5 animate-spin text-gray-400" />
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left text-[11px] uppercase tracking-wide text-gray-500">
              <tr>
                <th className="px-3 py-2">Estratégia</th>
                <th className="px-3 py-2">Procedimento</th>
                <th className="px-3 py-2">Situação</th>
                <th className="px-3 py-2">Resultado atual</th>
                <th className="hidden px-3 py-2 text-right sm:table-cell">Rodadas</th>
                <th className="hidden px-3 py-2 md:table-cell">Atualizada</th>
              </tr>
            </thead>
            <tbody>
              {(lista.data ?? []).map((e: EstrategiaResumo) => (
                <tr
                  key={e.id}
                  onClick={() => navigate(`/app/agenda/estrategias/${e.id}`)}
                  className="cursor-pointer border-t border-gray-100 hover:bg-gray-50"
                  title="Abrir a estratégia"
                >
                  <td className="px-3 py-2 font-medium text-gray-900">{e.nome}</td>
                  <td className="px-3 py-2 text-gray-700">{e.procedimentoNome}</td>
                  <td className="px-3 py-2">
                    <span className={`rounded-full px-2 py-0.5 text-[10px] ${STATUS_ROTULO[e.status].classe}`}>{STATUS_ROTULO[e.status].rotulo}</span>
                  </td>
                  <td className="px-3 py-2 text-xs">
                    <span className={e.rodadaAtual?.zera ? 'text-emerald-700' : 'text-red-700'}>
                      {fraseRodada(e.rodadaAtual)}
                    </span>
                  </td>
                  <td className="hidden px-3 py-2 text-right sm:table-cell">{e.rodadas}</td>
                  <td className="hidden px-3 py-2 text-xs text-gray-500 md:table-cell">{dataHoraBr(e.atualizadoEm ?? e.criadoEm)}</td>
                </tr>
              ))}
              {lista.data?.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-3 py-8 text-center text-xs text-gray-500">
                    Nenhuma estratégia salva ainda. Escolha um procedimento na outra aba para começar.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

/**
 * Estratégias de fila: escolher um procedimento (com a fila ao lado do nome) para simular, ou
 * reabrir uma estratégia salva.
 *
 * <p>Nada aqui escreve no SISREG. A estratégia é planejamento guardado no nosso banco, para ser
 * consultada e, se alguém a executar por fora, marcada como aplicada.</p>
 */
export function EstrategiasFilaPage() {
  const salvas = useEstrategias(false);
  return (
    <div className="space-y-5">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Target className="h-6 w-6 text-primary-600" />
          Estratégias de fila
        </h1>
        <p className="mt-1 max-w-4xl text-sm text-gray-600">
          Escolha um procedimento, veja o cenário de hoje (fila, ritmo de entrada, oferta) e simule mudanças na oferta até
          a fila zerar — à mão ou pedindo ao agente. A estratégia fica salva para consulta; nada é aplicado no SISREG.
        </p>
      </header>

      <Tabs
        abas={[
          { id: 'procedimentos', rotulo: 'Procedimentos', conteudo: <AbaProcedimentos /> },
          { id: 'salvas', rotulo: 'Estratégias salvas', badge: salvas.data?.length, conteudo: <AbaSalvas /> },
        ]}
      />
    </div>
  );
}
