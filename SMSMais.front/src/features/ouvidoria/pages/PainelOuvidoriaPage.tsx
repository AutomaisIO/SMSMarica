import { useState } from 'react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { BarChart3 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { hojeSP } from '@/shared/lib/datas';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { usePainelOuvidoria } from '@/features/ouvidoria/api/queries';
import { SeletorUnidade } from '@/features/ouvidoria/components/Seletores';
import { ROTULO_FAIXA_PRAZO, type FaixaPrazo } from '@/features/ouvidoria/lib/rotulos';
import type { ContagemDto } from '@/features/ouvidoria/types';

const COR_BARRA = '#b91c1c';
const COR_GRADE = '#e5e7eb';

function primeiroDiaDoMes(): string {
  return `${hojeSP().slice(0, 7)}-01`;
}

const fmt = (n: number) => n.toLocaleString('pt-BR');
const pct = (parte: number, total: number) => (total > 0 ? `${Math.round((parte / total) * 100)}%` : '—');

/** Indicadores da ouvidoria no período: cards + gráficos simples (por tipo, status, assunto, faixa de prazo). */
export function PainelOuvidoriaPage() {
  const [de, setDe] = useState(primeiroDiaDoMes());
  const [ate, setAte] = useState(hojeSP());
  const [unidadeId, setUnidadeId] = useState('');
  const { data, isLoading, isError, error } = usePainelOuvidoria(de, ate, unidadeId || undefined);

  const faixas = (data?.faixasPrazo ?? []).map((f) => ({
    ...f,
    rotulo: ROTULO_FAIXA_PRAZO[f.chave as FaixaPrazo] ?? f.rotulo,
  }));

  return (
    <div className="space-y-5">
      <div>
        <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-800">
          <BarChart3 className="h-5 w-5 text-red-600" aria-hidden="true" />
          Painel da ouvidoria
        </h1>
        <p className="text-sm text-slate-500">Manifestações registradas no período, resposta, prazo e resolutividade.</p>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <Campo label="De" htmlFor="pn-de">
          <Input id="pn-de" type="date" value={de} max={ate} onChange={(e) => setDe(e.target.value)} />
        </Campo>
        <Campo label="Até" htmlFor="pn-ate">
          <Input id="pn-ate" type="date" value={ate} min={de} max={hojeSP()} onChange={(e) => setAte(e.target.value)} />
        </Campo>
        <Campo label="Unidade" htmlFor="pn-unidade" className="w-64">
          <SeletorUnidade id="pn-unidade" value={unidadeId} onChange={setUnidadeId} rotuloVazio="Todas as unidades" />
        </Campo>
      </div>

      {isError ? (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(error)}
        </p>
      ) : null}
      {isLoading ? <p className="text-sm text-slate-500">Carregando…</p> : null}

      {data ? (
        <>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
            <Cartao rotulo="Registradas" valor={fmt(data.total)} />
            <Cartao rotulo="Respondidas" valor={fmt(data.respondidas)} detalhe={pct(data.respondidas, data.total) + ' do total'} />
            <Cartao
              rotulo="No prazo"
              valor={pct(data.noPrazo, data.noPrazo + data.foraPrazo)}
              detalhe={`${fmt(data.noPrazo)} no prazo · ${fmt(data.foraPrazo)} fora`}
              destaque={data.foraPrazo > data.noPrazo ? 'ruim' : 'bom'}
            />
            <Cartao
              rotulo="Tempo médio de resposta"
              valor={data.tempoMedioDias != null ? `${data.tempoMedioDias.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} d` : '—'}
              detalhe={data.tempoMedioAreaDias != null ? `área: ${data.tempoMedioAreaDias.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} d` : undefined}
            />
            <Cartao rotulo="Estoque (em aberto)" valor={fmt(data.estoque)} />
            <Cartao
              rotulo="Resolutividade"
              valor={pct(data.resolvidas, data.resolvidas + data.naoResolvidas)}
              detalhe={`${fmt(data.resolvidas)} resolvidas · ${fmt(data.naoResolvidas)} não`}
            />
          </div>

          <div className="grid gap-5 lg:grid-cols-2">
            <Grafico titulo="Por tipo" dados={data.porTipo} />
            <Grafico titulo="Por status" dados={data.porStatus} />
            <Grafico titulo="Assuntos mais frequentes (top 10)" dados={[...data.porAssunto].sort((a, b) => b.quantidade - a.quantidade).slice(0, 10)} horizontal />
            <Grafico titulo="Tempo até a resposta (faixas)" dados={faixas} />
          </div>
        </>
      ) : null}
    </div>
  );
}

function Cartao({ rotulo, valor, detalhe, destaque }: { rotulo: string; valor: string; detalhe?: string; destaque?: 'bom' | 'ruim' }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{rotulo}</p>
      <p className={`mt-1 text-2xl font-semibold tabular-nums ${destaque === 'ruim' ? 'text-red-700' : destaque === 'bom' ? 'text-green-700' : 'text-slate-900'}`}>{valor}</p>
      {detalhe ? <p className="mt-0.5 text-xs text-slate-500">{detalhe}</p> : null}
    </div>
  );
}

function Grafico({ titulo, dados, horizontal }: { titulo: string; dados: ContagemDto[]; horizontal?: boolean }) {
  const altura = horizontal ? Math.max(200, dados.length * 32 + 40) : 240;
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm" aria-label={titulo}>
      <h3 className="mb-2 text-sm font-semibold text-slate-700">{titulo}</h3>
      {dados.length === 0 ? (
        <p className="py-8 text-center text-sm text-slate-400">Sem dados no período.</p>
      ) : (
        <div style={{ height: altura }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={dados} layout={horizontal ? 'vertical' : 'horizontal'} margin={{ top: 8, right: 16, bottom: 8, left: horizontal ? 8 : 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke={COR_GRADE} vertical={!!horizontal} horizontal={!horizontal} />
              {horizontal ? (
                <>
                  <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11 }} />
                  <YAxis type="category" dataKey="rotulo" width={160} tick={{ fontSize: 11 }} />
                </>
              ) : (
                <>
                  <XAxis dataKey="rotulo" tick={{ fontSize: 11 }} interval={0} angle={dados.length > 6 ? -20 : 0} textAnchor={dados.length > 6 ? 'end' : 'middle'} height={dados.length > 6 ? 56 : 30} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} width={36} />
                </>
              )}
              <Tooltip formatter={(v: number) => [fmt(v), 'Manifestações']} cursor={{ fill: '#f8fafc' }} />
              <Bar dataKey="quantidade" name="Manifestações" fill={COR_BARRA} radius={3} isAnimationActive={false} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
      <ul className="sr-only">
        {dados.map((d) => (
          <li key={d.chave}>
            {d.rotulo}: {fmt(d.quantidade)}
          </li>
        ))}
      </ul>
    </section>
  );
}
