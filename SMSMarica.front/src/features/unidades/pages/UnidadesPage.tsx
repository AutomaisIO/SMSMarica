import { useState } from 'react';
import { Eye, Pencil, Plus, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDesativarUnidade,
  useListarUnidades,
} from '@/features/unidades/api/queries';
import type { UnidadeListItem } from '@/features/unidades/types';

export function UnidadesPage() {
  const navigate = useNavigate();
  const lista = useListarUnidades();
  const desativar = useDesativarUnidade();
  const [paraDesativar, setParaDesativar] = useState<UnidadeListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const colunas: Coluna<UnidadeListItem>[] = [
    {
      chave: 'nome',
      cabecalho: 'Nome',
      render: (u) => (
        <button
          type="button"
          onClick={() => navigate(`/app/unidades/${u.id}`)}
          className="text-left font-medium text-red-700 hover:underline"
        >
          {u.nome}
        </button>
      ),
    },
    {
      chave: 'cidade',
      cabecalho: 'Cidade/UF',
      render: (u) => (u.cidade ? `${u.cidade}${u.uf ? `/${u.uf}` : ''}` : '—'),
    },
    { chave: 'status', cabecalho: 'Status', render: (u) => <StatusBadge ativo={u.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (u) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => navigate(`/app/unidades/${u.id}`)}>
            <Eye className="w-3.5 h-3.5" /> Ver
          </BotaoLinhaAcao>
          <BotaoLinhaAcao onClick={() => navigate(`/app/unidades/${u.id}/editar`)}>
            <Pencil className="w-3.5 h-3.5" /> Editar
          </BotaoLinhaAcao>
          {u.ativo ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaDesativar(u)}>
              <Trash2 className="w-3.5 h-3.5" /> Excluir
            </BotaoLinhaAcao>
          ) : null}
        </div>
      ),
    },
  ];

  async function confirmarDesativar() {
    if (!paraDesativar) return;
    setErroAcao(null);
    try {
      await desativar.mutateAsync(paraDesativar.id);
      setParaDesativar(null);
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Unidades</h1>
          <p className="mt-1 text-sm text-gray-600">Locais de saúde que realizam tratamentos.</p>
        </div>
        <Button onClick={() => navigate('/app/unidades/novo')}>
          <Plus className="w-4 h-4" />
          Nova unidade
        </Button>
      </header>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={(lista.data ?? []).filter((u) => u.ativo)}
        chaveLinha={(u) => u.id}
        carregando={lista.isLoading}
      />

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Excluir unidade"
        mensagem={
          paraDesativar
            ? `Excluir "${paraDesativar.nome}"? A unidade some das listagens; histórico de tratamentos é preservado.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Excluir"
        carregando={desativar.isPending}
        aoConfirmar={confirmarDesativar}
        aoCancelar={() => {
          setParaDesativar(null);
          setErroAcao(null);
        }}
      />

      {erroAcao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroAcao}
        </div>
      ) : null}
    </div>
  );
}
