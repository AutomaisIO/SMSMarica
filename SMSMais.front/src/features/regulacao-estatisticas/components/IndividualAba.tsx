import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { diaBr, dias, mediana, numero, variacao } from '@/shared/lib/estatisticasPeriodo';
import { Select } from '@/shared/ui/Select';
import { useEquipe, useIndividual } from '../api/queries';
import { FONTES, hora } from '../lib/rotulos';
import type { FonteExterna, OperadorExternoPeriodo, Periodo } from '../types';
import {
  Cartao,
  Carregando,
  GraficoDiaSemana,
  GraficoPorDia,
  GraficoPorHora,
  GraficoPorMes,
  ListaTop,
  Secao,
} from './Blocos';

/** "mediana da equipe: 120,5" — referência para comparar a pessoa com o grupo. */
function ref(ranking: OperadorExternoPeriodo[], campo: keyof OperadorExternoPeriodo, formatar: (n: number) => string) {
  const m = mediana(ranking.map((o) => o[campo] as number | null));
  return m === null ? undefined : `mediana da equipe: ${formatar(m)}`;
}

/** Uma pessoa no período: os mesmos números da linha do ranking, abertos, e comparados com a equipe. */
export function IndividualAba({
  fonte,
  periodo,
  chave,
  aoEscolher,
}: {
  fonte: FonteExterna;
  periodo: Periodo;
  chave: string | null;
  aoEscolher: (chave: string | null) => void;
}) {
  const equipe = useEquipe(fonte, periodo);
  const q = useIndividual(fonte, chave, periodo);
  const ranking = equipe.data?.ranking ?? [];
  const posicao = chave ? ranking.findIndex((o) => o.chave === chave) : -1;
  const sigla = FONTES[fonte].sigla;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end gap-3">
        <label className="min-w-72 flex-1 text-xs text-gray-600">
          Operador
          <Select value={chave ?? ''} onChange={(ev) => aoEscolher(ev.target.value || null)} className="mt-1">
            <option value="">Escolha um operador…</option>
            {ranking.map((o) => (
              <option key={o.chave} value={o.chave}>
                {o.nome} — {numero(o.acoes)} ações
              </option>
            ))}
          </Select>
        </label>
        {equipe.data && ranking.length === 0 ? (
          <p className="text-xs text-gray-500">Ninguém da equipe agiu neste período.</p>
        ) : null}
      </div>

      {!chave ? (
        <p className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">
          Escolha um operador acima, ou clique numa linha do ranking na aba Equipe.
        </p>
      ) : q.isPending ? (
        <Carregando />
      ) : q.isError ? (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(q.error)}
        </p>
      ) : (
        (() => {
          const d = q.data;
          const m = d.metricas;
          const totalEquipe = equipe.data?.atual.acoes ?? 0;

          return (
            <>
              <header className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
                <h2 className="text-lg font-semibold text-gray-900">{d.nome}</h2>
                <p className="text-xs text-gray-500">
                  Nome no {sigla} · {diaBr(d.atual.de)} a {diaBr(d.atual.ate)}
                  {posicao >= 0 ? ` · ${posicao + 1}º de ${ranking.length} no ranking` : ''}
                  {m && totalEquipe ? ` · ${numero((100 * m.acoes) / totalEquipe, 1)}% das ações da equipe` : ''}
                </p>
                {m?.lotacoes.length ? (
                  <p className="mt-1 text-[11px] text-gray-400">Lotação: {m.lotacoes.join(' · ')}</p>
                ) : null}
              </header>

              {!m ? (
                <p className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">
                  Nenhuma ação neste período.
                </p>
              ) : (
                <>
                  <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
                    <Cartao
                      rotulo="Ações"
                      valor={numero(m.acoes)}
                      variacao={variacao(m.acoes, d.anterior.acoes)}
                      referencia={ref(ranking, 'acoes', (n) => numero(n))}
                      dica="Todos os eventos assinados no período (qualquer verbo)."
                    />
                    <Cartao
                      rotulo="Agendamentos"
                      valor={numero(m.agendamentos)}
                      variacao={variacao(m.agendamentos, d.anterior.agendamentos)}
                      referencia={ref(ranking, 'agendamentos', (n) => numero(n))}
                      dica="Agendamentos e reagendamentos."
                    />
                    <Cartao
                      rotulo="Cancelamentos"
                      valor={numero(m.cancelamentos)}
                      variacao={variacao(m.cancelamentos, d.anterior.cancelamentos)}
                      referencia={`${numero(m.pendencias)} pendência(s)`}
                      dica="Cancelamentos registrados."
                    />
                    <Cartao
                      rotulo="FollowUPs"
                      valor={numero(m.followUps)}
                      variacao={variacao(m.followUps, d.anterior.followUps)}
                      referencia={ref(ranking, 'followUps', (n) => numero(n))}
                      dica="Tentativas de contato, cobranças e orientações registradas."
                    />
                    <Cartao
                      rotulo="Solicitações tocadas"
                      valor={numero(m.solicitacoes)}
                      variacao={variacao(m.solicitacoes, d.anterior.solicitacoes)}
                      referencia={ref(ranking, 'solicitacoes', (n) => numero(n))}
                      dica="Solicitações diferentes em que mexeu."
                    />
                    <Cartao
                      rotulo="Dias trabalhados"
                      valor={`${numero(m.diasTrabalhados)} de ${numero(m.diasCorridos)}`}
                      variacao={variacao(m.diasTrabalhados, d.anterior.diasComAtividade)}
                      referencia={ref(ranking, 'diasTrabalhados', (n) => numero(n))}
                      dica="Dias com pelo menos uma ação, de todos os dias do período."
                    />
                    <Cartao
                      rotulo="Média por dia trabalhado"
                      valor={numero(m.mediaDiaTrabalhado, 1)}
                      variacao={variacao(m.mediaDiaTrabalhado, d.anterior.mediaDiaComAtividade)}
                      referencia={ref(ranking, 'mediaDiaTrabalhado', (n) => numero(n, 1))}
                      dica="A produção de um dia de trabalho: ações ÷ dias trabalhados."
                    />
                    <Cartao
                      rotulo="Pico diário"
                      valor={numero(m.picoDiario)}
                      referencia={m.diaDoPico ? `em ${diaBr(m.diaDoPico)}` : undefined}
                      dica="Maior número de ações num único dia."
                    />
                    <Cartao
                      rotulo="Espera até agendar"
                      valor={dias(m.esperaMedianaDias)}
                      referencia={ref(ranking, 'esperaMedianaDias', (n) => dias(n))}
                      dica={`Mediana de dias entre a solicitação e o agendamento. 90% até ${dias(m.esperaP90Dias)}.`}
                    />
                    <Cartao
                      rotulo="Jornada"
                      valor={`${hora(m.horaInicioMediana)} – ${hora(m.horaFimMediana)}`}
                      referencia={`${numero(m.percentualFimDeSemana, 1)}% no fim de semana`}
                      dica="Mediana da hora da primeira e da última ação de cada dia trabalhado."
                    />
                    <Cartao
                      rotulo="Amplitude"
                      valor={`${numero(m.recursos)} recurso(s)`}
                      referencia={`${numero(m.unidadesExecutoras)} unidade(s) executora(s)`}
                      dica="Recursos e unidades diferentes no trabalho do período."
                    />
                  </div>

                  <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
                    <Secao titulo="Dia a dia" descricao="Dias sem barra são dias sem ação.">
                      <GraficoPorDia dados={d.porDia} mostrarOperadores={false} />
                    </Secao>
                    <Secao titulo="Mês a mês">
                      <GraficoPorMes dados={d.porMes} mostrarOperadores={false} />
                    </Secao>
                  </div>

                  <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-4">
                    <Secao titulo="Ações por tipo">
                      <ListaTop itens={d.porTipoEvento} />
                    </Secao>
                    <Secao titulo="Recursos mais tocados">
                      <ListaTop itens={d.topRecursos} />
                    </Secao>
                    <Secao titulo="Unidades executoras">
                      <ListaTop itens={d.topUnidadesExecutoras} />
                    </Secao>
                    <Secao titulo="Lotações">
                      <ListaTop itens={d.topLotacoes} />
                    </Secao>
                  </div>

                  <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
                    <Secao titulo="Hora do dia" descricao="Distribuição das ações pelas 24 horas (relógio de Brasília).">
                      <GraficoPorHora dados={d.porHora} />
                    </Secao>
                    <Secao titulo="Dia da semana" descricao="Verde: média nos dias em que trabalhou. Cinza: total do período.">
                      <GraficoDiaSemana dados={d.porDiaSemana} />
                    </Secao>
                  </div>
                </>
              )}
            </>
          );
        })()
      )}
    </div>
  );
}
