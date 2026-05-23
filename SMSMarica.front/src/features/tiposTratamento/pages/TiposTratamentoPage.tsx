import { useState } from 'react';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Modal } from '@/shared/ui/Modal';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDesativarTipoTratamento,
  useListarTiposTratamento,
} from '@/features/tiposTratamento/api/queries';
import { FormularioTipoTratamento } from '@/features/tiposTratamento/components/FormularioTipoTratamento';
import type { TipoTratamentoListItem } from '@/features/tiposTratamento/types';

type EstadoModal = { tipo: 'fechado' } | { tipo: 'criar' } | { tipo: 'editar'; id: string };

export function TiposTratamentoPage() {
  const lista = useListarTiposTratamento(false);
  const desativar = useDesativarTipoTratamento();
  const [estado, setEstado] = useState<EstadoModal>({ tipo: 'fechado' });
  const [paraDesativar, setParaDesativar] = useState<TipoTratamentoListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const colunas: Coluna<TipoTratamentoListItem>[] = [
    { chave: 'nome', cabecalho: 'Nome', render: (t) => <span className="text-gray-900">{t.nome}</span> },
    { chave: 'codigo', cabecalho: 'Código', render: (t) => <code className="text-xs text-gray-600">{t.codigo}</code> },
    { chave: 'status', cabecalho: 'Status', render: (t) => <StatusBadge ativo={t.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (t) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => setEstado({ tipo: 'editar', id: t.id })}>
            <Pencil className="w-3.5 h-3.5" /> Editar
          </BotaoLinhaAcao>
          {t.ativo ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaDesativar(t)}>
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
          <h1 className="text-2xl font-semibold text-gray-900">Tipos de tratamento</h1>
          <p className="mt-1 text-sm text-gray-600">
            Catálogo usado pelos tratamentos (Hemodiálise, Radioterapia, etc.).
          </p>
        </div>
        <Button onClick={() => setEstado({ tipo: 'criar' })}>
          <Plus className="w-4 h-4" />
          Novo tipo
        </Button>
      </header>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(t) => t.id}
        carregando={lista.isLoading}
      />

      <Modal
        aberto={estado.tipo !== 'fechado'}
        aoFechar={() => setEstado({ tipo: 'fechado' })}
        titulo={estado.tipo === 'criar' ? 'Novo tipo de tratamento' : 'Editar tipo de tratamento'}
        largura="md"
      >
        {estado.tipo !== 'fechado' ? (
          <FormularioTipoTratamento
            modo={estado.tipo}
            id={estado.tipo === 'editar' ? estado.id : null}
            aoConcluir={() => setEstado({ tipo: 'fechado' })}
          />
        ) : null}
      </Modal>

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Excluir tipo de tratamento"
        mensagem={
          paraDesativar
            ? `Excluir "${paraDesativar.nome}"? Tratamentos existentes que apontam para este tipo continuarão referenciando-o.`
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
