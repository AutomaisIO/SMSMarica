import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Edit2, FileCog, Plus, Power, PowerOff } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDesativarTemplate,
  useListarTemplates,
  useReativarTemplate,
} from '@/features/laudo-templates/api/queries';
import type { LaudoTemplateListItem } from '@/features/laudo-templates/types';

export function LaudoTemplatesListagemPage() {
  const navigate = useNavigate();
  const [filtroCategoria, setFiltroCategoria] = useState('');
  const [incluirInativos, setIncluirInativos] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const lista = useListarTemplates(filtroCategoria || undefined, incluirInativos);
  const desativar = useDesativarTemplate();
  const reativar = useReativarTemplate();

  const podeCriar = usePermissao('LaudosTemplates', 'Inclusao');
  const podeEditar = usePermissao('LaudosTemplates', 'Edicao');
  const podeExcluir = usePermissao('LaudosTemplates', 'Exclusao');

  function alternarAtivacao(t: LaudoTemplateListItem) {
    setErro(null);
    const acao = t.ativo ? desativar : reativar;
    acao.mutate(t.id, {
      onError: (e) => setErro(extrairMensagemDeErro(e)),
    });
  }

  const colunas: Coluna<LaudoTemplateListItem>[] = [
    {
      chave: 'nome',
      cabecalho: 'Nome',
      render: (t) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900">{t.nome}</div>
          {t.descricao ? (
            <div className="truncate text-xs text-gray-500">{t.descricao}</div>
          ) : null}
        </div>
      ),
    },
    {
      chave: 'categoria',
      cabecalho: 'Categoria',
      render: (t) => (
        <span className="rounded bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-700">
          {t.categoria}
        </span>
      ),
    },
    {
      chave: 'criadoEm',
      cabecalho: 'Criado em',
      render: (t) => new Date(t.criadoEm).toLocaleDateString('pt-BR'),
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
              onClick={() => navigate(`/app/laudo-templates/${t.id}`)}
              title="Editar template"
              className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
            >
              <Edit2 className="h-3.5 w-3.5" />
              Editar
            </button>
          ) : null}
          {podeExcluir ? (
            <button
              type="button"
              onClick={() => alternarAtivacao(t)}
              title={t.ativo ? 'Desativar' : 'Reativar'}
              className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
            >
              {t.ativo ? <PowerOff className="h-3.5 w-3.5" /> : <Power className="h-3.5 w-3.5" />}
              {t.ativo ? 'Desativar' : 'Reativar'}
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
            <FileCog className="h-6 w-6 text-primary-600" />
            Templates de laudo
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Modelos institucionais usados como ponto de partida no editor de laudos.
          </p>
        </div>
        {podeCriar ? (
          <Link to="/app/laudo-templates/novo">
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Novo template
            </Button>
          </Link>
        ) : null}
      </header>

      <div className="grid grid-cols-1 gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-4">
        <Campo label="Categoria" htmlFor="categoria">
          <Input
            id="categoria"
            value={filtroCategoria}
            onChange={(e) => setFiltroCategoria(e.target.value)}
            placeholder="Ex.: Mamografia"
          />
        </Campo>
        <Campo label=" " htmlFor="inativos">
          <label className="flex h-10 items-center gap-2 text-sm text-gray-700">
            <input
              id="inativos"
              type="checkbox"
              checked={incluirInativos}
              onChange={(e) => setIncluirInativos(e.target.checked)}
            />
            Incluir inativos
          </label>
        </Campo>
      </div>

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
        vazio="Nenhum template cadastrado."
      />
    </div>
  );
}
