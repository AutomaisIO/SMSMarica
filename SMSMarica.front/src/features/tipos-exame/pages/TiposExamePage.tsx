import { Link, useNavigate } from 'react-router-dom';
import { Edit2, Layers, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useExcluirTipoExame, useListarTiposExame } from '@/features/tipos-exame/api/queries';
import type { TipoExameListItem } from '@/features/tipos-exame/types';

export function TiposExamePage() {
  const navigate = useNavigate();
  const lista = useListarTiposExame(undefined, true);
  const excluir = useExcluirTipoExame();
  const [erro, setErro] = useState<string | null>(null);

  const podeCriar = usePermissao('TiposExame', 'Inclusao');
  const podeEditar = usePermissao('TiposExame', 'Edicao');
  const podeExcluir = usePermissao('TiposExame', 'Exclusao');

  function aoExcluir(t: TipoExameListItem) {
    if (!window.confirm(`Excluir o tipo de exame "${t.nome}"?`)) return;
    setErro(null);
    excluir.mutate(t.id, { onError: (e) => setErro(extrairMensagemDeErro(e)) });
  }

  const colunas: Coluna<TipoExameListItem>[] = [
    {
      chave: 'nome',
      cabecalho: 'Nome',
      render: (t) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900">{t.nome}</div>
          <div className="truncate text-xs text-gray-500 font-mono">SIGTAP {t.procedimentoSigtapCodigo}</div>
        </div>
      ),
    },
    {
      chave: 'modalidade',
      cabecalho: 'Modalidade',
      render: (t) => (
        <span className="rounded bg-gray-100 px-2 py-0.5 text-xs font-medium uppercase text-gray-700">
          {t.modalidadeDicom}
        </span>
      ),
    },
    {
      chave: 'tempo',
      cabecalho: 'Tempo (min)',
      render: (t) => t.tempoEstimadoMinutos ?? '—',
    },
    {
      chave: 'worklist',
      cabecalho: 'Worklist',
      render: (t) =>
        t.enviarParaWorklist ? (
          <span className="rounded border border-emerald-200 bg-emerald-50 px-1.5 py-0.5 text-xs font-medium text-emerald-700">
            Envia
          </span>
        ) : (
          <span className="rounded border border-gray-200 bg-gray-50 px-1.5 py-0.5 text-xs font-medium text-gray-500">
            Desligado
          </span>
        ),
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (t) => <StatusBadge ativo={t.ativo} />,
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (t) => (
        <div className="flex items-center justify-end gap-2">
          {podeEditar ? (
            <button
              type="button"
              onClick={() => navigate(`/app/tipos-exame/${t.id}`)}
              title="Editar"
              className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
            >
              <Edit2 className="h-3.5 w-3.5" />
              Editar
            </button>
          ) : null}
          {podeExcluir ? (
            <button
              type="button"
              onClick={() => aoExcluir(t)}
              title="Excluir"
              className="inline-flex items-center gap-1 rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50"
            >
              <Trash2 className="h-3.5 w-3.5" />
              Excluir
            </button>
          ) : null}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <Layers className="h-6 w-6 text-primary-600" />
            Tipos de Exame
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Catálogo curado de exames ofertados pela SMS Maricá, amarrado ao código oficial SIGTAP.
          </p>
        </div>
        {podeCriar ? (
          <Link to="/app/tipos-exame/novo">
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Novo tipo
            </Button>
          </Link>
        ) : null}
      </header>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(t) => t.id}
        carregando={lista.isPending}
        vazio="Nenhum tipo de exame cadastrado."
      />
    </div>
  );
}
