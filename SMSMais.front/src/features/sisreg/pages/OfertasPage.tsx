import { useState } from 'react';
import { CalendarPlus, Clock, Loader2, PackageOpen, Sparkles, Timer } from 'lucide-react';
import { useOfertas } from '../api/queries';
import { FilaDaOfertaPainel } from '../components/FilaDaOfertaPainel';
import type { AgendaNova, VagaLiberada } from '../types';

const DIAS_SEMANA = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'];

function dataCurta(iso: string) {
  const [a, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${a.slice(2)}`;
}

function dataHora(iso: string) {
  const d = new Date(iso);
  return `${d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })} ${d.toLocaleTimeString(
    'pt-BR',
    { hour: '2-digit', minute: '2-digit' },
  )}`;
}

/** Dias inteiros até a data, a partir de hoje. Negativo = já passou. */
function diasAte(iso: string) {
  const alvo = new Date(iso);
  const hoje = new Date();
  return Math.ceil((alvo.getTime() - hoje.getTime()) / 86_400_000);
}

/**
 * A espera é o que ordena a tela — "4 vagas" é burocracia, "4 vagas numa fila de 466 dias" faz
 * alguém agir. Cor por faixa, e o número sempre visível.
 */
function Espera({ dias, urgente }: { dias: number | null; urgente: number }) {
  if (dias === null) {
    return <span className="text-xs text-gray-400">sem histórico</span>;
  }
  const cor =
    dias >= urgente
      ? 'bg-red-50 text-red-700 ring-red-200'
      : dias >= 90
        ? 'bg-amber-50 text-amber-700 ring-amber-200'
        : 'bg-gray-50 text-gray-600 ring-gray-200';
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs ring-1 ${cor}`}>
      <Timer className="h-3 w-3" />
      fila {dias}d
    </span>
  );
}

function CartaoAgenda({
  a,
  urgente,
  aoAbrir,
}: {
  a: AgendaNova;
  urgente: number;
  aoAbrir: (procedimento: string) => void;
}) {
  const destaque = (a.esperaMedianaDias ?? 0) >= urgente;
  const recorrente = a.blocos > 1;
  return (
    <li
      role="button"
      tabIndex={0}
      onClick={() => aoAbrir(a.procedimentoNome)}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          aoAbrir(a.procedimentoNome);
        }
      }}
      title="Ver quem espera por este procedimento"
      className={`cursor-pointer rounded-lg border p-3 transition hover:shadow-sm ${
        destaque
          ? 'border-red-200 bg-red-50/40 hover:border-red-300'
          : 'border-gray-200 bg-white hover:border-primary-300'
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-gray-900">{a.procedimentoNome}</p>
          <p className="truncate text-xs text-gray-600">
            {a.unidadeNome}
            {a.cboDescricao ? ` · ${a.cboDescricao}` : ''}
          </p>
        </div>
        <div className="shrink-0 text-right">
          <p className="text-lg font-semibold leading-none text-primary-700">{a.vagas}</p>
          <p className="text-[11px] text-gray-500">vagas</p>
        </div>
      </div>

      <div className="mt-2 flex flex-wrap items-center gap-2">
        <Espera dias={a.esperaMedianaDias} urgente={urgente} />
        <span className="text-xs text-gray-500">
          {dataCurta(a.vigenciaInicio)} a {dataCurta(a.vigenciaFim)}
        </span>
        {recorrente ? (
          <span className="rounded bg-gray-100 px-1.5 py-0.5 text-[11px] text-gray-600">
            {[...a.diasSemana].sort((x, y) => x - y).map((d) => DIAS_SEMANA[d]).join(', ')}
          </span>
        ) : (
          <span className="rounded bg-gray-100 px-1.5 py-0.5 text-[11px] text-gray-600">dia único</span>
        )}
        <span className="text-[11px] text-gray-400">vista {dataHora(a.vistaEm)}</span>
      </div>
    </li>
  );
}

function CartaoVaga({
  v,
  urgente,
  perecivel,
  aoAbrir,
}: {
  v: VagaLiberada;
  urgente: number;
  perecivel: number;
  aoAbrir: (procedimento: string) => void;
}) {
  const dias = diasAte(v.dataAgendada);
  const corre = dias <= perecivel;
  const clicavel = Boolean(v.procedimentoNome);
  return (
    <li
      role={clicavel ? 'button' : undefined}
      tabIndex={clicavel ? 0 : undefined}
      onClick={() => clicavel && aoAbrir(v.procedimentoNome!)}
      onKeyDown={(e) => {
        if (clicavel && (e.key === 'Enter' || e.key === ' ')) {
          e.preventDefault();
          aoAbrir(v.procedimentoNome!);
        }
      }}
      title={clicavel ? 'Ver quem espera por este procedimento' : undefined}
      className={`rounded-lg border p-3 transition ${clicavel ? 'cursor-pointer hover:shadow-sm' : ''} ${
        corre ? 'border-amber-300 bg-amber-50/50' : 'border-gray-200 bg-white hover:border-primary-300'
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-gray-900">
            {v.procedimentoNome ?? 'Procedimento não identificado'}
          </p>
          <p className="truncate text-xs text-gray-600">{v.unidadeNome ?? '—'}</p>
        </div>
        <div className="shrink-0 text-right">
          <p
            className={`text-sm font-semibold leading-none ${corre ? 'text-amber-700' : 'text-gray-700'}`}
          >
            {dataHora(v.dataAgendada)}
          </p>
          <p className="text-[11px] text-gray-500">
            {dias <= 0 ? 'hoje' : `em ${dias} dia${dias > 1 ? 's' : ''}`}
          </p>
        </div>
      </div>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <Espera dias={v.esperaMedianaDias} urgente={urgente} />
        <span className="text-[11px] text-gray-400">detectada {dataHora(v.detectadaEm)}</span>
      </div>
    </li>
  );
}

/**
 * Tela de OFERTAS — o que abriu no SISREG.
 *
 * <p>Duas colunas porque são dois fatos com urgências diferentes: <b>agenda nova</b> (um bloco que
 * passou a existir) e <b>vaga liberada</b> (alguém cancelou lá e o horário voltou). A segunda é
 * perecível: uma vaga para depois de amanhã morre se ninguém agir hoje.</p>
 *
 * <p>A tela é deliberadamente de LEITURA. Ela responde "o que abriu e o que é urgente"; agir ainda
 * é no SISREG, porque a fila de quem espera não é lida por nós — e prometer um botão "chamar o
 * próximo" sem saber quem é o próximo seria mentira.</p>
 */
export function OfertasPage() {
  const [dias, setDias] = useState(7);
  /** Procedimento cuja fila está aberta; null = mostrando as ofertas. */
  const [filaAberta, setFilaAberta] = useState<string | null>(null);
  const { data, isLoading, isError } = useOfertas(dias);

  const urgente = data?.diasEsperaUrgente ?? 180;
  const perecivel = data?.diasVagaPerecivel ?? 7;
  const agendas = data?.agendasNovas ?? [];
  const vagas = data?.vagasLiberadas ?? [];
  const totalVagasNovas = agendas.reduce((s, a) => s + a.vagas, 0);
  const correndo = vagas.filter((v) => diasAte(v.dataAgendada) <= perecivel).length;

  return (
    <div className="mx-auto max-w-6xl px-4 py-6">
      <header className="mb-5">
        <h1 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
          <Sparkles className="h-5 w-5 text-primary-600" />
          Ofertas
        </h1>
        <p className="mt-1 max-w-3xl text-sm text-gray-600">
          O que abriu no SISREG: agendas que passaram a existir e horários que vagaram por
          cancelamento. Ordenado pela espera do procedimento — quanto maior a fila, mais alto.
          <strong> Clique numa oferta para ver quem está esperando.</strong>
        </p>
      </header>

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <label className="text-sm text-gray-700">
          Agendas vistas nos últimos{' '}
          <select
            className="rounded-md border border-gray-300 px-2 py-1 text-sm"
            value={dias}
            onChange={(e) => setDias(Number(e.target.value))}
          >
            <option value={1}>1 dia</option>
            <option value={3}>3 dias</option>
            <option value={7}>7 dias</option>
            <option value={15}>15 dias</option>
            <option value={30}>30 dias</option>
          </select>
        </label>
        {isLoading ? <Loader2 className="h-4 w-4 animate-spin text-gray-400" /> : null}
      </div>

      {isError ? (
        <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">
          Não foi possível carregar as ofertas.
        </p>
      ) : null}

      {filaAberta ? (
        <FilaDaOfertaPainel procedimento={filaAberta} aoFechar={() => setFilaAberta(null)} />
      ) : (
      <div className="grid gap-6 lg:grid-cols-2">
        <section>
          <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold text-gray-800">
            <CalendarPlus className="h-4 w-4 text-emerald-600" />
            Abriu agenda
            {agendas.length > 0 ? (
              <span className="text-xs font-normal text-gray-500">
                {agendas.length} agenda(s) · {totalVagasNovas} vagas
              </span>
            ) : null}
          </h2>
          {agendas.length === 0 && !isLoading ? (
            <p className="rounded-lg border border-dashed border-gray-200 p-6 text-center text-sm text-gray-500">
              Nenhuma agenda nova nesta janela.
            </p>
          ) : (
            <ul className="space-y-2">
              {agendas.map((a) => (
                <CartaoAgenda
                  key={`${a.unidadeId}-${a.procedimentoCodigo}`}
                  a={a}
                  urgente={urgente}
                  aoAbrir={setFilaAberta}
                />
              ))}
            </ul>
          )}
        </section>

        <section>
          <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold text-gray-800">
            <PackageOpen className="h-4 w-4 text-amber-600" />
            Vagou
            {vagas.length > 0 ? (
              <span className="text-xs font-normal text-gray-500">
                {vagas.length} horário(s)
                {correndo > 0 ? ` · ${correndo} nos próximos ${perecivel} dias` : ''}
              </span>
            ) : null}
          </h2>
          {vagas.length === 0 && !isLoading ? (
            <p className="rounded-lg border border-dashed border-gray-200 p-6 text-center text-sm text-gray-500">
              Nenhum horário liberado à espera.
            </p>
          ) : (
            <ul className="space-y-2">
              {vagas.map((v) => (
                <CartaoVaga
                  key={v.alteracaoId}
                  v={v}
                  urgente={urgente}
                  perecivel={perecivel}
                  aoAbrir={setFilaAberta}
                />
              ))}
            </ul>
          )}
        </section>
      </div>
      )}

      <footer className="mt-6 flex items-start gap-2 rounded-md bg-gray-50 p-3 text-xs text-gray-600">
        <Clock className="mt-0.5 h-3.5 w-3.5 shrink-0 text-gray-400" />
        <p>
          <strong>Como ler a fila.</strong> É a espera mediana de quem <em>já conseguiu data</em>{' '}
          neste procedimento neste ano, não a de quem está esperando agora — a fila do SISREG sem
          data marcada não é lida por nós, então o número real é maior. Serve para priorizar entre
          procedimentos, não para prometer prazo. <strong>“Vagou”</strong> quer dizer que o SISREG
          parou de mostrar aquele agendamento; confirmar se a vaga está livre é trabalho de gente.
        </p>
      </footer>
    </div>
  );
}
