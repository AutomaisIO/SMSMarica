import { useEffect, useMemo, useState } from 'react';
import { FileDown, Gauge, Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { baixarPdfIndicadores, mensagemErroPdf } from '../api/indicadoresApi';
import { useIndicadoresRegulacao } from '../api/queries';
import { Resumo } from '../components/Resumo';
import { SecaoBloco } from '../components/SecaoBloco';
import { LegendaSelos } from '../components/SeloOrigem';
import {
  dataHoraBr,
  FONTES_INDICADORES,
  MAXIMO_MESES,
  mesAnoLongo,
  mesAtual,
  presetsPeriodo,
  problemaPeriodo,
} from '../lib/indicadores';
import type { FonteIndicadores, PeriodoMeses } from '../types';

/**
 * Regulação → {SISREG, SER, SERNIT, ESUS SG} → Indicadores: a série mensal dos indicadores de
 * regulação de cada sistema — a versão viva do relatório em PDF de 30/09/2026
 * (`docs/regulacao/relatorio-2025-2026`). Vagas, absenteísmo, regulados, fila, desfechos (com
 * motivos), tempo de espera e judicialização, cada número com o seu selo de origem.
 *
 * <p>A mesma página serve os quatro sistemas; o backend monta as seções (o que cada sistema não
 * fornece volta como Indisponível, e a tela só desenha). Módulo IndicadoresRegulacao (79), só
 * Consulta — que também libera o PDF.</p>
 */
export function IndicadoresRegulacaoPage({ fonte }: { fonte: FonteIndicadores }) {
  const info = FONTES_INDICADORES[fonte];
  const presets = useMemo(() => presetsPeriodo(), []);

  const [presetAtivo, setPresetAtivo] = useState('12m');
  const [periodo, setPeriodo] = useState<PeriodoMeses>({ inicio: '', fim: '' });
  const [baixando, setBaixando] = useState(false);
  const [erroPdf, setErroPdf] = useState<string | null>(null);

  const problema = problemaPeriodo(periodo);
  const q = useIndicadoresRegulacao(fonte, periodo, problema === null);
  const dados = q.data;

  // O erro do PDF é do sistema anterior: não carrega para o próximo.
  useEffect(() => {
    setErroPdf(null);
  }, [fonte]);

  // O que está na tela de fato: sem escolha, o período é o que o backend decidiu.
  const efetivo: PeriodoMeses = {
    inicio: periodo.inicio || dados?.meses[0] || '',
    fim: periodo.fim || dados?.meses[dados.meses.length - 1] || '',
  };

  function escolherPreset(id: string) {
    const p = presets.find((x) => x.id === id);
    if (!p) return;
    setPresetAtivo(id);
    setPeriodo(p.periodo());
    setErroPdf(null);
  }

  function mudarMes(campo: keyof PeriodoMeses, valor: string) {
    if (!valor) return;
    setPresetAtivo('');
    setErroPdf(null);
    setPeriodo({ ...efetivo, [campo]: valor });
  }

  async function exportarPdf() {
    setErroPdf(null);
    setBaixando(true);
    try {
      await baixarPdfIndicadores(fonte, periodo);
    } catch (e) {
      setErroPdf(await mensagemErroPdf(e));
    } finally {
      setBaixando(false);
    }
  }

  return (
    <div className="space-y-5">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Gauge className="h-6 w-6 text-primary-600" />
          Indicadores — {info.sigla}
          <AjudaManual artigo="indicadores-regulacao" />
        </h1>
        <p className="mt-1 max-w-4xl text-sm text-gray-600">
          Série mensal dos indicadores de regulação
          {dados ? (
            <>
              {' '}
              do <strong className="font-medium text-gray-800">{dados.nomeSistema}</strong>
            </>
          ) : null}
          : vagas, absenteísmo, regulados, fila, desfechos, tempo de espera e demandas judiciais. Cada
          número traz o selo de origem — Oficial, Calculado, Parcial ou Indisponível.
        </p>
        {dados && dados.meses.length > 0 ? (
          <p className="mt-1 text-xs text-gray-500">
            {mesAnoLongo(dados.meses[0])} a {mesAnoLongo(dados.meses[dados.meses.length - 1])} ·{' '}
            {dados.meses.length} {dados.meses.length === 1 ? 'mês' : 'meses'} · montado em {dataHoraBr(dados.geradoEm)}
          </p>
        ) : null}
      </header>

      {/* Período + exportação. */}
      <div className="rounded-xl border border-gray-200 bg-white p-3 shadow-sm">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex flex-wrap gap-1">
            {presets.map((p) => (
              <button
                key={p.id}
                type="button"
                title={p.dica}
                onClick={() => escolherPreset(p.id)}
                className={`rounded-full px-3 py-1 text-xs ${
                  presetAtivo === p.id ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                }`}
              >
                {p.rotulo}
              </button>
            ))}
          </div>
          <label className="text-xs text-gray-600">
            De
            <Input
              type="month"
              value={efetivo.inicio}
              max={mesAtual()}
              onChange={(e) => mudarMes('inicio', e.target.value)}
              className="mt-1"
            />
          </label>
          <label className="text-xs text-gray-600">
            Até
            <Input
              type="month"
              value={efetivo.fim}
              max={mesAtual()}
              onChange={(e) => mudarMes('fim', e.target.value)}
              className="mt-1"
            />
          </label>
          <div className="ml-auto flex items-center gap-2">
            {q.isFetching && dados ? (
              <span className="flex items-center gap-1 text-[11px] text-gray-400">
                <Loader2 className="h-3.5 w-3.5 animate-spin" /> Atualizando…
              </span>
            ) : null}
            <Button
              variante="outline"
              tamanho="sm"
              onClick={() => void exportarPdf()}
              disabled={baixando || problema !== null}
              title="Baixa o relatório em PDF do período escolhido"
            >
              {baixando ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileDown className="h-4 w-4" />}
              {baixando ? 'Gerando PDF…' : 'Exportar PDF'}
            </Button>
          </div>
        </div>
        <p className="mt-2 text-[11px] text-gray-400">
          Até {MAXIMO_MESES} meses. Sem escolha, os últimos 12 meses já fechados. O PDF sai com o mesmo período da tela.
        </p>
        {problema ? <p className="mt-2 text-xs text-amber-800">{problema}</p> : null}
        {erroPdf ? (
          <p className="mt-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            Não foi possível gerar o PDF: {erroPdf}
          </p>
        ) : null}
      </div>

      {problema ? null : q.isPending ? (
        <Loader2 className="mx-auto my-10 h-5 w-5 animate-spin text-gray-400" />
      ) : q.isError ? (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(q.error)}
        </p>
      ) : dados ? (
        <>
          <section className="space-y-3">
            <div>
              <h2 className="text-base font-semibold text-gray-900">Resumo</h2>
              <p className="max-w-4xl text-xs text-gray-500">
                Um valor por ano do período. Contagens somadas; percentuais como razão do ano (soma ÷ soma);
                tempos como mediana de todos os casos do ano; fila como a posição no último mês.
              </p>
            </div>
            <Resumo dados={dados} />
          </section>

          <section className="grid gap-5 rounded-xl border border-gray-200 bg-white p-4 shadow-sm sm:p-5 lg:grid-cols-2">
            <div className="min-w-0">
              <h2 className="text-sm font-semibold text-gray-900">Cobertura dos dados</h2>
              {dados.cobertura.length > 0 ? (
                <ul className="mt-2 list-disc space-y-1 pl-5 text-xs leading-relaxed text-gray-600">
                  {dados.cobertura.map((c, i) => (
                    <li key={i}>{c}</li>
                  ))}
                </ul>
              ) : (
                <p className="mt-2 text-xs text-gray-500">Sem observações de cobertura para este sistema.</p>
              )}
            </div>
            <div className="min-w-0">
              <h2 className="mb-2 text-sm font-semibold text-gray-900">Selos de origem</h2>
              <LegendaSelos />
            </div>
          </section>

          {dados.secoes.length > 1 ? (
            <nav aria-label="Seções" className="flex flex-wrap gap-1.5">
              {dados.secoes.map((s) => (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => document.getElementById(`secao-${s.id}`)?.scrollIntoView({ behavior: 'smooth' })}
                  className="rounded-full border border-gray-200 bg-white px-3 py-1 text-xs text-gray-700 hover:border-primary-300 hover:text-primary-700"
                >
                  {s.titulo}
                </button>
              ))}
            </nav>
          ) : null}

          {dados.secoes.map((s) => (
            <SecaoBloco key={s.id} secao={s} meses={dados.meses} />
          ))}
        </>
      ) : null}
    </div>
  );
}
