import { numero } from '@/shared/lib/estatisticasPeriodo';
import { mesesPorAno, rotuloLimpo } from '../lib/indicadores';
import type { ResumoTempo, SecaoIndicador, TabelaIndicador } from '../types';
import { GraficoSecao } from './GraficoSecao';
import { SeloOrigem } from './SeloOrigem';
import { TabelaSerieAno, TabelaSimples, ordenarMotivos } from './Tabelas';

function ResumoJudicial({ r }: { r: ResumoTempo }) {
  if (r.n === 0) {
    return <p className="text-sm text-gray-600">Nenhuma solicitação judicializada foi agendada no período.</p>;
  }
  const itens: { valor: number | null; rotulo: string }[] = [
    { valor: r.n, rotulo: 'judicializadas agendadas' },
    { valor: r.mediana, rotulo: 'mediana de dias até agendar' },
    { valor: r.p90, rotulo: '90% agendadas em até (dias)' },
    { valor: r.menor, rotulo: 'menor tempo (dias)' },
    { valor: r.maior, rotulo: 'maior tempo (dias)' },
  ];
  return (
    <div className="flex flex-wrap gap-x-8 gap-y-3">
      {itens.map((i) => (
        <div key={i.rotulo}>
          <p className="text-xl font-semibold tabular-nums text-gray-900">{numero(i.valor)}</p>
          <p className="text-[11px] text-gray-500">{i.rotulo}</p>
        </div>
      ))}
    </div>
  );
}

/** Tabelas estreitas andam em dupla em tela larga; as largas (a lista do judicial) ocupam a linha. */
function GradeTabelas({ tabelas }: { tabelas: TabelaIndicador[] }) {
  if (tabelas.length === 0) return null;
  return (
    <div className="grid gap-5 lg:grid-cols-2">
      {tabelas.map((t, i) => (
        <div key={`${t.titulo}-${i}`} className={t.colunas.length > 5 ? 'min-w-0 lg:col-span-2' : 'min-w-0'}>
          <TabelaSimples tabela={t} />
        </div>
      ))}
    </div>
  );
}

/**
 * Um bloco do relatório: título e texto; se o sistema não fornece, o aviso tracejado; senão o
 * gráfico, a série mês a mês (uma tabela por ano civil), os motivos, as tabelas de detalhe e as
 * notas de cada indicador.
 */
export function SecaoBloco({ secao, meses }: { secao: SecaoIndicador; meses: string[] }) {
  const anos = mesesPorAno(meses);
  const notas = secao.series.filter((s) => s.nota);
  const tabelas: TabelaIndicador[] = [
    ...(secao.motivos && secao.motivos.linhas.length > 0 ? [ordenarMotivos(secao.motivos)] : []),
    ...secao.tabelas,
  ];

  return (
    <section
      id={`secao-${secao.id}`}
      className="min-w-0 scroll-mt-20 space-y-4 rounded-xl border border-gray-200 bg-white p-4 shadow-sm sm:p-5"
    >
      <header>
        <h2 className="text-base font-semibold text-gray-900">{secao.titulo}</h2>
        {secao.texto && !secao.indisponivel ? (
          <p className="mt-1 max-w-4xl text-sm leading-relaxed text-gray-600">{secao.texto}</p>
        ) : null}
      </header>

      {secao.indisponivel ? (
        <div className="flex flex-col gap-2 rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-3 sm:flex-row sm:items-start sm:gap-3">
          <SeloOrigem selo="Indisponivel" className="self-start" />
          <p className="text-sm leading-relaxed text-gray-600">
            {secao.texto ?? 'O sistema de origem não fornece este indicador ao município.'}
          </p>
        </div>
      ) : (
        <>
          {secao.resumoJudicial ? <ResumoJudicial r={secao.resumoJudicial} /> : null}
          <GraficoSecao secao={secao} meses={meses} />
          {secao.series.length > 0 ? (
            <div className="space-y-3">
              {anos.map((a) => (
                <TabelaSerieAno key={a.ano} series={secao.series} ano={a.ano} meses={a.meses} />
              ))}
            </div>
          ) : null}
          <GradeTabelas tabelas={tabelas} />
          {notas.length > 0 ? (
            <div className="border-t border-gray-100 pt-3">
              <p className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-gray-500">Notas</p>
              <ul className="space-y-1 text-xs leading-relaxed text-gray-600">
                {notas.map((s) => (
                  <li key={s.rotulo}>
                    <strong className="font-semibold text-gray-800">{rotuloLimpo(s.rotulo)}</strong>{' '}
                    <SeloOrigem selo={s.selo} className="mx-0.5 align-middle" /> {s.nota}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </>
      )}
    </section>
  );
}
