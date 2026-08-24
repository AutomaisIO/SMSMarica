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
  useDesativarPerfil,
  useListarPerfis,
} from '@/features/perfis/api/queries';
import { FormularioPerfil } from '@/features/perfis/components/FormularioPerfil';
import type { PerfilListItem } from '@/features/perfis/types';

type EstadoModal = { tipo: 'fechado' } | { tipo: 'criar' } | { tipo: 'editar'; id: string };

export function PerfisPage() {
  const lista = useListarPerfis();
  const desativar = useDesativarPerfil();
  const [estado, setEstado] = useState<EstadoModal>({ tipo: 'fechado' });
  const [paraDesativar, setParaDesativar] = useState<PerfilListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const colunas: Coluna<PerfilListItem>[] = [
    { chave: 'nome', cabecalho: 'Nome', render: (p) => <span className="text-gray-900">{p.nome}</span> },
    {
      chave: 'descricao',
      cabecalho: 'Descrição',
      render: (p) => <span className="text-gray-600">{p.descricao ?? '—'}</span>,
    },
    {
      chave: 'modulos',
      cabecalho: 'Módulos',
      render: (p) => <span className="text-xs text-gray-600">{p.modulos}</span>,
    },
    { chave: 'status', cabecalho: 'Status', render: (p) => <StatusBadge ativo={p.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (p) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => setEstado({ tipo: 'editar', id: p.id })}>
            <Pencil className="w-3.5 h-3.5" /> Editar
          </BotaoLinhaAcao>
          {p.ativo ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaDesativar(p)}>
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
          <h1 className="text-2xl font-semibold text-gray-900">Perfis</h1>
          <p className="mt-1 text-sm text-gray-600">
            Conjuntos reutilizáveis de permissões. Cada usuário pode ter um ou mais perfis.
          </p>
        </div>
        <Button onClick={() => setEstado({ tipo: 'criar' })}>
          <Plus className="w-4 h-4" />
          Novo perfil
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
        chaveLinha={(p) => p.id}
        carregando={lista.isLoading}
      />

      <Modal
        aberto={estado.tipo !== 'fechado'}
        aoFechar={() => setEstado({ tipo: 'fechado' })}
        titulo={estado.tipo === 'criar' ? 'Novo perfil' : 'Editar perfil'}
        largura="lg"
      >
        {estado.tipo !== 'fechado' ? (
          <FormularioPerfil
            modo={estado.tipo}
            id={estado.tipo === 'editar' ? estado.id : null}
            aoConcluir={() => setEstado({ tipo: 'fechado' })}
          />
        ) : null}
      </Modal>

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Excluir perfil"
        mensagem={
          paraDesativar
            ? `Excluir "${paraDesativar.nome}"? Usuários que ainda apontam para este perfil perderão as permissões herdadas dele.`
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
