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
  useDesativarVeiculo,
  useListarVeiculos,
} from '@/features/veiculos/api/queries';
import { FormularioVeiculo } from '@/features/veiculos/components/FormularioVeiculo';
import { ROTULOS_TIPO_VEICULO, type VeiculoListItem } from '@/features/veiculos/types';

type EstadoModal = { tipo: 'fechado' } | { tipo: 'criar' } | { tipo: 'editar'; id: string };

export function VeiculosPage() {
  const lista = useListarVeiculos();
  const desativar = useDesativarVeiculo();
  const [estado, setEstado] = useState<EstadoModal>({ tipo: 'fechado' });
  const [paraExcluir, setParaExcluir] = useState<VeiculoListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const visiveis = (lista.data ?? []).filter((v) => v.ativo);

  const colunas: Coluna<VeiculoListItem>[] = [
    { chave: 'placa', cabecalho: 'Placa', render: (v) => v.placa },
    { chave: 'tipo', cabecalho: 'Tipo', render: (v) => ROTULOS_TIPO_VEICULO[v.tipo] ?? '—' },
    {
      chave: 'descricao',
      cabecalho: 'Veículo',
      render: (v) => (
        <span>
          {v.fabricante} {v.modelo}
          <span className="ml-1 text-xs text-gray-500">· {v.cor}</span>
        </span>
      ),
    },
    { chave: 'status', cabecalho: 'Status', render: (v) => <StatusBadge ativo={v.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (v) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => setEstado({ tipo: 'editar', id: v.id })}>
            <Pencil className="h-3.5 w-3.5" /> Editar
          </BotaoLinhaAcao>
          {v.ativo ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaExcluir(v)}>
              <Trash2 className="h-3.5 w-3.5" /> Excluir
            </BotaoLinhaAcao>
          ) : null}
        </div>
      ),
    },
  ];

  async function confirmar() {
    if (!paraExcluir) return;
    setErroAcao(null);
    try {
      await desativar.mutateAsync(paraExcluir.id);
      setParaExcluir(null);
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Veículos</h1>
          <p className="mt-1 text-sm text-gray-600">
            Frota utilizada para translado. Cada veículo tem layout de assentos por fileira.
          </p>
        </div>
        <Button onClick={() => setEstado({ tipo: 'criar' })}>
          <Plus className="h-4 w-4" />
          Novo veículo
        </Button>
      </header>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={visiveis}
        chaveLinha={(v) => v.id}
        carregando={lista.isLoading}
      />

      <Modal
        aberto={estado.tipo !== 'fechado'}
        aoFechar={() => setEstado({ tipo: 'fechado' })}
        titulo={estado.tipo === 'criar' ? 'Novo veículo' : 'Editar veículo'}
        largura="lg"
      >
        {estado.tipo !== 'fechado' ? (
          <FormularioVeiculo
            modo={estado.tipo}
            idVeiculo={estado.tipo === 'editar' ? estado.id : null}
            aoConcluir={() => setEstado({ tipo: 'fechado' })}
          />
        ) : null}
      </Modal>

      <ConfirmDialog
        aberto={Boolean(paraExcluir)}
        titulo="Excluir veículo"
        mensagem={
          paraExcluir
            ? `Excluir o veículo "${paraExcluir.placa}"? Ele deixará de aparecer nas listagens; o histórico permanece preservado.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Excluir"
        carregando={desativar.isPending}
        aoConfirmar={confirmar}
        aoCancelar={() => {
          setParaExcluir(null);
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
