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
  useDesativarUsuario,
  useListarUsuarios,
} from '@/features/usuarios/api/queries';
import { FormularioUsuario } from '@/features/usuarios/components/FormularioUsuario';
import { rotulosPerfil, type UsuarioListItem } from '@/features/usuarios/types';

type EstadoModal = { tipo: 'fechado' } | { tipo: 'criar' } | { tipo: 'editar'; id: string };

export function UsuariosPage() {
  const lista = useListarUsuarios();
  const desativar = useDesativarUsuario();
  const [estado, setEstado] = useState<EstadoModal>({ tipo: 'fechado' });
  const [paraDesativar, setParaDesativar] = useState<UsuarioListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const colunas: Coluna<UsuarioListItem>[] = [
    { chave: 'nome', cabecalho: 'Nome', render: (u) => u.nomeCompleto },
    { chave: 'email', cabecalho: 'E-mail', render: (u) => u.email },
    {
      chave: 'perfil',
      cabecalho: 'Perfil',
      render: (u) => (
        <span className="badge badge-primary">{rotulosPerfil[u.perfil]}</span>
      ),
    },
    { chave: 'status', cabecalho: 'Status', render: (u) => <StatusBadge ativo={u.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (u) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => setEstado({ tipo: 'editar', id: u.id })}>
            <Pencil className="w-3.5 h-3.5" /> Editar
          </BotaoLinhaAcao>
          {u.ativo ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaDesativar(u)}>
              <Trash2 className="w-3.5 h-3.5" /> Desativar
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
          <h1 className="text-2xl font-semibold text-gray-900">Usuários</h1>
          <p className="mt-1 text-sm text-gray-600">Contas com acesso ao ecossistema SMS Maricá.</p>
        </div>
        <Button onClick={() => setEstado({ tipo: 'criar' })}>
          <Plus className="w-4 h-4" />
          Novo usuário
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
        chaveLinha={(u) => u.id}
        carregando={lista.isLoading}
      />

      <Modal
        aberto={estado.tipo !== 'fechado'}
        aoFechar={() => setEstado({ tipo: 'fechado' })}
        titulo={estado.tipo === 'criar' ? 'Novo usuário' : 'Editar usuário'}
        largura="lg"
      >
        {estado.tipo !== 'fechado' ? (
          <FormularioUsuario
            modo={estado.tipo}
            idUsuario={estado.tipo === 'editar' ? estado.id : null}
            aoConcluir={() => setEstado({ tipo: 'fechado' })}
          />
        ) : null}
      </Modal>

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Desativar usuário"
        mensagem={
          paraDesativar
            ? `Desativar "${paraDesativar.nomeCompleto}"? A conta deixa de poder autenticar.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Desativar"
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
