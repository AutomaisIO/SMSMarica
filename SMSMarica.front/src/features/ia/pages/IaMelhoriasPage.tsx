import { useState } from 'react';
import { Link } from 'react-router-dom';
import { BookOpen, Sparkles, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import {
  useAprendizados,
  useCorrecoes,
  useDesativarAprendizado,
  useFeedbacks,
  useTratarFeedback,
} from '@/features/ia/api/queries';
import type { AprendizadoIa, CorrecaoIa, FeedbackIa } from '@/features/ia/types';

function formatarDataHora(iso?: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}

const ORIGEM_BADGE: Record<string, string> = {
  Auto: 'bg-amber-100 text-amber-800',
  Manual: 'bg-blue-100 text-blue-700',
};

function SecaoAprendizados() {
  const q = useAprendizados();
  const desativar = useDesativarAprendizado();

  function remover(a: AprendizadoIa) {
    if (!window.confirm(`Remover esta instrução aprendida?\n\n"${a.conteudo}"`)) return;
    desativar.mutate(a.id);
  }

  const colunas: Coluna<AprendizadoIa>[] = [
    {
      chave: 'origem',
      cabecalho: 'Origem',
      render: (a) => (
        <span
          className={`rounded px-2 py-0.5 text-xs font-medium uppercase ${
            ORIGEM_BADGE[a.origem] ?? 'bg-gray-100 text-gray-700'
          }`}
        >
          {a.origem === 'Auto' ? 'Automática' : a.origem}
        </span>
      ),
    },
    { chave: 'tipo', cabecalho: 'Tipo', render: (a) => <span className="text-sm text-gray-700">{a.tipo}</span> },
    { chave: 'fonte', cabecalho: 'Base', render: (a) => <span className="text-sm text-gray-700">{a.fonteNome}</span> },
    {
      chave: 'conteudo',
      cabecalho: 'Instrução aprendida',
      render: (a) => <span className="block max-w-xl whitespace-pre-wrap text-sm text-gray-900">{a.conteudo}</span>,
    },
    {
      chave: 'criadoEm',
      cabecalho: 'Quando',
      render: (a) => <span className="whitespace-nowrap text-xs text-gray-500">{formatarDataHora(a.criadoEm)}</span>,
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (a) => (
        <button
          type="button"
          onClick={() => remover(a)}
          disabled={desativar.isPending}
          title="Remover instrução aprendida"
          className="inline-flex items-center gap-1 rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50 disabled:opacity-50"
        >
          <Trash2 className="h-3.5 w-3.5" />
          Remover
        </button>
      ),
    },
  ];

  return (
    <div className="space-y-3">
      {q.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(q.error)}
        </div>
      ) : null}
      <Tabela
        colunas={colunas}
        dados={q.data ?? []}
        chaveLinha={(a) => a.id}
        carregando={q.isPending}
        vazio="Nenhuma instrução aprendida ainda. À medida que a IA corrige erros, as melhorias aparecem aqui."
      />
    </div>
  );
}

function SecaoCorrecoes() {
  const q = useCorrecoes();

  const colunas: Coluna<CorrecaoIa>[] = [
    {
      chave: 'criadoEm',
      cabecalho: 'Quando',
      render: (c) => <span className="whitespace-nowrap text-xs text-gray-500">{formatarDataHora(c.criadoEm)}</span>,
    },
    {
      chave: 'pergunta',
      cabecalho: 'Pergunta',
      render: (c) => <span className="block max-w-xs whitespace-pre-wrap text-sm text-gray-900">{c.pergunta}</span>,
    },
    {
      chave: 'erro',
      cabecalho: 'Erro corrigido',
      render: (c) => <span className="block max-w-xs whitespace-pre-wrap text-xs text-red-700">{c.erroOriginal}</span>,
    },
    {
      chave: 'instrucao',
      cabecalho: 'Aprendizado gerado',
      render: (c) =>
        c.instrucaoGerada ? (
          <span className="block max-w-md whitespace-pre-wrap text-sm text-gray-700">{c.instrucaoGerada}</span>
        ) : (
          <span className="text-gray-400">—</span>
        ),
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (c) =>
        c.removidoEm ? (
          <span className="rounded bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-500">Removido</span>
        ) : (
          <span className="rounded bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700">Ativo</span>
        ),
    },
  ];

  return (
    <div className="space-y-3">
      {q.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(q.error)}
        </div>
      ) : null}
      <Tabela
        colunas={colunas}
        dados={q.data ?? []}
        chaveLinha={(c) => c.id}
        carregando={q.isPending}
        vazio="Nenhuma correção automática registrada ainda."
      />
    </div>
  );
}

function SecaoAvaliacoes() {
  const q = useFeedbacks(true);
  const tratar = useTratarFeedback();
  const [resol, setResol] = useState<Record<string, string>>({});

  function agir(f: FeedbackIa, descartar: boolean) {
    tratar.mutate({ id: f.id, descartar, resolucao: resol[f.id] });
  }

  const itens = q.data ?? [];

  return (
    <div className="space-y-3">
      {q.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(q.error)}
        </div>
      ) : null}
      {q.isPending ? <p className="text-sm text-gray-400">Carregando…</p> : null}
      {!q.isPending && itens.length === 0 ? (
        <p className="rounded-lg border border-dashed border-gray-200 p-6 text-center text-sm text-gray-400">
          Nenhuma avaliação pendente. Quando alguém marcar 👎 numa resposta da Consulta Inteligente,
          ela aparece aqui para virar conhecimento da base.
        </p>
      ) : null}
      {itens.map((f) => (
        <div key={f.id} className="rounded-lg border border-gray-200 bg-white p-4">
          <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
            <span className="rounded bg-gray-100 px-2 py-0.5 font-medium text-gray-700">{f.fonteNome}</span>
            {f.familia ? (
              <span className="rounded bg-primary-50 px-2 py-0.5 font-medium text-primary-700">
                família: {f.familia}
              </span>
            ) : null}
            <span>{formatarDataHora(f.criadoEm)}</span>
          </div>

          <p className="mt-2 text-xs font-semibold uppercase tracking-wide text-gray-400">Pergunta</p>
          <p className="whitespace-pre-wrap text-sm text-gray-900">{f.pergunta}</p>

          {f.resposta ? (
            <>
              <p className="mt-2 text-xs font-semibold uppercase tracking-wide text-gray-400">
                Resposta dada
              </p>
              <p className="max-h-40 overflow-y-auto whitespace-pre-wrap text-sm text-gray-600">
                {f.resposta}
              </p>
            </>
          ) : null}

          {f.comentario ? (
            <div className="mt-2 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
              <span className="font-medium">O que faltou:</span> {f.comentario}
            </div>
          ) : null}

          <div className="mt-3 flex flex-col gap-2">
            <input
              value={resol[f.id] ?? ''}
              onChange={(e) => setResol((r) => ({ ...r, [f.id]: e.target.value }))}
              placeholder="O que foi feito (ex.: adicionei doc sobre atendimentos por período)…"
              className="w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm text-gray-700 focus:border-primary-500 focus:outline-none"
            />
            <div className="flex flex-wrap items-center gap-2">
              <Link
                to="/app/ia/configuracao"
                className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
              >
                <BookOpen className="h-3.5 w-3.5" />
                Enriquecer conhecimento da base
              </Link>
              <button
                type="button"
                onClick={() => agir(f, false)}
                disabled={tratar.isPending}
                className="rounded-md bg-emerald-600 px-3 py-1 text-xs font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
              >
                Marcar como tratado
              </button>
              <button
                type="button"
                onClick={() => agir(f, true)}
                disabled={tratar.isPending}
                className="rounded-md border border-gray-300 px-3 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-50"
              >
                Descartar
              </button>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

export function IaMelhoriasPage() {
  const abas: Aba[] = [
    { id: 'avaliacoes', rotulo: 'Avaliações pendentes', conteudo: <SecaoAvaliacoes /> },
    { id: 'aprendizados', rotulo: 'Aprendizados ativos', conteudo: <SecaoAprendizados /> },
    { id: 'correcoes', rotulo: 'Histórico de correções', conteudo: <SecaoCorrecoes /> },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Sparkles className="h-6 w-6 text-primary-600" />
          Melhorias da IA
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Evolução do aprendizado: instruções que a IA aprendeu (manualmente ou ao corrigir erros) e o
          histórico de correções. Você pode remover qualquer instrução aplicada.
        </p>
      </header>

      <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <Tabs abas={abas} />
      </div>
    </div>
  );
}
