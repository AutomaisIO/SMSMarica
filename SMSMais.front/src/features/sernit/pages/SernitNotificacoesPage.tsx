import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, BellRing, Check, Loader2 } from 'lucide-react';

import {
  useMarcarNotificacaoSernitVista,
  useNotificacoesSernit,
  useResumoNotificacoesSernit,
  useTecnicosNotificacoesSernit,
} from '@/features/sernit/api/queries';
import {
  ROTULO_SITUACAO,
  SITUACOES_SERNIT,
  type FollowUpResumoSernit,
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
import { Paginacao } from '@/shared/ui/Paginacao';
import {
  CATEGORIAS_FOLLOWUP,
  ROTULO_CATEGORIA_FOLLOWUP,
  ehCategoriaDeAtencao,
  rotuloCategoriaFollowUp,
  type CategoriaFollowUp,
} from '@/shared/regulacao/categoriasFollowUp';
import {
  ROTULO_TIPO_EVENTO_EXTERNO,
  TIPOS_EVENTO_EXTERNO_FILTRO,
  rotuloTipoEventoExterno,
  type TipoEventoExterno,
} from '@/shared/regulacao/eventosExternos';
import { AvatarTecnico } from '@/shared/regulacao/AvatarTecnico';
import { FiltroTecnicos } from '@/shared/regulacao/FiltroTecnicos';
import { SEM_TECNICO } from '@/shared/regulacao/tecnicos';
import { useTecnicosFiltro } from '@/shared/regulacao/tecnicosFiltroPreferencia';

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
  // Filtro pelo ÚLTIMO FollowUP da solicitação (a categoria que o card mostra). Não zera
  // ao trocar de aba: quem está caçando "falha de contato" quer isso em Consulta e Exame.
  const [categoria, setCategoria] = useState<CategoriaFollowUp | undefined>(undefined);
  // Filtro pelo ÚLTIMO evento da trilha, de qualquer verbo ("Chegada no Destino", "Transferir"…).
  const [tipoEvento, setTipoEvento] = useState<TipoEventoExterno | undefined>(undefined);

  // Página e tamanho: até 15/09/2026 a tela trazia só os 100 mais recentes, e o resto só aparecia
  // marcando os primeiros como vistos.
  const [paginaAtual, setPaginaAtual] = useState(1);
  const [tamanho, setTamanho] = useState(100);

  // Técnico regulador (quem incluiu no SERNIT): salvo no perfil do usuário. Resumo e lista
  // recontam pelo filtro — o número da aba tem de bater com o que aparece embaixo.
  const tecnicosSelecionados = useTecnicosFiltro((s) => s.sernit);
  const definirTecnicos = useTecnicosFiltro((s) => s.definir);
  const { data: tecnicos = [] } = useTecnicosNotificacoesSernit();
  const escolherTecnicos = (chaves: string[]) => {
    definirTecnicos('sernit', chaves);
    setPaginaAtual(1);
  };

  const { data: resumo } = useResumoNotificacoesSernit(tecnicosSelecionados);
  const filtro = useMemo(
    () => ({
      tipo,
      tecnicos: tecnicosSelecionados,
      situacao,
      categoriaFollowUp: categoria,
      tipoUltimoEvento: tipoEvento,
      pagina: paginaAtual,
      tamanho,
    }),
    [tipo, tecnicosSelecionados, situacao, categoria, tipoEvento, paginaAtual, tamanho],
  );
  const { data: pagina, isLoading } = useNotificacoesSernit(filtro);

  // Marcar como vista encolhe a lista: se a página atual deixou de existir, volta para a última.
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
  const escolherSituacao = (s: SituacaoSernit | undefined) => {
    setSituacao(s);
    setPaginaAtual(1);
  };
  const escolherCategoria = (c: CategoriaFollowUp | undefined) => {
    setCategoria(c);
    setPaginaAtual(1);
  };
  const escolherTipoEvento = (t: TipoEventoExterno | undefined) => {
    setTipoEvento(t);
    setPaginaAtual(1);
  };

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
      </div>

      {/* Situações: só aparecem as que têm movimento — lista cheia de zeros é ruído */}
      <div className="flex flex-wrap gap-2">
        <FiltroSituacao ativo={situacao === undefined} onClick={() => escolherSituacao(undefined)}>
          Todas
        </FiltroSituacao>
        {SITUACOES_SERNIT.filter((s) => porSituacao(s) > 0).map((s) => (
          <FiltroSituacao key={s} ativo={situacao === s} onClick={() => escolherSituacao(s)}>
            {ROTULO_SITUACAO[s]}
            <span className="ml-1.5 text-xs opacity-80">{porSituacao(s)}</span>
          </FiltroSituacao>
        ))}
      </div>

      {/* Filtro pelo último FollowUP. O atalho "Falha de contato" é a fila de quem a central
          não conseguiu achar — e nós temos telefone verificado e WhatsApp que ela não tem. */}
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <label htmlFor="sernit-filtro-tecnicos" className="text-slate-600">
          Técnico regulador:
        </label>
        <FiltroTecnicos
          id="sernit-filtro-tecnicos"
          tecnicos={tecnicos}
          selecionados={tecnicosSelecionados}
          aoMudar={escolherTecnicos}
        />

        <label htmlFor="sernit-filtro-categoria-followup" className="ml-2 text-slate-600">
          Último FollowUP:
        </label>
        <select
          id="sernit-filtro-categoria-followup"
          value={categoria ?? ''}
          onChange={(e) =>
            escolherCategoria((e.target.value || undefined) as CategoriaFollowUp | undefined)
          }
          className="rounded border border-slate-300 bg-white px-2 py-1 text-sm"
        >
          <option value="">Todos</option>
          {CATEGORIAS_FOLLOWUP.map((c) => (
            <option key={c} value={c}>
              {ROTULO_CATEGORIA_FOLLOWUP[c]}
            </option>
          ))}
        </select>
        <button
          type="button"
          onClick={() =>
            escolherCategoria(categoria === 'FalhaContato' ? undefined : 'FalhaContato')
          }
          className={`flex items-center gap-1 rounded-full border px-3 py-1 text-sm ${
            categoria === 'FalhaContato'
              ? 'border-amber-500 bg-amber-100 text-amber-900'
              : 'border-amber-300 text-amber-800 hover:bg-amber-50'
          }`}
          title="Só solicitações cujo último FollowUP é falha de contato com o paciente"
        >
          <AlertTriangle className="size-4" />
          Falha de contato
        </button>

        <label htmlFor="sernit-filtro-tipo-evento" className="ml-2 text-slate-600">
          Último evento:
        </label>
        <select
          id="sernit-filtro-tipo-evento"
          value={tipoEvento ?? ''}
          onChange={(e) =>
            escolherTipoEvento((e.target.value || undefined) as TipoEventoExterno | undefined)
          }
          className="rounded border border-slate-300 bg-white px-2 py-1 text-sm"
        >
          <option value="">Todos</option>
          {TIPOS_EVENTO_EXTERNO_FILTRO.map((t) => (
            <option key={t} value={t}>
              {ROTULO_TIPO_EVENTO_EXTERNO[t]}
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
  // Ponto de atenção: o último FollowUP pede ação da unidade (falha de contato, pedido de
  // documento). O card inteiro muda de cor — é o que se enxerga rolando uma fila de 100.
  const atencao = ehCategoriaDeAtencao(n.ultimoFollowUp?.categoria);

  return (
    <div
      className={`flex items-start gap-3 rounded-lg border p-3 transition ${
        atencao
          ? 'border-amber-300 bg-amber-50/60 hover:border-amber-400 hover:bg-amber-50'
          : 'border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50'
      }`}
    >
      {/* Quem incluiu a solicitação: a bolinha colorida é o que o técnico procura na fila. */}
      <AvatarTecnico chave={n.tecnico ?? SEM_TECNICO} className="mt-0.5" />

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

        {/* O último FollowUP aparece em TODO card, não só no de "FollowUP novo": quem tria a
            fila precisa saber o que já foi cobrado sem abrir o detalhe de cada linha. */}
        {n.ultimoFollowUp && <UltimoFollowUp f={n.ultimoFollowUp} />}

        {/* O último evento de qualquer verbo, quando não é o próprio FollowUP já mostrado acima:
            "Chegada no Destino" fecha o ciclo; "Transferir"/"Devolvido" pedem reação. */}
        {n.ultimoEvento && n.ultimoEvento.tipo !== 'FollowUp' && (
          <div className="text-xs text-slate-500">
            Último evento:{' '}
            <span className="font-medium text-slate-700">
              {rotuloTipoEventoExterno(n.ultimoEvento.tipo, n.ultimoEvento.evento)}
            </span>{' '}
            {formatarInstante(n.ultimoEvento.dataEvento)}
          </div>
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


/** Último FollowUP da solicitação: quando, quem e o texto (cortado em duas linhas — o card é
 * um resumo; o texto inteiro está no detalhe). */
function UltimoFollowUp({ f }: { f: FollowUpResumoSernit }) {
  const atencao = ehCategoriaDeAtencao(f.categoria);
  const rotulo = rotuloCategoriaFollowUp(f.categoria);

  return (
    <div
      className={`rounded border px-2 py-1 text-xs text-slate-700 ${
        atencao ? 'border-amber-300 bg-amber-100/70' : 'border-rose-100 bg-rose-50/60'
      }`}
    >
      <div
        className={`flex flex-wrap items-center gap-x-2 text-[11px] ${
          atencao ? 'text-amber-900' : 'text-rose-800'
        }`}
      >
        {atencao && <AlertTriangle className="size-3.5" aria-label="Pede atenção" />}
        <span className="font-semibold">Último FollowUP</span>
        {rotulo && (
          <span
            className={`rounded px-1.5 py-0.5 font-medium ${
              atencao ? 'bg-amber-200 text-amber-900' : 'bg-white/70 text-slate-700'
            }`}
          >
            {rotulo}
          </span>
        )}
        <span>{formatarInstante(f.dataEvento)}</span>
        {f.usuario && <span className="text-slate-500">por {f.usuario}</span>}
      </div>
      {f.observacao ? (
        <p className="line-clamp-2 whitespace-pre-line" title={f.observacao}>
          {f.observacao}
        </p>
      ) : (
        <p className="italic text-slate-400">(sem texto)</p>
      )}
    </div>
  );
}
