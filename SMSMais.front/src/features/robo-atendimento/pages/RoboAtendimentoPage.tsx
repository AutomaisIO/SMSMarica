import { useState } from 'react';
import { Bot, Edit2, Plus, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { ConfiguracaoRoboCard } from '@/features/robo-atendimento/components/ConfiguracaoRoboCard';
import { EditorAssunto } from '@/features/robo-atendimento/components/EditorAssunto';
import { ErrosRoboCard } from '@/features/robo-atendimento/components/ErrosRoboCard';
import { SimuladorRoboCard } from '@/features/robo-atendimento/components/SimuladorRoboCard';
import { useExcluirAssunto, useListarAssuntos } from '@/features/robo-atendimento/api/queries';
import type { RoboAssuntoListItem } from '@/features/robo-atendimento/types';

export function RoboAtendimentoPage() {
  const [incluirInativos, setIncluirInativos] = useState(false);
  const lista = useListarAssuntos(incluirInativos);
  const excluir = useExcluirAssunto();

  const [editorAberto, setEditorAberto] = useState(false);
  const [assuntoId, setAssuntoId] = useState<string | null>(null);

  function abrirNovo() {
    setAssuntoId(null);
    setEditorAberto(true);
  }

  function abrirEdicao(a: RoboAssuntoListItem) {
    setAssuntoId(a.id);
    setEditorAberto(true);
  }

  function aoExcluir(a: RoboAssuntoListItem) {
    if (!window.confirm(`Excluir o assunto "${a.nome}"?`)) return;
    excluir.mutate(a.id, { onError: (err) => window.alert(extrairMensagemDeErro(err)) });
  }

  const colunas: Coluna<RoboAssuntoListItem>[] = [
    {
      chave: 'nome',
      cabecalho: 'Assunto',
      render: (a) => (
        <div>
          <span className="font-medium text-gray-900">{a.nome}</span>
          {a.padrao ? (
            <span className="ml-2 rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-medium text-amber-800">
              padrão
            </span>
          ) : null}
          {a.descricao ? <p className="text-xs text-gray-500">{a.descricao}</p> : null}
        </div>
      ),
    },
    { chave: 'modelo', cabecalho: 'Modelo', render: (a) => a.modelo ?? <span className="text-gray-400">padrão</span> },
    { chave: 'comandos', cabecalho: 'Comandos', render: (a) => a.qtdComandos },
    { chave: 'status', cabecalho: 'Status', render: (a) => <StatusBadge ativo={a.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (a) => (
        <div className="flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={() => abrirEdicao(a)}
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
          >
            <Edit2 className="h-3.5 w-3.5" />
            Editar
          </button>
          <button
            type="button"
            onClick={() => aoExcluir(a)}
            className="inline-flex items-center gap-1 rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50"
          >
            <Trash2 className="h-3.5 w-3.5" />
            Excluir
          </button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <Bot className="h-6 w-6 text-primary-600" />
            Robô de Atendimento
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Assuntos que o robô atende no WhatsApp, com treinos, condições e comandos liberados por assunto.
          </p>
        </div>
        <Button onClick={abrirNovo}>
          <Plus className="mr-2 h-4 w-4" />
          Novo assunto
        </Button>
      </header>

      <ConfiguracaoRoboCard />

      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-gray-900">Assuntos</h2>
        <label className="flex items-center gap-2 text-sm text-gray-600">
          <input type="checkbox" checked={incluirInativos} onChange={(e) => setIncluirInativos(e.target.checked)} />
          Incluir inativos
        </label>
      </div>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(a) => a.id}
        carregando={lista.isPending}
        vazio="Nenhum assunto cadastrado."
      />

      <SimuladorRoboCard />

      <ErrosRoboCard />

      <EditorAssunto assuntoId={assuntoId} aberto={editorAberto} aoFechar={() => setEditorAberto(false)} />
    </div>
  );
}
