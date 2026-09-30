import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { BellRing, Check, Loader2 } from 'lucide-react';

import {
  useMarcarNotificacaoEsusSgVista,
  useNotificacoesEsusSg,
  useResumoNotificacoesEsusSg,
  useTecnicosNotificacoesEsusSg,
} from '@/features/esussg/api/queries';
import {
  ROTULO_SITUACAO_ESUSSG,
  SITUACOES_ESUSSG,
  TIPOS_RECURSO_ESUSSG,
  type NotificacaoEsusSg,
  type SituacaoEsusSg,
  type TipoGatilhoEsusSg,
  type TipoRecursoEsusSg,
} from '@/features/esussg/types';
import { SituacaoEsusSgBadge } from '@/features/esussg/components/SituacaoEsusSgBadge';
import { ModalSolicitacaoEsusSg } from '@/features/esussg/components/ModalSolicitacaoEsusSg';
import { dataHoraAgendada } from '@/features/esussg/lib/agendamento';
import { limparTexto } from '@/features/esussg/lib/formatacao';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { Button } from '@/shared/ui/Button';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Paginacao } from '@/shared/ui/Paginacao';
import { formatarInstante } from '@/shared/lib/datas';
import { AvatarTecnico } from '@/shared/regulacao/AvatarTecnico';
import { FiltroTecnicos } from '@/shared/regulacao/FiltroTecnicos';
import { SEM_TECNICO } from '@/shared/regulacao/tecnicos';
import { useTecnicosFiltro } from '@/shared/regulacao/tecnicosFiltroPreferencia';

/**
 * Regulação → ESUS SG → Notificações: o que mudou no ESUS de São Gonçalo e ainda ninguém olhou.
 *
 * Irmã da tela do SERNIT, sem o que o ESUS não tem: não há FollowUP (nem categoria, nem filtro de
 * último FollowUP). Cada linha é um MOVIMENTO — entrou na fila, foi agendado, remarcado, mudou de
 * prioridade, saiu da fila. "Visto" é marca NOSSA: tira daqui e não escreve nada no ESUS, por isso
 * basta a Consulta do módulo.
 */

const ROTULO_GATILHO: Record<TipoGatilhoEsusSg, string> = {
  NovaSolicitacao: 'Novo pedido',
  MudancaSituacao: 'Mudou de situação',
  MudancaAgendamento: 'Remarcado',
  MudancaPrioridade: 'Mudou a prioridade',
};

const COR_GATILHO: Record<TipoGatilhoEsusSg, string> = {
  NovaSolicitacao: 'bg-sky-100 text-sky-800',
  MudancaSituacao: 'bg-amber-100 text-amber-800',
  MudancaAgendamento: 'bg-violet-100 text-violet-800',
  MudancaPrioridade: 'bg-rose-100 text-rose-800',
};

const GATILHOS: TipoGatilhoEsusSg[] = [
  'NovaSolicitacao',
  'MudancaSituacao',
  'MudancaAgendamento',
  'MudancaPrioridade',
];

export function EsusSgNotificacoesPage() {
  // Na PPI de São Gonçalo o município usa, na prática, só Exame — a aba de Exame abre primeiro.
  const [tipo, setTipo] = useState<TipoRecursoEsusSg>('Exame');
  const [situacao, setSituacao] = useState<SituacaoEsusSg | undefined>(undefined);
  const [tipoGatilho, setTipoGatilho] = useState<TipoGatilhoEsusSg | undefined>(undefined);
  const [paginaAtual, setPaginaAtual] = useState(1);
  const [tamanho, setTamanho] = useState(100);

  // Técnico = quem incluiu o pedido na fila do ESUS. Salvo no perfil do usuário; resumo e lista
  // recontam pelo filtro — o número da aba tem de bater com o que aparece embaixo.
  const tecnicosSelecionados = useTecnicosFiltro((s) => s.esussg);
  const definirTecnicos = useTecnicosFiltro((s) => s.definir);
  const { data: tecnicos = [] } = useTecnicosNotificacoesEsusSg();
  const escolherTecnicos = (chaves: string[]) => {
    definirTecnicos('esussg', chaves);
    setPaginaAtual(1);
  };

  const { data: resumo } = useResumoNotificacoesEsusSg(tecnicosSelecionados);
  const filtro = useMemo(
    () => ({
      tipo,
      tecnicos: tecnicosSelecionados,
      situacao,
      tipoGatilho,
      pagina: paginaAtual,
      tamanho,
    }),
    [tipo, tecnicosSelecionados, situacao, tipoGatilho, paginaAtual, tamanho],
  );
  const { data: pagina, isLoading } = useNotificacoesEsusSg(filtro);

  // Marcar como visto encolhe a lista: se a página atual deixou de existir, volta para a última.
  useEffect(() => {
    if (!pagina || pagina.total === 0) return;
    const ultima = Math.ceil(pagina.total / tamanho);
    if (paginaAtual > ultima) setPaginaAtual(ultima);
  }, [pagina, paginaAtual, tamanho]);

  const mudarPagina = (p: number) => {
    setPaginaAtual(p);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };
  const mudarTamanho = (t: number) => {
    setTamanho(t);
    setPaginaAtual(1);
  };
  const escolherSituacao = (s: SituacaoEsusSg | undefined) => {
    setSituacao(s);
    setPaginaAtual(1);
  };

  const marcarUma = useMarcarNotificacaoEsusSgVista();
  const [detalhe, setDetalhe] = useState<string | null>(null);

  const porTipo = (t: TipoRecursoEsusSg) =>
    resumo?.contadores.filter((c) => c.tipo === t).reduce((a, c) => a + c.quantidade, 0) ?? 0;

  const porSituacao = (s: SituacaoEsusSg) =>
    resumo?.contadores.find((c) => c.tipo === tipo && c.situacao === s)?.quantidade ?? 0;

  return (
    <div className="space-y-4">
      <header className="flex items-center gap-3">
        <BellRing className="size-6 text-red-700" />
        <div>
          <div className="flex items-center gap-1">
            <h1 className="text-xl font-semibold text-slate-900">Notificações do ESUS São Gonçalo</h1>
            <AjudaManual artigo="esus-sao-goncalo" secao="notificacoes" />
          </div>
          <p className="text-sm text-slate-600">
            Movimentações no ESUS de São Gonçalo que ainda não foram vistas. Marcar como visto tira
            daqui — e não escreve nada no ESUS.
          </p>
        </div>
        {resumo && resumo.total > 0 && (
          <span className="ml-auto rounded-full bg-red-700 px-3 py-1 text-sm font-semibold text-white">
            {resumo.total}
          </span>
        )}
      </header>

      <div className="flex flex-wrap items-center gap-2 border-b border-slate-200">
        {TIPOS_RECURSO_ESUSSG.map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => {
              setTipo(t);
              setSituacao(undefined);
              setPaginaAtual(1);
            }}
            className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium ${
              tipo === t
                ? 'border-red-700 text-red-700'
                : 'border-transparent text-slate-500 hover:text-slate-700'
            }`}
          >
            {t}
            {porTipo(t) > 0 && (
              <span className="ml-2 rounded-full bg-slate-200 px-2 py-0.5 text-xs text-slate-700">
                {porTipo(t)}
              </span>
            )}
          </button>
        ))}

        <div className="mb-1 ml-auto flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-2 py-1">
          <label
            htmlFor="esussg-filtro-tecnicos"
            className="text-xs font-semibold uppercase tracking-wide text-slate-500"
            title="Quem incluiu o pedido na fila do ESUS"
          >
            Técnico (quem incluiu)
          </label>
          <FiltroTecnicos
            id="esussg-filtro-tecnicos"
            tecnicos={tecnicos}
            selecionados={tecnicosSelecionados}
            aoMudar={escolherTecnicos}
          />
        </div>
      </div>

      {/* Situações: só aparecem as que têm movimento — lista cheia de zeros é ruído */}
      <div className="flex flex-wrap gap-2">
        <FiltroSituacao ativo={situacao === undefined} onClick={() => escolherSituacao(undefined)}>
          Todas
        </FiltroSituacao>
        {SITUACOES_ESUSSG.filter((s) => porSituacao(s) > 0).map((s) => (
          <FiltroSituacao key={s} ativo={situacao === s} onClick={() => escolherSituacao(s)}>
            {ROTULO_SITUACAO_ESUSSG[s]}
            <span className="ml-1.5 text-xs opacity-80">{porSituacao(s)}</span>
          </FiltroSituacao>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-2 text-sm">
        <label htmlFor="esussg-filtro-gatilho" className="text-slate-600">
          Movimento:
        </label>
        <select
          id="esussg-filtro-gatilho"
          value={tipoGatilho ?? ''}
          onChange={(e) => {
            setTipoGatilho((e.target.value || undefined) as TipoGatilhoEsusSg | undefined);
            setPaginaAtual(1);
          }}
          className="rounded border border-slate-300 bg-white px-2 py-1 text-sm"
        >
          <option value="">Todos</option>
          {GATILHOS.map((g) => (
            <option key={g} value={g}>
              {ROTULO_GATILHO[g]}
            </option>
          ))}
        </select>
      </div>

      {isLoading && (
        <div className="flex items-center gap-2 p-6 text-slate-500">
          <Loader2 className="size-4 animate-spin" /> carregando…
        </div>
      )}

      {!isLoading && (pagina?.itens.length ?? 0) === 0 && (
        <div className="rounded-lg border border-dashed border-slate-300 p-10 text-center text-slate-500">
          Nada novo por aqui. Toda movimentação já foi vista.
        </div>
      )}

      {pagina && pagina.total > 0 && (
        <Paginacao
          pagina={paginaAtual}
          tamanho={tamanho}
          total={pagina.total}
          aoMudarPagina={mudarPagina}
          aoMudarTamanho={mudarTamanho}
        />
      )}

      <div className="space-y-2">
        {pagina?.itens.map((n) => (
          <LinhaNotificacao
            key={n.id}
            n={n}
            ocupado={marcarUma.isPending}
            onVista={() => marcarUma.mutate(n.id)}
            onAbrir={() => setDetalhe(n.solicitacaoId)}
          />
        ))}
      </div>

      {pagina && pagina.total > pagina.itens.length && (
        <Paginacao
          pagina={paginaAtual}
          tamanho={tamanho}
          total={pagina.total}
          aoMudarPagina={mudarPagina}
          aoMudarTamanho={mudarTamanho}
        />
      )}
      <ModalSolicitacaoEsusSg solicitacaoId={detalhe} aoFechar={() => setDetalhe(null)} />
    </div>
  );
}

function FiltroSituacao({
  ativo,
  onClick,
  children,
}: {
  ativo: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-full border px-3 py-1 text-sm ${
        ativo
          ? 'border-red-700 bg-red-50 text-red-800'
          : 'border-slate-300 text-slate-600 hover:bg-slate-50'
      }`}
    >
      {children}
    </button>
  );
}

function LinhaNotificacao({
  n,
  ocupado,
  onVista,
  onAbrir,
}: {
  n: NotificacaoEsusSg;
  ocupado: boolean;
  onVista: () => void;
  onAbrir: () => void;
}) {
  const agendado = dataHoraAgendada(n.dataHoraAgendadaTexto, n.dataAgendada);

  return (
    <div className="flex items-start gap-3 rounded-lg border border-slate-200 bg-white p-3 transition hover:border-slate-300 hover:bg-slate-50">
      {/* Quem incluiu o pedido na fila do ESUS: a bolinha colorida é o que o técnico procura. */}
      <AvatarTecnico chave={n.tecnico ?? SEM_TECNICO} className="mt-0.5" />

      <div
        role="button"
        tabIndex={0}
        onClick={onAbrir}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            onAbrir();
          }
        }}
        className="min-w-0 flex-1 cursor-pointer space-y-1 text-left"
      >
        <div className="flex flex-wrap items-center gap-2">
          <span className={`rounded px-2 py-0.5 text-xs font-medium ${COR_GATILHO[n.tipo]}`}>
            {ROTULO_GATILHO[n.tipo]}
          </span>

          {n.tipo === 'MudancaSituacao' && n.situacaoAnterior && n.situacaoAtual && (
            <span className="flex items-center gap-1.5 text-xs text-slate-600">
              <SituacaoEsusSgBadge situacao={n.situacaoAnterior} />
              <span aria-hidden>→</span>
              <SituacaoEsusSgBadge situacao={n.situacaoAtual} />
            </span>
          )}
          {n.tipo !== 'MudancaSituacao' && n.situacaoAtual && (
            <SituacaoEsusSgBadge situacao={n.situacaoAtual} />
          )}

          <span className="text-xs text-slate-400">{formatarInstante(n.criadoEm)}</span>
        </div>

        <div className="truncate text-sm font-medium text-slate-900">
          {n.pacienteId ? (
            <span onClick={(e) => e.stopPropagation()}>
              <NomePacienteComResumo
                pacienteId={n.pacienteId}
                nome={n.pacienteNome || '(sem nome)'}
                mostrarWhatsApp
              />
            </span>
          ) : (
            n.pacienteNome || '(sem nome)'
          )}
        </div>
        <div className="truncate text-xs text-slate-600">
          {n.recurso || '—'}
          {n.prioridade && <span className="text-slate-400"> · {n.prioridade}</span>}
        </div>
        {agendado && (
          <div className="text-xs text-slate-500">
            Agendado para {agendado}
            {n.unidadeExecutora && ` · ${limparTexto(n.unidadeExecutora)}`}
          </div>
        )}

        {n.ultimoEvento && (
          <div className="text-xs text-slate-500">
            Último marco:{' '}
            {/* Os rótulos da trilha do ESUS já são nossos ("Inclusão na fila", "Saiu da fila") —
                mostram-se como estão, sem passar pelo vocabulário do SER. */}
            <span className="font-medium text-slate-700">{n.ultimoEvento.evento}</span>{' '}
            {formatarInstante(n.ultimoEvento.dataEvento)}
          </div>
        )}

        <span className="inline-block text-xs text-red-700">pedido {n.idEsusSg} no ESUS</span>
      </div>

      <div className="flex shrink-0 flex-col gap-1">
        <Button
          variante="secundaria"
          onClick={onVista}
          disabled={ocupado}
          title="Marcar este movimento como visto (não escreve no ESUS)"
        >
          <Check className="size-4" />
          Vista
        </Button>
      </div>
    </div>
  );
}
