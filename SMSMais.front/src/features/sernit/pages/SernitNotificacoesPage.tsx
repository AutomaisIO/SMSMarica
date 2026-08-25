import { useMemo, useState } from 'react';
import { BellRing, Check, Loader2 } from 'lucide-react';

import {
  useMarcarNotificacaoSernitVista,
  useNotificacoesSernit,
  useResumoNotificacoesSernit,
} from '@/features/sernit/api/queries';
import {
  ROTULO_SITUACAO,
  SITUACOES_SERNIT,
  type NotificacaoSernit,
  type SituacaoSernit,
  type TipoGatilhoSernit,
  type TipoRecursoSernit,
} from '@/features/sernit/types';
import { SituacaoSernitBadge } from '@/features/sernit/components/SituacaoSernitBadge';
import { ModalSolicitacaoSernit } from '@/features/sernit/components/ModalSolicitacaoSernit';
import { Button } from '@/shared/ui/Button';
import { formatarInstante } from '@/shared/lib/datas';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';

/**
 * Regulação → Notificações: o que mudou no SERNIT e ainda ninguém olhou.
 *
 * Cada linha é um MOVIMENTO (não uma solicitação): entrou na fila, saiu para agendada, foi
 * cancelada, chegou FollowUP. Marcar como visto é o que tira o item daqui — e, por baixo, o que
 * consome a fila de gatilhos do motor. Enquanto ninguém marca, nada some.
 */

const ROTULO_GATILHO: Record<TipoGatilhoSernit, string> = {
  NovaSolicitacao: 'Nova solicitação',
  MudancaSituacao: 'Mudou de situação',
  MudancaAgendamento: 'Remarcada',
  NovoFollowUp: 'FollowUP novo',
};

/** FollowUP é o que o paciente (ou a unidade) cobra da fila — merece destaque próprio. */
const COR_GATILHO: Record<TipoGatilhoSernit, string> = {
  NovaSolicitacao: 'bg-sky-100 text-sky-800',
  MudancaSituacao: 'bg-amber-100 text-amber-800',
  MudancaAgendamento: 'bg-violet-100 text-violet-800',
  NovoFollowUp: 'bg-rose-100 text-rose-800',
};

const TIPOS: TipoRecursoSernit[] = ['Consulta', 'Exame'];

export function SernitNotificacoesPage() {
  const [tipo, setTipo] = useState<TipoRecursoSernit>('Consulta');
  const [situacao, setSituacao] = useState<SituacaoSernit | undefined>(undefined);

  const { data: resumo } = useResumoNotificacoesSernit();
  const filtro = useMemo(() => ({ tipo, situacao, pagina: 1, tamanho: 100 }), [tipo, situacao]);
  const { data: pagina, isLoading } = useNotificacoesSernit(filtro);

  const marcarUma = useMarcarNotificacaoSernitVista();

  // Abrir em modal, e não navegar: quem tria a fila perde filtro, aba e posição de leitura se a
  // tela troca — e volta tendo de reencontrar onde estava.
  const [detalhe, setDetalhe] = useState<string | null>(null);

  const porTipo = (t: TipoRecursoSernit) =>
    resumo?.contadores.filter((c) => c.tipo === t).reduce((a, c) => a + c.quantidade, 0) ?? 0;

  const porSituacao = (s: SituacaoSernit) =>
    resumo?.contadores.find((c) => c.tipo === tipo && c.situacao === s)?.quantidade ?? 0;

  return (
    <div className="space-y-4">
      <header className="flex items-center gap-3">
        <BellRing className="size-6 text-red-700" />
        <div>
          <h1 className="text-xl font-semibold text-slate-900">Notificações da regulação</h1>
          <p className="text-sm text-slate-600">
            Movimentações no SERNIT que ainda não foram vistas. Marcar como visto tira daqui.
          </p>
        </div>
        {resumo && resumo.total > 0 && (
          <span className="ml-auto rounded-full bg-red-700 px-3 py-1 text-sm font-semibold text-white">
            {resumo.total}
          </span>
        )}
      </header>

      {/* Abas Consulta / Exame — cada uma com o próprio contador */}
      <div className="flex gap-2 border-b border-slate-200">
        {TIPOS.map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => {
              setTipo(t);
              setSituacao(undefined);
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
      </div>

      {/* Situações: só aparecem as que têm movimento — lista cheia de zeros é ruído */}
      <div className="flex flex-wrap gap-2">
        <FiltroSituacao ativo={situacao === undefined} onClick={() => setSituacao(undefined)}>
          Todas
        </FiltroSituacao>
        {SITUACOES_SERNIT.filter((s) => porSituacao(s) > 0).map((s) => (
          <FiltroSituacao key={s} ativo={situacao === s} onClick={() => setSituacao(s)}>
            {ROTULO_SITUACAO[s]}
            <span className="ml-1.5 text-xs opacity-80">{porSituacao(s)}</span>
          </FiltroSituacao>
        ))}
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
        <p className="text-center text-xs text-slate-500">
          Mostrando {pagina.itens.length} de {pagina.total}. Marque as vistas para revelar o resto.
        </p>
      )}
      <ModalSolicitacaoSernit solicitacaoId={detalhe} aoFechar={() => setDetalhe(null)} />
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
  children: React.ReactNode;
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
  n: NotificacaoSernit;
  ocupado: boolean;
  onVista: () => void;
  onAbrir: () => void;
}) {
  return (
    <div className="flex items-start gap-3 rounded-lg border border-slate-200 bg-white p-3 transition hover:border-slate-300 hover:bg-slate-50">
      {/* A linha inteira abre o detalhe: o alvo de clique é o caso, não um link escondido no
          meio do texto. Os botões ficam fora deste bloco para não disparar o modal junto. */}
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

          {/* A transição é a informação principal: de onde saiu, para onde foi. */}
          {n.tipo === 'MudancaSituacao' && n.situacaoAnterior && n.situacaoAtual && (
            <span className="flex items-center gap-1.5 text-xs text-slate-600">
              <SituacaoSernitBadge situacao={n.situacaoAnterior} />
              <span aria-hidden>→</span>
              <SituacaoSernitBadge situacao={n.situacaoAtual} />
            </span>
          )}
          {n.tipo !== 'MudancaSituacao' && n.situacaoAtual && (
            <SituacaoSernitBadge situacao={n.situacaoAtual} />
          )}

          <span className="text-xs text-slate-400">{formatarInstante(n.criadoEm)}</span>
        </div>

        <div className="truncate text-sm font-medium text-slate-900">
          {/* Os ícones param o clique: a linha inteira abre a solicitação, e abrir o resumo do
              paciente ou o WhatsApp não pode arrastar o operador para outra tela junto. */}
          {n.pacienteId ? (
            <span onClick={(e) => e.stopPropagation()}>
              <NomePacienteComResumo
                pacienteId={n.pacienteId}
                nome={n.pacienteNome ?? '(sem nome)'}
                mostrarWhatsApp
              />
            </span>
          ) : (
            (n.pacienteNome ?? '(sem nome)')
          )}
        </div>
        <div className="truncate text-xs text-slate-600">{n.recurso ?? '—'}</div>
        {n.agendadoParaTexto && (
          <div className="text-xs text-slate-500">Agendado para {n.agendadoParaTexto}</div>
        )}

        <span className="inline-block text-xs text-red-700">solicitação {n.idSernit}</span>
      </div>

      <div className="flex shrink-0 flex-col gap-1">
        <Button variante="secundaria" onClick={onVista} disabled={ocupado} title="Marcar este movimento como visto">
          <Check className="size-4" />
          Vista
        </Button>
      </div>
    </div>
  );
}
