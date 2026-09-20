import { useMemo, useState } from 'react';
import { ArrowDown, ArrowUp } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { diaBr, dias, numero, variacao } from '@/shared/lib/estatisticasPeriodo';
import { useEquipe } from '../api/queries';
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

type Coluna = {
  id: keyof OperadorExternoPeriodo;
  rotulo: string;
  dica: string;
  formato: (o: OperadorExternoPeriodo) => string;
  /** Tempos: menor é melhor — a seta de ordenação começa crescente. */
  crescente?: boolean;
};

const COLUNAS: Coluna[] = [
  { id: 'acoes', rotulo: 'Ações', dica: 'Todos os eventos assinados no período (qualquer verbo).', formato: (o) => numero(o.acoes) },
  { id: 'agendamentos', rotulo: 'Agend.', dica: 'Agendamentos e reagendamentos.', formato: (o) => numero(o.agendamentos) },
  { id: 'cancelamentos', rotulo: 'Cancel.', dica: 'Cancelamentos.', formato: (o) => numero(o.cancelamentos) },
  { id: 'followUps', rotulo: 'FollowUP', dica: 'Registros de FollowUP (tentativa de contato, cobrança, orientação).', formato: (o) => numero(o.followUps) },
  { id: 'pendencias', rotulo: 'Pend.', dica: 'Solicitações pendenciadas.', formato: (o) => numero(o.pendencias) },
  { id: 'solicitacoes', rotulo: 'Solic.', dica: 'Solicitações diferentes em que mexeu.', formato: (o) => numero(o.solicitacoes) },
  { id: 'diasTrabalhados', rotulo: 'Dias trab.', dica: 'Dias com pelo menos uma ação.', formato: (o) => numero(o.diasTrabalhados) },
  {
    id: 'mediaDiaTrabalhado',
    rotulo: 'Média/dia trab.',
    dica: 'Ações ÷ dias trabalhados — a produção de um dia de trabalho.',
    formato: (o) => numero(o.mediaDiaTrabalhado, 1),
  },
  {
    id: 'mediaDiaCorrido',
    rotulo: 'Média/dia corrido',
    dica: 'Ações ÷ dias do período — cai para quem não trabalha todo dia.',
    formato: (o) => numero(o.mediaDiaCorrido, 1),
  },
  { id: 'picoDiario', rotulo: 'Pico', dica: 'Maior número de ações num único dia.', formato: (o) => numero(o.picoDiario) },
  {
    id: 'esperaMedianaDias',
    rotulo: 'Espera p50',
    dica: 'Mediana de dias entre a data da solicitação e o agendamento feito por esta pessoa.',
    formato: (o) => dias(o.esperaMedianaDias),
    crescente: true,
  },
  {
    id: 'horaInicioMediana',
    rotulo: 'Começa',
    dica: 'Mediana da hora da primeira ação de cada dia trabalhado.',
    formato: (o) => hora(o.horaInicioMediana),
    crescente: true,
  },
  {
    id: 'horaFimMediana',
    rotulo: 'Termina',
    dica: 'Mediana da hora da última ação de cada dia trabalhado.',
    formato: (o) => hora(o.horaFimMediana),
  },
  { id: 'recursos', rotulo: 'Recursos', dica: 'Recursos (procedimentos) diferentes tocados.', formato: (o) => numero(o.recursos) },
  { id: 'unidadesExecutoras', rotulo: 'Unid. exec.', dica: 'Unidades executoras diferentes.', formato: (o) => numero(o.unidadesExecutoras) },
  { id: 'percentualFimDeSemana', rotulo: '% fds', dica: 'Parte das ações feita no sábado ou domingo.', formato: (o) => `${numero(o.percentualFimDeSemana, 1)}%` },
];

function Ranking({
  linhas,
  total,
  aoAbrir,
}: {
  linhas: OperadorExternoPeriodo[];
  total: number;
  aoAbrir: (chave: string) => void;
}) {
  const [ordem, setOrdem] = useState<{ id: keyof OperadorExternoPeriodo; crescente: boolean }>({
    id: 'acoes',
    crescente: false,
  });

  const ordenadas = useMemo(() => {
    const valor = (o: OperadorExternoPeriodo) => {
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
      <table className="w-full min-w-[80rem] text-left text-xs">
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
            <th className="py-1 text-right font-medium" title="Parte das ações da equipe">
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
                {o.lotacoes.length ? <p className="max-w-64 truncate text-[10px] text-gray-500" title={o.lotacoes.join(' · ')}>{o.lotacoes.join(' · ')}</p> : null}
              </td>
              {COLUNAS.map((c) => (
                <td key={c.id} className="py-2 pr-3 text-right tabular-nums text-gray-700">
                  {c.formato(o)}
                </td>
              ))}
              <td className="py-2 text-right tabular-nums text-gray-700">
                {total ? `${numero((100 * o.acoes) / total, 1)}%` : '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** A equipe inteira (nomes habilitados) no período, com o período anterior de mesmo tamanho. */
export function EquipeAba({
  fonte,
  periodo,
  aoAbrirOperador,
}: {
  fonte: FonteExterna;
  periodo: Periodo;
  aoAbrirOperador: (chave: string) => void;
}) {
  const q = useEquipe(fonte, periodo);

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
  const sigla = FONTES[fonte].sigla;

  if (e.habilitados === 0) {
    return (
      <div className="rounded-xl border border-dashed border-amber-300 bg-amber-50 p-6 text-sm text-amber-900">
        Nenhum operador está habilitado nas estatísticas do {sigla}. Abra a aba <strong>Configuração</strong> e
        marque os nomes da regulação que devem entrar.
      </div>
    );
  }

  const cobertura = e.acoesTodosOsOperadores ? (100 * a.acoes) / e.acoesTodosOsOperadores : 0;
  const topDias = [...e.porDia].sort((x, y) => y.acoes - x.acoes).slice(0, 5);

  return (
    <div className="space-y-5">
      <p className="text-xs text-gray-500">
        {diaBr(a.de)} a {diaBr(a.ate)} ({numero(e.diasCorridos)} dias), comparado com {diaBr(ant.de)} a{' '}
        {diaBr(ant.ate)}. {numero(e.habilitados)} nome(s) habilitado(s). A equipe configurada responde por{' '}
        <strong>{numero(cobertura, 1)}%</strong> de todas as ações do período ({numero(e.acoesTodosOsOperadores)} no
        total, contando quem não está marcado).
      </p>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
        <Cartao
          rotulo="Ações"
          valor={numero(a.acoes)}
          variacao={variacao(a.acoes, ant.acoes)}
          dica={`Todos os eventos assinados pela equipe no ${sigla} (qualquer verbo).`}
        />
        <Cartao
          rotulo="Agendamentos"
          valor={numero(a.agendamentos)}
          variacao={variacao(a.agendamentos, ant.agendamentos)}
          dica="Agendamentos e reagendamentos feitos pela equipe."
        />
        <Cartao
          rotulo="Cancelamentos"
          valor={numero(a.cancelamentos)}
          variacao={variacao(a.cancelamentos, ant.cancelamentos)}
          referencia={`${numero(a.pendencias)} pendência(s)`}
          dica="Cancelamentos registrados pela equipe."
        />
        <Cartao
          rotulo="FollowUPs"
          valor={numero(a.followUps)}
          variacao={variacao(a.followUps, ant.followUps)}
          dica="Tentativas de contato, cobranças e orientações registradas."
        />
        <Cartao
          rotulo="Solicitações tocadas"
          valor={numero(a.solicitacoes)}
          variacao={variacao(a.solicitacoes, ant.solicitacoes)}
          dica="Solicitações diferentes em que alguém da equipe mexeu."
        />
        <Cartao
          rotulo="Média por dia corrido"
          valor={numero(a.mediaDiaCorrido, 1)}
          variacao={variacao(a.mediaDiaCorrido, ant.mediaDiaCorrido)}
          dica="Ações ÷ dias do período, com fins de semana e feriados."
        />
        <Cartao
          rotulo="Média por dia com atividade"
          valor={numero(a.mediaDiaComAtividade, 1)}
          variacao={variacao(a.mediaDiaComAtividade, ant.mediaDiaComAtividade)}
          referencia={`${numero(a.diasComAtividade)} dia(s) com atividade`}
          dica="Ações ÷ dias em que alguém da equipe agiu."
        />
        <Cartao
          rotulo="Pessoas ativas"
          valor={numero(a.operadores)}
          variacao={variacao(a.operadores, ant.operadores)}
          referencia={`${numero(e.mediaOperadoresPorDiaUtil, 1)} por dia útil, em média`}
          dica="Nomes com pelo menos uma ação no período."
        />
        <Cartao
          rotulo="Dependência (3 maiores)"
          valor={`${numero(e.concentracaoTop3Percentual, 1)}%`}
          classe={e.concentracaoTop3Percentual >= 50 ? 'text-amber-700' : 'text-gray-900'}
          dica="Quanto das ações vem das 3 pessoas mais ativas. Alto = a fila para se elas pararem."
        />
        <Cartao
          rotulo="Maior dia"
          valor={e.pico ? numero(e.pico.acoes) : '—'}
          referencia={e.pico ? `${diaBr(e.pico.dia)} · ${numero(e.pico.operadores)} pessoa(s)` : undefined}
          dica="O dia de maior volume da equipe no período."
        />
        <Cartao
          rotulo="Espera até agendar"
          valor={dias(a.esperaMedianaDias)}
          dica="Mediana de dias entre a data da solicitação e o agendamento."
        />
        <Cartao
          rotulo="Fim de semana"
          valor={`${numero(a.percentualFimDeSemana, 1)}%`}
          dica="Parte das ações feita no sábado ou no domingo."
        />
        <Cartao
          rotulo="Fora do expediente"
          valor={`${numero(a.percentualForaDoExpediente, 1)}%`}
          dica="Parte das ações antes das 7h ou a partir das 19h (relógio de Brasília)."
        />
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Secao titulo="Dia a dia" descricao="Barras: ações (agendamentos em destaque). Linha: quantas pessoas agiram naquele dia.">
          <GraficoPorDia dados={e.porDia} />
        </Secao>
        <Secao titulo="Mês a mês" descricao="Mês incompleto nas pontas do período aparece menor — compare a linha de pessoas.">
          <GraficoPorMes dados={e.porMes} />
        </Secao>
      </div>

      <Secao
        titulo="Ranking dos operadores"
        descricao={`Clique no nome para abrir o detalhe individual. Clique no cabeçalho para ordenar. O nome é como o ${sigla} grava; a lotação aparece embaixo.`}
      >
        <Ranking linhas={e.ranking} total={a.acoes} aoAbrir={aoAbrirOperador} />
      </Secao>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-4">
        <Secao titulo="Ações por tipo" descricao="O que a equipe faz, por verbo.">
          <ListaTop itens={e.porTipoEvento} />
        </Secao>
        <Secao titulo="Recursos mais tocados">
          <ListaTop itens={e.topRecursos} />
        </Secao>
        <Secao titulo="Unidades executoras">
          <ListaTop itens={e.topUnidadesExecutoras} />
        </Secao>
        <Secao titulo="Lotações" descricao="Como cada ação foi assinada.">
          <ListaTop itens={e.topLotacoes} />
        </Secao>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Secao titulo="Hora do dia" descricao="Distribuição das ações pelas 24 horas (relógio de Brasília).">
          <GraficoPorHora dados={e.porHora} />
        </Secao>
        <Secao titulo="Dia da semana" descricao="Verde: média por dia em que houve atividade. Cinza: total do período.">
          <GraficoDiaSemana dados={e.porDiaSemana} />
        </Secao>
      </div>

      <Secao titulo="Maiores dias" descricao="Os 5 dias de maior volume.">
        <ListaTop
          itens={topDias.map((d) => ({
            rotulo: `${diaBr(d.dia)} · ${numero(d.operadores)} pessoa(s) · ${numero(d.agendamentos)} agendamento(s)`,
            acoes: d.acoes,
            operadores: d.operadores,
          }))}
        />
      </Secao>
    </div>
  );
}
