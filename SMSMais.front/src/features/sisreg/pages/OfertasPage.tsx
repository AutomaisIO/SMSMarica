import { useState } from 'react';
import { ArrowLeft, CalendarPlus, Clock, Home, Loader2, PackageOpen, Sparkles, Timer, Users } from 'lucide-react';
import { useOfertas } from '../api/queries';
import { DatasDaOfertaPainel } from '../components/DatasDaOfertaPainel';
import { FilaDaOfertaPainel } from '../components/FilaDaOfertaPainel';
import type { AgendaNova, VagaLiberada } from '../types';

const DIAS_SEMANA = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'];

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

/** "12/11" — dia e mês bastam no cartão. */
function diaMes(iso: string) {
  const [, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}`;
}

/** O procedimento aberto: código para as datas, nome para a fila. */
type Aberta = { codigo: string | null; nome: string };

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
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs ring-1 ${cor}`}
      title="Espera mediana de quem já conseguiu data neste procedimento, neste ano"
    >
      <Timer className="h-3 w-3" />
      espera {dias}d
    </span>
  );
}

/**
 * Quantas pessoas esperam AGORA — o que faltava ao lado da espera: "4 vagas numa fila de 466 dias"
 * diz a urgência; "6.059 esperando" diz o tamanho. Mesma conta do "Quem espera" do clique.
 */
function NaFila({ n }: { n: number | null }) {
  if (n === null) return null;
  if (n === 0) {
    return <span className="text-xs text-gray-400">ninguém na fila</span>;
  }
  return (
    <span
      className="inline-flex items-center gap-1 rounded-full bg-primary-50 px-2 py-0.5 text-xs font-medium text-primary-700 ring-1 ring-primary-200"
      title="Pessoas esperando agora por este procedimento (fila do SISREG)"
    >
      <Users className="h-3 w-3" />
      {n.toLocaleString('pt-BR')} na fila
    </span>
  );
}

function AgendaLocal() {
  return (
    <span
      className="inline-flex items-center gap-1 rounded bg-gray-100 px-1.5 py-0.5 text-[11px] text-gray-600"
      title="A própria unidade marca nestas vagas — não passam pela regulação."
    >
      <Home className="h-3 w-3" />
      agenda local
    </span>
  );
}

/** Clicável por mouse e teclado. */
function propsDeClique(ativo: boolean, abrir: () => void) {
  return ativo
    ? {
        role: 'button' as const,
        tabIndex: 0,
        onClick: abrir,
        onKeyDown: (e: React.KeyboardEvent) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            abrir();
          }
        },
        title: 'Ver as datas e quem está esperando',
      }
    : {};
}

function CartaoAgenda({
  a,
  urgente,
  aoAbrir,
}: {
  a: AgendaNova;
  urgente: number;
  aoAbrir: (x: Aberta) => void;
}) {
  const destaque = !a.agendaLocal && (a.esperaMedianaDias ?? 0) >= urgente;
  const recorrente = a.blocos > 1;
  return (
    <li
      {...propsDeClique(true, () => aoAbrir({ codigo: a.procedimentoCodigo, nome: a.procedimentoNome }))}
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
        {/* O número grande é o que a regulação ainda pode marcar no procedimento — não o tamanho do
            bloco que abriu (o ECO adulto aparecia com "4 vagas/semana" e havia ~300 livres). */}
        <div className="shrink-0 text-right">
          {a.agendaLocal ? (
            <>
              <p className="text-lg font-semibold leading-none text-primary-700">
                {a.vagasLivresUnidade ?? '—'}
              </p>
              <p className="text-[11px] text-gray-500">livres nesta unidade</p>
            </>
          ) : (
            <>
              <p className="text-lg font-semibold leading-none text-primary-700">
                {a.vagasLivresRegulacao ?? '—'}
              </p>
              <p className="text-[11px] text-gray-500">
                vagas livres na regulação
                {a.unidadesComVaga ? ` · ${a.unidadesComVaga} unid.` : ''}
              </p>
              {a.primeiraVagaLivreRegulacao ? (
                <p className="text-[11px] text-gray-500">1ª em {diaMes(a.primeiraVagaLivreRegulacao)}</p>
              ) : null}
            </>
          )}
        </div>
      </div>

      <div className="mt-2 flex flex-wrap items-center gap-2">
        <NaFila n={a.naFila} />
        <Espera dias={a.esperaMedianaDias} urgente={urgente} />
        {a.agendaLocal ? <AgendaLocal /> : null}
        <span className="rounded bg-gray-100 px-1.5 py-0.5 text-[11px] text-gray-600">
          {recorrente
            ? [...a.diasSemana].sort((x, y) => x - y).map((d) => DIAS_SEMANA[d]).join(', ')
            : 'dia único'}
        </span>
        {/* A vigência NÃO vai no cartão: ela é a validade do bloco, e foi lida como "período com
            vaga" (ECG "01/07/25 a 31/12/26" quando a vaga era em novembro). As datas estão no clique. */}
        <span className="text-[11px] text-gray-500">
          {a.agendaLocal ? '' : `nesta unidade: ${a.vagasLivresUnidade ?? '—'} livres · `}abriu {a.vagas}{' '}
          vaga(s)
        </span>
        <span className="text-[11px] font-medium text-primary-700">ver datas →</span>
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
  aoAbrir: (x: Aberta) => void;
}) {
  const dias = diasAte(v.dataAgendada);
  const corre = dias <= perecivel;
  const clicavel = Boolean(v.procedimentoNome);
  return (
    <li
      {...propsDeClique(clicavel, () => aoAbrir({ codigo: v.procedimentoCodigo, nome: v.procedimentoNome! }))}
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
        <NaFila n={v.naFila} />
        <Espera dias={v.esperaMedianaDias} urgente={urgente} />
        {v.agendaLocal ? <AgendaLocal /> : null}
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
 * <p><b>Agenda local fica escondida por padrão.</b> Nela a própria unidade marca; o regulador não
 * enxerga essas vagas no SISREG. Em 10/09/2026 todas as "vagas de ECG" da tela eram de USF de
 * agenda local — anunciar isso ao regulador é mandá-lo atrás de vaga que ele não pode usar.</p>
 *
 * <p>Clicar abre <b>quando dá para marcar</b> (as datas, por unidade) e <b>quem espera</b> (a fila
 * lida do SISREG). Agendar continua sendo no SISREG.</p>
 */
export function OfertasPage() {
  const [dias, setDias] = useState(7);
  const [comAgendaLocal, setComAgendaLocal] = useState(false);
  const [aberta, setAberta] = useState<Aberta | null>(null);
  const { data, isLoading, isError } = useOfertas(dias);

  const urgente = data?.diasEsperaUrgente ?? 180;
  const perecivel = data?.diasVagaPerecivel ?? 7;
  const todasAgendas = data?.agendasNovas ?? [];
  const todasVagas = data?.vagasLiberadas ?? [];
  const agendas = comAgendaLocal ? todasAgendas : todasAgendas.filter((a) => !a.agendaLocal);
  // Horário que já passou não é vaga: sai da tela. O servidor já não manda; isto cobre o intervalo
  // entre uma recarga e outra (a página recarrega a cada 5 minutos).
  const agora = Date.now();
  const vagasFuturas = todasVagas.filter((v) => new Date(v.dataAgendada).getTime() > agora);
  const vagas = comAgendaLocal ? vagasFuturas : vagasFuturas.filter((v) => v.agendaLocal !== true);
  const ocultas = todasAgendas.length - agendas.length + (vagasFuturas.length - vagas.length);
  const correndo = vagas.filter((v) => diasAte(v.dataAgendada) <= perecivel).length;

  if (aberta) {
    return (
      <div className="mx-auto max-w-6xl space-y-4 px-4 py-6">
        <header>
          <button
            type="button"
            onClick={() => setAberta(null)}
            className="mb-1 inline-flex items-center gap-1 text-xs text-gray-500 hover:text-gray-800"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
            voltar para as ofertas
          </button>
          <h1 className="text-lg font-semibold text-gray-900">{aberta.nome}</h1>
        </header>
        {aberta.codigo ? <DatasDaOfertaPainel codigo={aberta.codigo} /> : null}
        <FilaDaOfertaPainel procedimento={aberta.nome} codigo={aberta.codigo} />
      </div>
    );
  }

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
          <strong> Clique numa oferta para ver as datas livres e quem está esperando.</strong>
        </p>
      </header>

      <div className="mb-4 flex flex-wrap items-center gap-4">
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
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={comAgendaLocal}
            onChange={(e) => setComAgendaLocal(e.target.checked)}
          />
          Mostrar agenda local das unidades
          {!comAgendaLocal && ocultas > 0 ? (
            <span className="text-xs text-gray-500">({ocultas} oculta(s) — fora da regulação)</span>
          ) : null}
        </label>
        {isLoading ? <Loader2 className="h-4 w-4 animate-spin text-gray-400" /> : null}
      </div>

      {isError ? (
        <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">
          Não foi possível carregar as ofertas.
        </p>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-2">
        <section>
          <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold text-gray-800">
            <CalendarPlus className="h-4 w-4 text-emerald-600" />
            Abriu agenda
            {agendas.length > 0 ? (
              <span className="text-xs font-normal text-gray-500">
                {agendas.length} agenda(s)
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
                  aoAbrir={setAberta}
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
                  aoAbrir={setAberta}
                />
              ))}
            </ul>
          )}
        </section>
      </div>

      <footer className="mt-6 flex items-start gap-2 rounded-md bg-gray-50 p-3 text-xs text-gray-600">
        <Clock className="mt-0.5 h-3.5 w-3.5 shrink-0 text-gray-400" />
        <p>
          <strong>Como ler.</strong> “N na fila” é quantas pessoas esperam agora pelo procedimento
          (fila do SISREG). “Espera Nd” é a espera mediana de quem <em>já conseguiu data</em> neste
          procedimento neste ano — serve para priorizar entre procedimentos; a espera de quem ainda
          aguarda está no clique. <strong>“Vagou”</strong> quer dizer que o SISREG parou de
          mostrar aquele agendamento; confirmar se a vaga está livre é trabalho de gente.{' '}
          <strong>“Vagas livres na regulação”</strong> soma as unidades reguladas nos próximos 120
          dias (1ª vez + reserva, menos o que já está agendado) — é estimativa: a grade do SISREG, ao
          autorizar, é a verdade.
        </p>
      </footer>
    </div>
  );
}
