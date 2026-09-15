import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Select } from '@/shared/ui/Select';
import { useEquipe, useIndividual } from '../api/queries';
import { diaBr, dias, mediana, moeda, numero, variacao } from '../lib/formato';
import type { OperadorPeriodo, Periodo } from '../types';
import {
  Cartao,
  Carregando,
  GraficoDiaSemana,
  GraficoPorDia,
  GraficoPorMes,
  ListaTop,
  Secao,
} from './Blocos';

/** "mediana da equipe: 120,5" — referência para comparar a pessoa com o grupo. */
function ref(ranking: OperadorPeriodo[], campo: keyof OperadorPeriodo, formatar: (n: number) => string) {
  const m = mediana(ranking.map((o) => o[campo] as number | null));
  return m === null ? undefined : `mediana da equipe: ${formatar(m)}`;
}

/** Uma pessoa no período: os mesmos números da linha do ranking, abertos, e comparados com a equipe. */
export function IndividualAba({
  periodo,
  chave,
  aoEscolher,
}: {
  periodo: Periodo;
  chave: string | null;
  aoEscolher: (chave: string | null) => void;
}) {
  const equipe = useEquipe(periodo);
  const q = useIndividual(chave, periodo);
  const ranking = equipe.data?.ranking ?? [];
  const posicao = chave ? ranking.findIndex((o) => o.chave === chave) : -1;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end gap-3">
        <label className="min-w-72 flex-1 text-xs text-gray-600">
          Operador
          <Select value={chave ?? ''} onChange={(ev) => aoEscolher(ev.target.value || null)} className="mt-1">
            <option value="">Escolha um operador…</option>
            {ranking.map((o) => (
              <option key={o.chave} value={o.chave}>
                {o.nome} — {numero(o.autorizacoes)} autorizações
              </option>
            ))}
          </Select>
        </label>
        {equipe.data && ranking.length === 0 ? (
          <p className="text-xs text-gray-500">Ninguém da equipe autorizou neste período.</p>
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
          const totalEquipe = equipe.data?.atual.autorizacoes ?? 0;

          return (
            <>
              <header className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
                <h2 className="text-lg font-semibold text-gray-900">{d.nome}</h2>
                <p className="text-xs text-gray-500">
                  Login(s) no SISREG: {d.logins.join(', ')} · {diaBr(d.atual.de)} a {diaBr(d.atual.ate)}
                  {posicao >= 0 ? ` · ${posicao + 1}º de ${ranking.length} no ranking` : ''}
                  {m && totalEquipe ? ` · ${numero((100 * m.autorizacoes) / totalEquipe, 1)}% das autorizações da equipe` : ''}
                </p>
                {!d.usuarioId ? (
                  <p className="mt-1 text-[11px] text-gray-400">
                    Para mostrar o nome, associe o login ao usuário em Cadastros → Usuários (campo "Logins no SISREG").
                  </p>
                ) : null}
              </header>

              {!m ? (
                <p className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">
                  Nenhuma autorização neste período.
                </p>
              ) : (
                <>
                  <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
                    <Cartao
                      rotulo="Autorizações"
                      valor={numero(m.autorizacoes)}
                      variacao={variacao(m.autorizacoes, d.anterior.autorizacoes)}
                      referencia={ref(ranking, 'autorizacoes', (n) => numero(n))}
                      dica="Agendamentos autorizados no período."
                    />
                    <Cartao
                      rotulo="Dias trabalhados"
                      valor={`${numero(m.diasTrabalhados)} de ${numero(m.diasCorridos)}`}
                      variacao={variacao(m.diasTrabalhados, d.anterior.diasComAtividade)}
                      referencia={ref(ranking, 'diasTrabalhados', (n) => numero(n))}
                      dica="Dias com pelo menos uma autorização, de todos os dias do período."
                    />
                    <Cartao
                      rotulo="Média por dia trabalhado"
                      valor={numero(m.mediaDiaTrabalhado, 1)}
                      variacao={variacao(m.mediaDiaTrabalhado, d.anterior.mediaDiaComAtividade)}
                      referencia={ref(ranking, 'mediaDiaTrabalhado', (n) => numero(n, 1))}
                      dica="A produção de um dia de trabalho: autorizações ÷ dias trabalhados."
                    />
                    <Cartao
                      rotulo="Média por dia corrido"
                      valor={numero(m.mediaDiaCorrido, 1)}
                      variacao={variacao(m.mediaDiaCorrido, d.anterior.mediaDiaCorrido)}
                      referencia={ref(ranking, 'mediaDiaCorrido', (n) => numero(n, 1))}
                      dica="Autorizações ÷ todos os dias do período."
                    />
                    <Cartao
                      rotulo="Pico diário"
                      valor={numero(m.picoDiario)}
                      referencia={m.diaDoPico ? `em ${diaBr(m.diaDoPico)}` : undefined}
                      dica="Maior número de autorizações num único dia."
                    />
                    <Cartao
                      rotulo="Espera até autorizar"
                      valor={dias(m.esperaMedianaDias)}
                      referencia={ref(ranking, 'esperaMedianaDias', (n) => dias(n))}
                      dica={`Mediana de dias entre o pedido e a autorização. 90% até ${dias(m.esperaP90Dias)}.`}
                    />
                    <Cartao
                      rotulo="Antecedência"
                      valor={dias(m.antecedenciaMedianaDias)}
                      referencia={ref(ranking, 'antecedenciaMedianaDias', (n) => dias(n))}
                      dica="Mediana de dias entre a autorização e o dia do atendimento."
                    />
                    <Cartao
                      rotulo="Agenda nova"
                      valor={dias(m.agendaNovaMedianaDias)}
                      referencia={`${numero(m.agendaNovaAutorizacoes)} autorização(ões) em agenda nova`}
                      dica="Mediana de dias entre a abertura da escala no SISREG e a autorização (escalas de até 90 dias)."
                    />
                    <Cartao
                      rotulo="Amplitude"
                      valor={`${numero(m.procedimentos)} proc.`}
                      referencia={`${numero(m.unidadesExecutantes)} executante(s) · ${numero(m.unidadesSolicitantes)} solicitante(s)`}
                      dica="Procedimentos e unidades diferentes no trabalho do período."
                    />
                    <Cartao
                      rotulo="Valor regulado"
                      valor={moeda(m.valorRegulado)}
                      variacao={variacao(m.valorRegulado, d.anterior.valorRegulado)}
                      referencia={`${numero(m.percentualFimDeSemana, 1)}% no fim de semana · ${numero(m.percentualProprioPedido, 1)}% pedido próprio`}
                      dica="Soma do valor dos procedimentos autorizados (tabela do SISREG)."
                    />
                  </div>

                  <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
                    <Secao titulo="Dia a dia" descricao="Dias sem barra são dias sem autorização.">
                      <GraficoPorDia dados={d.porDia} mostrarOperadores={false} />
                    </Secao>
                    <Secao titulo="Mês a mês">
                      <GraficoPorMes dados={d.porMes} mostrarOperadores={false} />
                    </Secao>
                  </div>

                  <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-3">
                    <Secao titulo="Procedimentos mais autorizados">
                      <ListaTop itens={d.topProcedimentos} />
                    </Secao>
                    <Secao titulo="Unidades executantes">
                      <ListaTop itens={d.topUnidadesExecutantes} />
                    </Secao>
                    <Secao titulo="Unidades solicitantes">
                      <ListaTop itens={d.topUnidadesSolicitantes} />
                    </Secao>
                  </div>

                  <Secao titulo="Dia da semana" descricao="Verde: média nos dias em que trabalhou. Cinza: total do período.">
                    <GraficoDiaSemana dados={d.porDiaSemana} />
                  </Secao>
                </>
              )}
            </>
          );
        })()
      )}
    </div>
  );
}
