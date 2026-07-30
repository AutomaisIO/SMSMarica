import { useState } from 'react';
import { Edit2, Plus, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useExcluirEquipamento,
  useListarEquipamentos,
} from '@/features/equipamentos/api/queries';
import { ModalEquipamento } from '@/features/equipamentos/components/ModalEquipamento';
import { rotuloModalidade, type EquipamentoListItem } from '@/features/equipamentos/types';

type Props = { unidadeId: string };

/**
 * Aba "Equipamentos" do detalhe da unidade: os equipamentos de imagem desta
 * unidade, com incluir/editar/excluir. Mesma informação da página standalone
 * `/app/equipamentos`, filtrada pela unidade e com a unidade já fixada no modal.
 */
export function EquipamentosDaUnidadeSecao({ unidadeId }: Props) {
  const [incluirInativos, setIncluirInativos] = useState(false);
  const lista = useListarEquipamentos(unidadeId, incluirInativos);
  const excluir = useExcluirEquipamento();

  const [modalAberto, setModalAberto] = useState(false);
  const [emEdicao, setEmEdicao] = useState<EquipamentoListItem | null>(null);

  function abrirNovo() {
    setEmEdicao(null);
    setModalAberto(true);
  }

  function abrirEdicao(e: EquipamentoListItem) {
    setEmEdicao(e);
    setModalAberto(true);
  }

  function aoExcluir(e: EquipamentoListItem) {
    if (!window.confirm(`Excluir o equipamento "${e.nome}"?`)) return;
    excluir.mutate(e.id, { onError: (err) => window.alert(extrairMensagemDeErro(err)) });
  }

  const colunas: Coluna<EquipamentoListItem>[] = [
    { chave: 'nome', cabecalho: 'Equipamento', render: (e) => <span className="font-medium text-gray-900">{e.nome}</span> },
    { chave: 'modalidade', cabecalho: 'Modalidade', render: (e) => rotuloModalidade(e.modalidadeDicom) },
    {
      chave: 'ae',
      cabecalho: 'AE Title',
      render: (e) =>
        e.identificadorDicom ? (
          <span className="font-mono text-xs text-gray-800">{e.identificadorDicom}</span>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        ),
    },
    { chave: 'status', cabecalho: 'Status', render: (e) => <StatusBadge ativo={e.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (e) => (
        <div className="flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={() => abrirEdicao(e)}
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
          >
            <Edit2 className="h-3.5 w-3.5" />
            Editar
          </button>
          <button
            type="button"
            onClick={() => aoExcluir(e)}
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
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <label className="flex items-center gap-2 text-sm text-gray-600">
          <input type="checkbox" checked={incluirInativos} onChange={(e) => setIncluirInativos(e.target.checked)} />
          Incluir inativos
        </label>
        <Button onClick={abrirNovo}>
          <Plus className="mr-2 h-4 w-4" />
          Novo equipamento
        </Button>
      </div>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(e) => e.id}
        carregando={lista.isPending}
        vazio={
          !lista.isPending && (lista.data?.length ?? 0) === 0
            ? 'Nenhum equipamento cadastrado nesta unidade.'
            : undefined
        }
      />

      <ModalEquipamento
        aberto={modalAberto}
        aoFechar={() => setModalAberto(false)}
        equipamento={emEdicao}
        unidadeIdFixa={unidadeId}
      />
    </div>
  );
}
