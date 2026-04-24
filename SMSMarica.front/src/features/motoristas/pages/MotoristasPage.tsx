import { useState } from 'react';
import { Eye, Pencil, Plus, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Modal } from '@/shared/ui/Modal';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDesativarMotorista,
  useListarMotoristas,
} from '@/features/motoristas/api/queries';
import { FormularioMotorista } from '@/features/motoristas/components/FormularioMotorista';
import type { MotoristaListItem } from '@/features/motoristas/types';

type EstadoModal = { tipo: 'fechado' } | { tipo: 'criar' } | { tipo: 'editar'; id: string };

export function MotoristasPage() {
  const navigate = useNavigate();
  const lista = useListarMotoristas();
  const desativar = useDesativarMotorista();
  const [estado, setEstado] = useState<EstadoModal>({ tipo: 'fechado' });
  const [paraDesativar, setParaDesativar] = useState<MotoristaListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const colunas: Coluna<MotoristaListItem>[] = [
    {
      chave: 'nome',
      cabecalho: 'Nome',
      render: (m) => (
        <button
          type="button"
          onClick={() => navigate(`/operador/motoristas/${m.id}`)}
          className="text-left font-medium text-red-700 hover:underline"
        >
          {m.nomeCompleto}
        </button>
      ),
    },
    { chave: 'cpf', cabecalho: 'CPF', render: (m) => m.cpf },
    { chave: 'status', cabecalho: 'Status', render: (m) => <StatusBadge ativo={m.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (m) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => navigate(`/operador/motoristas/${m.id}`)}>
            <Eye className="w-3.5 h-3.5" /> Ver
          </BotaoLinhaAcao>
          <BotaoLinhaAcao onClick={() => setEstado({ tipo: 'editar', id: m.id })}>
            <Pencil className="w-3.5 h-3.5" /> Editar
          </BotaoLinhaAcao>
          {m.ativo ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaDesativar(m)}>
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
          <h1 className="text-2xl font-semibold text-gray-900">Motoristas</h1>
          <p className="mt-1 text-sm text-gray-600">Agentes de transporte sanitário.</p>
        </div>
        <Button onClick={() => setEstado({ tipo: 'criar' })}>
          <Plus className="w-4 h-4" />
          Novo motorista
        </Button>
      </header>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={(lista.data ?? []).filter((m) => m.ativo)}
        chaveLinha={(m) => m.id}
        carregando={lista.isLoading}
      />

      <Modal
        aberto={estado.tipo !== 'fechado'}
        aoFechar={() => setEstado({ tipo: 'fechado' })}
        titulo={estado.tipo === 'criar' ? 'Novo motorista' : 'Editar motorista'}
        largura="lg"
      >
        {estado.tipo !== 'fechado' ? (
          <FormularioMotorista
            modo={estado.tipo}
            idMotorista={estado.tipo === 'editar' ? estado.id : null}
            aoConcluir={() => setEstado({ tipo: 'fechado' })}
          />
        ) : null}
      </Modal>

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Excluir motorista"
        mensagem={
          paraDesativar
            ? `Excluir "${paraDesativar.nomeCompleto}"? O motorista some das listagens; histórico de rotas é preservado.`
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
