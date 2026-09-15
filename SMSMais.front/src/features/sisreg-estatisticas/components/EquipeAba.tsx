import { useMemo, useState } from 'react';
import { ArrowDown, ArrowUp } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useEquipe } from '../api/queries';
import { diaBr, dias, moeda, numero, variacao } from '../lib/formato';
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

type Coluna = {
  id: keyof OperadorPeriodo;
  rotulo: string;
  dica: string;
  formato: (o: OperadorPeriodo) => string;
  /** Tempos: menor é melhor — a seta de ordenação começa crescente. */
  crescente?: boolean;
};

const COLUNAS: Coluna[] = [
  { id: 'autorizacoes', rotulo: 'Autorizações', dica: 'Agendamentos autorizados no período.', formato: (o) => numero(o.autorizacoes) },
  { id: 'diasTrabalhados', rotulo: 'Dias trab.', dica: 'Dias com pelo menos uma autorização.', formato: (o) => numero(o.diasTrabalhados) },
  {
    id: 'mediaDiaTrabalhado',
    rotulo: 'Média/dia trab.',
    dica: 'Autorizações ÷ dias trabalhados — a produção de um dia de trabalho.',
    formato: (o) => numero(o.mediaDiaTrabalhado, 1),
  },
  {
    id: 'mediaDiaCorrido',
    rotulo: 'Média/dia corrido',
    dica: 'Autorizações ÷ dias do período — cai para quem não trabalha todo dia.',
    formato: (o) => numero(o.mediaDiaCorrido, 1),
  },
  { id: 'picoDiario', rotulo: 'Pico', dica: 'Maior número de autorizações num único dia.', formato: (o) => numero(o.picoDiario) },
  {
    id: 'esperaMedianaDias',
    rotulo: 'Espera p50',
    dica: 'Mediana de dias entre o pedido e a autorização. Alto = pega pedido antigo da fila.',
    formato: (o) => dias(o.esperaMedianaDias),
    crescente: true,
  },
  {
    id: 'antecedenciaMedianaDias',
    rotulo: 'Antecedência',
    dica: 'Mediana de dias entre a autorização e o atendimento.',
    formato: (o) => dias(o.antecedenciaMedianaDias),
  },
  {
    id: 'agendaNovaMedianaDias',
    rotulo: 'Agenda nova',
    dica: 'Mediana de dias entre a abertura da escala no SISREG e a autorização (escalas de até 90 dias).',
    formato: (o) => dias(o.agendaNovaMedianaDias),
    crescente: true,
  },
  { id: 'procedimentos', rotulo: 'Proced.', dica: 'Procedimentos diferentes autorizados.', formato: (o) => numero(o.procedimentos) },
  { id: 'unidadesExecutantes', rotulo: 'Unid. exec.', dica: 'Unidades executantes diferentes.', formato: (o) => numero(o.unidadesExecutantes) },
  { id: 'percentualFimDeSemana', rotulo: '% fds', dica: 'Parte das autorizações feita no sábado ou domingo.', formato: (o) => `${numero(o.percentualFimDeSemana, 1)}%` },
  {
    id: 'percentualProprioPedido',
    rotulo: '% próprio',
    dica: 'Autorizações de pedidos feitos pelo mesmo login. Alto = login de unidade marcando a própria agenda, não regulação.',
    formato: (o) => `${numero(o.percentualProprioPedido, 1)}%`,
  },
  { id: 'valorRegulado', rotulo: 'Valor', dica: 'Soma do valor dos procedimentos autorizados (tabela do SISREG).', formato: (o) => moeda(o.valorRegulado) },
];

function Ranking({
  linhas,
  total,
  aoAbrir,
}: {
  linhas: OperadorPeriodo[];
  total: number;
  aoAbrir: (chave: string) => void;
}) {
  const [ordem, setOrdem] = useState<{ id: keyof OperadorPeriodo; crescente: boolean }>({
    id: 'autorizacoes',
    crescente: false,
  });

  const ordenadas = useMemo(() => {
    const valor = (o: OperadorPeriodo) => {
      const v = o[ordem.id];
      return typeof v === 'number' ? v : v === null ? Number.POSITIVE_INFINITY : String(v);
    };
    return [...linhas].sort((a, b) => {
      const va = valor(a);
      const vb = valor(b);
      const c = va < vb ? -1 : va > vb ? 1 : 0;
      return ordem.crescente ? c : -c;
    });
  }, [linhas, ordem]);

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[72rem] text-left text-xs">
        <thead className="text-gray-500">
          <tr>
            <th className="py-1 pr-2 font-medium">#</th>
            <th className="py-1 pr-3 font-medium">Operador</th>
            {COLUNAS.map((c) => (
              <th key={c.id} className="py-1 pr-3 text-right font-medium" title={c.dica}>
                <button
                  type="button"
                  className="inline-flex items-center gap-0.5 hover:text-gray-900"
                  onClick={() =>
                    setOrdem((o) => (o.id === c.id ? { id: c.id, crescente: !o.crescente } : { id: c.id, crescente: !!c.crescente }))
                  }
                >
                  {c.rotulo}
                  {ordem.id === c.id ? (ordem.crescente ? <ArrowUp className="h-3 w-3" /> : <ArrowDown className="h-3 w-3" />) : null}
                </button>
              </th>
            ))}
            <th className="py-1 text-right font-medium" title="Parte das autorizações da equipe">
              % equipe
            </th>
          </tr>
        </thead>
        <tbody>
          {ordenadas.map((o, i) => (
            <tr
              key={o.chave}
              onClick={() => aoAbrir(o.chave)}
              className="cursor-pointer border-t border-gray-100 hover:bg-gray-50"
              title="Ver o detalhe individual"
            >
              <td className="py-2 pr-2 text-gray-400">{i + 1}</td>
              <td className="py-2 pr-3">
                <p className="font-medium text-gray-900">{o.nome}</p>
                {o.usuarioId ? <p className="text-[10px] text-gray-500">{o.logins.join(' · ')}</p> : null}
              </td>
              {COLUNAS.map((c) => (
                <td key={c.id} className="py-2 pr-3 text-right tabular-nums text-gray-700">
                  {c.formato(o)}
                </td>
              ))}
              <td className="py-2 text-right tabular-nums text-gray-700">
                {total ? `${numero((100 * o.autorizacoes) / total, 1)}%` : '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** A equipe inteira (logins habilitados) no período, com o período anterior de mesmo tamanho. */
export function EquipeAba({ periodo, aoAbrirOperador }: { periodo: Periodo; aoAbrirOperador: (chave: string) => void }) {
  const q = useEquipe(periodo);

  if (q.isPending) return <Carregando />;
  if (q.isError) {
    return (
      <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(q.error)}
      </p>
    );
  }

  const e = q.data;
  const a = e.atual;
  const ant = e.anterior;

  if (e.habilitados === 0) {
    return (
      <div className="rounded-xl border border-dashed border-amber-300 bg-amber-50 p-6 text-sm text-amber-900">
        Nenhum operador está habilitado nas estatísticas. Abra a aba <strong>Configuração</strong> e marque os
        logins da regulação que devem entrar.
      </div>
    );
  }

  const cobertura = e.autorizacoesTodosOsLogins ? (100 * a.autorizacoes) / e.autorizacoesTodosOsLogins : 0;
  const topDias = [...e.porDia].sort((x, y) => y.autorizacoes - x.autorizacoes).slice(0, 5);

  return (
    <div className="space-y-5">
      <p className="text-xs text-gray-500">
        {diaBr(a.de)} a {diaBr(a.ate)} ({numero(e.diasCorridos)} dias), comparado com {diaBr(ant.de)} a{' '}
        {diaBr(ant.ate)}. {numero(e.habilitados)} login(s) habilitado(s). A equipe configurada responde por{' '}
        <strong>{numero(cobertura, 1)}%</strong> de todas as autorizações do período (
        {numero(e.autorizacoesTodosOsLogins)} no total, contando logins de unidade).
      </p>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
        <Cartao
          rotulo="Autorizações"
          valor={numero(a.autorizacoes)}
          variacao={variacao(a.autorizacoes, ant.autorizacoes)}
          dica="Agendamentos autorizados pela equipe no período."
        />
        <Cartao
          rotulo="Valor regulado"
          valor={moeda(a.valorRegulado)}
          variacao={variacao(a.valorRegulado, ant.valorRegulado)}
          dica="Soma do valor dos procedimentos autorizados (tabela do SISREG)."
        />
        <Cartao
          rotulo="Média por dia corrido"
          valor={numero(a.mediaDiaCorrido, 1)}
          variacao={variacao(a.mediaDiaCorrido, ant.mediaDiaCorrido)}
          dica="Autorizações ÷ dias do período, com fins de semana e feriados."
        />
        <Cartao
          rotulo="Média por dia com atividade"
          valor={numero(a.mediaDiaComAtividade, 1)}
          variacao={variacao(a.mediaDiaComAtividade, ant.mediaDiaComAtividade)}
          referencia={`${numero(a.diasComAtividade)} dia(s) com atividade`}
          dica="Autorizações ÷ dias em que alguém da equipe autorizou."
        />
        <Cartao
          rotulo="Pessoas ativas"
          valor={numero(a.operadores)}
          variacao={variacao(a.operadores, ant.operadores)}
          referencia={`${numero(e.mediaOperadoresPorDiaUtil, 1)} por dia útil, em média`}
          dica="Pessoas (ou logins sem pessoa associada) com pelo menos uma autorização."
        />
        <Cartao
          rotulo="Dependência (3 maiores)"
          valor={`${numero(e.concentracaoTop3Percentual, 1)}%`}
          classe={e.concentracaoTop3Percentual >= 50 ? 'text-amber-700' : 'text-gray-900'}
          dica="Quanto das autorizações vem das 3 pessoas que mais autorizam. Alto = a regulação para se elas pararem."
        />
        <Cartao
          rotulo="Maior dia"
          valor={e.pico ? numero(e.pico.autorizacoes) : '—'}
          referencia={e.pico ? `${diaBr(e.pico.dia)} · ${numero(e.pico.operadores)} pessoa(s)` : undefined}
          dica="O dia de maior volume da equipe no período."
        />
        <Cartao
          rotulo="Espera até autorizar"
          valor={dias(a.esperaMedianaDias)}
          dica="Mediana de dias entre o pedido e a autorização."
        />
        <Cartao
          rotulo="Antecedência"
          valor={dias(a.antecedenciaMedianaDias)}
          dica="Mediana de dias entre a autorização e o dia do atendimento."
        />
        <Cartao
          rotulo="Fim de semana"
          valor={`${numero(a.percentualFimDeSemana, 1)}%`}
          dica="Parte das autorizações feita no sábado ou no domingo."
        />
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Secao titulo="Dia a dia" descricao="Barras: autorizações. Linha: quantas pessoas autorizaram naquele dia.">
          <GraficoPorDia dados={e.porDia} />
        </Secao>
        <Secao titulo="Mês a mês" descricao="Mês incompleto nas pontas do período aparece menor — compare a linha de pessoas.">
          <GraficoPorMes dados={e.porMes} />
        </Secao>
      </div>

      <Secao
        titulo="Ranking dos operadores"
        descricao="Clique no nome para abrir o detalhe individual. Clique no cabeçalho para ordenar. Tempos em dias: a autorização no SISREG não tem hora."
      >
        <Ranking linhas={e.ranking} total={a.autorizacoes} aoAbrir={aoAbrirOperador} />
      </Secao>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-4">
        <Secao titulo="Procedimentos mais autorizados">
          <ListaTop itens={e.topProcedimentos} />
        </Secao>
        <Secao titulo="Unidades executantes">
          <ListaTop itens={e.topUnidadesExecutantes} />
        </Secao>
        <Secao titulo="Unidades solicitantes">
          <ListaTop itens={e.topUnidadesSolicitantes} />
        </Secao>
        <Secao titulo="Maiores dias" descricao="Os 5 dias de maior volume.">
          <ListaTop
            itens={topDias.map((d) => ({
              rotulo: `${diaBr(d.dia)} · ${numero(d.operadores)} pessoa(s)`,
              autorizacoes: d.autorizacoes,
              valorRegulado: d.valorRegulado,
              operadores: d.operadores,
            }))}
          />
        </Secao>
      </div>

      <Secao
        titulo="Dia da semana"
        descricao="Verde: média por dia em que houve atividade. Cinza: total do período."
      >
        <GraficoDiaSemana dados={e.porDiaSemana} />
      </Secao>
    </div>
  );
}
